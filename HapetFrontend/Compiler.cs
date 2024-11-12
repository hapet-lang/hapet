using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Parsing.PostPrepare;
using HapetFrontend.Scoping;
using System.Collections.ObjectModel;
using System.Diagnostics;

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
		public static int AssemblyPointerSize { get; set; }
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

		private ProgramFile AddFile(string fileName)
		{
			if (!CompilerUtils.ValidateFilePath("", fileName, false, MessageHandler, null, out string filePath))
			{
				return null;
			}

			if (_files.ContainsKey(filePath))
			{
				return _files[filePath];
			}

			var file = ParseFile(filePath, MessageHandler);
			if (file == null)
				return null;

			return file;
		}

		private ProgramFile ParseFile(string fileName, IMessageHandler mh)
		{
			var lexer = Lexer.FromFile(fileName, mh);

			if (lexer == null)
				return null;

			var parser = new Parser(lexer, mh);

			var file = new ProgramFile(fileName, lexer.Text);
			_files[fileName] = file;

			// the list is to handle attributes
			List<AstAttributeStmt> foundAttributes = new List<AstAttributeStmt>();
			while (true)
			{
				var s = parser.ParseStatement();
				if (s == null)
					break;

				HandleStatement(s);
			}

			string normalNamespace = CompilerUtils.GetNamespace(CurrentProjectSettings.ProjectPath, CurrentProjectSettings.RootNamespace, fileName);
			GetCustomNamespaceIfDeclared(file, ref normalNamespace); // will change the namespace if declared

			// generating namespace scope and doing some shite with it
			var nsScope = GetNamespaceScope(normalNamespace);
			file.NamespaceScope = nsScope;
			file.Namespace = normalNamespace;

			return file;

			void HandleStatement(AstStatement s)
			{
				if (s is AstEnumDecl ||
					s is AstStructDecl ||
					s is AstClassDecl ||
					s is AstDelegateDecl ||
					s is AstUsingStmt)
				{
					s.SourceFile = file;
					file.Statements.Add(s);

					// if it is a 'using' add it to the list
					if (s is AstUsingStmt usng)
						file.Usings.Add(usng);

					// add previously found attributes into the declaration
					if (s is AstDeclaration decl)
						decl.Attributes.AddRange(foundAttributes);

					// clear attributes
					foundAttributes.Clear();
				}
				else if (s is AstAttributeStmt attr)
				{
					// we found an attr - add it to list and use it when find a decl
					foundAttributes.Add(attr);
				}
				else if (s != null)
				{
					mh.ReportMessage(lexer.Text, s, "This type of statement is not allowed in global scope");
				}
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
