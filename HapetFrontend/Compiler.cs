using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text;

namespace HapetFrontend
{
    public partial class Compiler : ITextOnLocationProvider
    {
        /// <summary>
        /// Path and file
        /// </summary>
        private readonly Dictionary<string, ProgramFile> _files = new Dictionary<string, ProgramFile>();

        /// <summary>
        /// All the namespaces in the project
        /// </summary>
        private readonly Dictionary<string, Scope> _nameSpaces = new Dictionary<string, Scope>();

        public IMessageHandler MessageHandler { get; }
        public CompilerSettings CurrentProjectSettings { get; }
        public ProjectData CurrentProjectData { get; }
        public Stopwatch CompilationStopwatch { get; set; }

        public Scope GlobalScope { get; private set; }

        /// <summary>
        /// The main function like an entry point of a program
        /// </summary>
        public AstFuncDecl MainFunction { get; set; }

        /// <summary>
        /// The function that calls dependent stors callers and current stors
        /// </summary>
        public AstFuncDecl StorsCallerFunction { get; set; }

        /// <summary>
        /// Handles all lambda and nested func declarations for easier inference
        /// </summary>
        public List<AstStatement> LambdasAndNested { get; } = new List<AstStatement>();

        public Compiler(CompilerSettings projectSettings, ProjectData projectData, IMessageHandler messageHandler)
        {
            CurrentProjectSettings = projectSettings;
            CurrentProjectData = projectData;
            MessageHandler = messageHandler;
        }

        public void InitGlobalScope()
        {
            GlobalScope = new Scope("global_scope_of_assembly");
            GlobalScope.DefineBuiltInTypes();
            GlobalScope.DefineBuiltInOperators();
        }

        public void GenerateAstTree()
        {
            // getting all files in project folder
            var allFilesInProjectFolder = (new DirectoryInfo(Path.GetDirectoryName(CurrentProjectSettings.ProjectPath))).EnumerateFiles("*", SearchOption.AllDirectories);
            foreach (var file in allFilesInProjectFolder)
            {
                if (Path.GetExtension(file.FullName) == ".hpt")
                    AddFile(file.FullName);
            }
        }

        public MetadataMetadataJson HandleExternalMetadata(string fileName, string metadataText)
        {
            var metaFile = new ProgramFile(Path.GetFileName(fileName), metadataText);
            metaFile.FilePath = new Uri(fileName);
            // just parsing metadata and adding its files into the compiler
            var files = ParseMetadata(metaFile, out var metadata);
            foreach (var f in files)
            {
                AddFile(f, f.Name);
            }
            return metadata;
        }

        public ProgramFile AddFile(ProgramFile file, string filePath)
        {
            if (_files.TryGetValue(filePath, out ProgramFile value))
            {
                return value;
            }
            _files[filePath] = file;
            return file;
        }

        public ProgramFile AddFile(string fileName)
        {
            if (!CompilerUtils.ValidateFilePath("", fileName, false, out string filePath))
            {
                return null;
            }

            if (_files.TryGetValue(filePath, out ProgramFile value))
            {
                return value;
            }

            var file = ParseFile(filePath);
            if (file == null)
                return null;

            return file;
        }

        public List<ProgramFile> ParseMetadata(ProgramFile metadataFile, out MetadataMetadataJson metadata)
        {
            metadata = null;
            var lexer = Lexer.FromFile(metadataFile, MessageHandler);

            if (lexer == null)
                return null;

            var parser = new Parser(lexer, this, MessageHandler);

            List<ProgramFile> allFiles = new List<ProgramFile>();
            ProgramFile currentFile = null;

            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;

            // parsing metadata
            inInfo.ExternalMetadata = true;

            while (true)
            {
                parser.CurrentSourceFile = currentFile;

                var s = parser.ParseTopLevel(inInfo, ref outInfo);
                if (s == null)
                    break;

                if (s is AstDirectiveStmt dir2 && dir2.DirectiveType == Enums.DirectiveType.MetadataMeta)
                {
                    while (lexer.PeekToken().Type != TokenType.SharpIdentifier)
                        lexer.SkipLine();
                    var end = parser.ParseTopLevel(inInfo, ref outInfo);

                    var metaText = lexer.Text.Substring(s.Location.Ending.End, end.Location.Beginning.Index - s.Location.Ending.End);
                    metadata = JsonConvert.DeserializeObject<MetadataMetadataJson>(metaText); // why do we need it
                    continue; // no need to add this shite
                }

                // create a virtual file of the directive
                if (s is AstDirectiveStmt dir && dir.DirectiveType == Enums.DirectiveType.MetadataFile)
                {
                    // creating a virtual file
                    currentFile = new ProgramFile(Path.GetFileName((dir.Value as AstStringExpr).StringValue), lexer.Text);
                    currentFile.FilePath = new Uri((dir.Value as AstStringExpr).StringValue);
                    allFiles.Add(currentFile);
                    // change lexer locations' filename
                    lexer.ChangeFilename(currentFile.Name);
                    continue; // no need to add this shite
                }

                // should not happen
                if (currentFile == null)
                {
                    MessageHandler.ReportMessage(metadataFile, s, [], ErrorCode.Get(CTEN.NullProgramFileInMeta));
                    break;
                }

                // check for namespace 
                if (s is AstNamespaceStmt nsStmt)
                {
                    // generating namespace scope and doing some shite with it
                    string normalNamespace = nsStmt.NameExpression.TryFlatten(MessageHandler, currentFile);
                    var nsScope = GetNamespaceScope(normalNamespace);
                    currentFile.NamespaceScope = nsScope;
                    currentFile.Namespace = normalNamespace;
                    currentFile.IsImported = true;
                    continue;
                }

                // just handle directive and do not add them
                if (s is AstDirectiveStmt dir3)
                {
                    var statementsToAdd = parser.HandleDirective(dir3, currentFile, inInfo, ref outInfo);
                    foreach (var ss in statementsToAdd)
                        HandleStatement(ss, currentFile, metadataFile);
                }
                else
                    HandleStatement(s, currentFile, metadataFile);
            }

            return allFiles;
        }

        private ProgramFile ParseFile(string fileName)
        {
            if (!File.Exists(fileName))
            {
                MessageHandler.ReportMessage([fileName], ErrorCode.Get(CTEN.FileForLexerNotFound));
                return null;
            }
            var text = File.ReadAllText(fileName, Encoding.UTF8)
                           .Replace("\r\n", "\n", StringComparison.InvariantCulture)
                           .Replace("\t", "    ", StringComparison.InvariantCulture);
            var file = new ProgramFile(Path.GetFileName(fileName), text);
            file.FilePath = new Uri(fileName);
            _files[fileName] = file;

            var lexer = Lexer.FromFile(file, MessageHandler);
            if (lexer == null)
                return null;

            var parser = new Parser(lexer, this, MessageHandler);
            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;
            while (true)
            {
                parser.CurrentSourceFile = file;

                var s = parser.ParseTopLevel(inInfo, ref outInfo);
                if (s == null)
                    break;

                // just handle directive and do not add them
                if (s is AstDirectiveStmt dir3)
                {
                    var statementsToAdd = parser.HandleDirective(dir3, file, inInfo, ref outInfo);
                    foreach (var ss in statementsToAdd)
                        HandleStatement(ss, file, file);
                }
                else
                    HandleStatement(s, file, file);
            }

            string normalNamespace = CompilerUtils.GetNamespace(CurrentProjectSettings.ProjectPath, CurrentProjectSettings.RootNamespace, fileName);
            GetCustomNamespaceIfDeclared(file, ref normalNamespace); // will change the namespace if declared

            // generating namespace scope and doing some shite with it
            var nsScope = GetNamespaceScope(normalNamespace);
            file.NamespaceScope = nsScope;
            file.Namespace = normalNamespace;

            return file;
        }

        private void HandleStatement(AstStatement s, ProgramFile file, ProgramFile currentlyParsingFile)
        {
            if (s is AstEnumDecl ||
                s is AstStructDecl ||
                s is AstClassDecl ||
                s is AstDelegateDecl ||
                s is AstUsingStmt ||
                s is AstNamespaceStmt ||
                s is AstDirectiveStmt)
            {
                s.SourceFile = file;
                file.Statements.Add(s);

                // if it is a 'using' add it to the list
                if (s is AstUsingStmt usng)
                    file.Usings.Add(usng);
            }
            else if (s != null)
            {
                MessageHandler.ReportMessage(currentlyParsingFile, s, [], ErrorCode.Get(CTEN.StmtNotAllowedInThis));
            }
        }

        private void GetCustomNamespaceIfDeclared(ProgramFile file, ref string ns)
        {
            foreach (AstStatement s in file.Statements)
            {
                if (s is AstNamespaceStmt nsStmt)
                {
                    ns = nsStmt.NameExpression.TryFlatten(MessageHandler, file);
                    file.Statements.Remove(s);
                    return;
                }
            }
        }

        public Scope GetNamespaceScope(string ns)
        {
            string scopeName = $"{ns}_scope";
            if (_nameSpaces.TryGetValue(scopeName, out var scope))
            {
                return scope;
            }
            _nameSpaces[scopeName] = new Scope(scopeName, GlobalScope);
            GlobalScope.DefineNamespaceSymbol(ns, _nameSpaces[scopeName]);
            return _nameSpaces[scopeName];
        }

        public ProgramFile GetFile(string v)
        {
            var normalizedPath = Path.GetFullPath(v).PathNormalize();
            if (!_files.ContainsKey(normalizedPath))
                return null;
            return _files[normalizedPath];
        }

        public ReadOnlyDictionary<string, ProgramFile> GetFiles()
        {
            return new ReadOnlyDictionary<string, ProgramFile>(_files);
        }

        public string GetText(ILocation location)
        {
            if (location is null)
                throw new ArgumentNullException(nameof(location));

            var normalizedPath = Path.GetFullPath(location.Beginning.File).PathNormalize();

            // files
            if (_files.TryGetValue(normalizedPath, out var f))
                return f.Text;

            return null;
        }
    }
}
