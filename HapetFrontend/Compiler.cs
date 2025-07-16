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

        public MetadataMetadataJson HandleExternalMetadata(string metadataText)
        {
            // just parsing metadata and adding its files into the compiler
            var files = ParseMetadata(metadataText, out var metadata);
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
            if (!CompilerUtils.ValidateFilePath("", fileName, false, MessageHandler, null, out string filePath))
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

        public List<ProgramFile> ParseMetadata(string metadataText, out MetadataMetadataJson metadata)
        {
            metadata = null;
            var lexer = Lexer.FromString(metadataText, MessageHandler, "metadata");

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
                    currentFile = new ProgramFile((dir.Value as AstStringExpr).StringValue, lexer.Text);
                    allFiles.Add(currentFile);
                    // change lexer locations' filename
                    lexer.ChangeFilename(currentFile.Name);
                    continue; // no need to add this shite
                }

                // should not happen
                if (currentFile == null)
                {
                    MessageHandler.ReportMessage(lexer.Text, s, [], ErrorCode.Get(CTEN.NullProgramFileInMeta));
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
                        HandleStatement(ss, currentFile, lexer);
                }
                else
                    HandleStatement(s, currentFile, lexer);
            }

            return allFiles;
        }

        private ProgramFile ParseFile(string fileName)
        {
            var lexer = Lexer.FromFile(fileName, MessageHandler);

            if (lexer == null)
                return null;

            var parser = new Parser(lexer, this, MessageHandler);

            var file = new ProgramFile(fileName, lexer.Text);
            _files[fileName] = file;

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
                        HandleStatement(ss, file, lexer);
                }
                else
                    HandleStatement(s, file, lexer);
            }

            string normalNamespace = CompilerUtils.GetNamespace(CurrentProjectSettings.ProjectPath, CurrentProjectSettings.RootNamespace, fileName);
            GetCustomNamespaceIfDeclared(file, ref normalNamespace); // will change the namespace if declared

            // generating namespace scope and doing some shite with it
            var nsScope = GetNamespaceScope(normalNamespace);
            file.NamespaceScope = nsScope;
            file.Namespace = normalNamespace;

            return file;
        }

        private void HandleStatement(AstStatement s, ProgramFile file, ILexer lexer)
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
                MessageHandler.ReportMessage(lexer.Text, s, [], ErrorCode.Get(CTEN.StmtNotAllowedInThis));
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
