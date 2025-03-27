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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HapetFrontend
{
    public class Compiler : ITextOnLocationProvider
    {
        /// <summary>
        /// Path and file
        /// </summary>
        private Dictionary<string, ProgramFile> _files = new Dictionary<string, ProgramFile>();

        /// <summary>
        /// All the namespaces in the project
        /// </summary>
        private Dictionary<string, Scope> _nameSpaces = new Dictionary<string, Scope>();

        public IMessageHandler MessageHandler { get; }
        public CompilerSettings CurrentProjectSettings { get; }
        public Stopwatch CompilationStopwatch { get; set; }

        public Scope GlobalScope { get; private set; }

        /// <summary>
        /// The main function like an entry point of a program
        /// </summary>
        public AstFuncDecl MainFunction { get; set; }

        public Compiler(CompilerSettings projectSettings, IMessageHandler messageHandler)
        {
            CurrentProjectSettings = projectSettings;
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

        public List<ProgramFile> ParseMetadata(string metadataText)
        {
            var lexer = Lexer.FromString(metadataText, MessageHandler, "metadata");

            if (lexer == null)
                return null;

            var parser = new Parser(lexer, MessageHandler);

            List<ProgramFile> allFiles = new List<ProgramFile>();
            ProgramFile currentFile = null;

            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;

            // parsing metadata
            inInfo.ExternalMetadata = true;

            while (true)
            {
                var s = parser.ParseTopLevel(inInfo, ref outInfo);
                if (s == null)
                    break;

                // create a virtual file of the directive
                if (s is AstDirectiveStmt dir && dir.DirectiveType == Enums.DirectiveType.MetadataFile)
                {
                    // creating a virtual file
                    currentFile = new ProgramFile((dir.RightPart as AstStringExpr).StringValue, lexer.Text);

                    // parse namespace directive
                    s = parser.ParseStatement(inInfo, ref outInfo);
                    if (s is not AstDirectiveStmt dirNs || dirNs.DirectiveType != Enums.DirectiveType.MetadataNamespace)
                    {
                        // TODO: error
                        continue;
                    }

                    // generating namespace scope and doing some shite with it
                    string normalNamespace = (dirNs.RightPart as AstStringExpr).StringValue;
                    var nsScope = GetNamespaceScope(normalNamespace);
                    currentFile.NamespaceScope = nsScope;
                    currentFile.Namespace = normalNamespace;
                    currentFile.IsImported = true;

                    allFiles.Add(currentFile);
                    // change lexer locations' filename
                    lexer.ChangeFilename(currentFile.Name);
                    continue; // no need to add this shite
                }

                HandleStatement(s, currentFile, lexer);
            }

            return allFiles;
        }

        private ProgramFile ParseFile(string fileName)
        {
            var lexer = Lexer.FromFile(fileName, MessageHandler);

            if (lexer == null)
                return null;

            var parser = new Parser(lexer, MessageHandler);

            var file = new ProgramFile(fileName, lexer.Text);
            _files[fileName] = file;

            // just handlers
            ParserInInfo inInfo = ParserInInfo.Default;
            ParserOutInfo outInfo = ParserOutInfo.Default;
            while (true)
            {
                var s = parser.ParseTopLevel(inInfo, ref outInfo);
                if (s == null)
                    break;

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
                MessageHandler.ReportMessage(lexer.Text, s, [], ErrorCode.Get(CTEN.StmtNotAllowedInGlobal));
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
