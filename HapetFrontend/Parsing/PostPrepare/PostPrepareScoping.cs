using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Scoping;

namespace HapetFrontend.Parsing.PostPrepare
{
	public partial class PostPrepare
	{
		private ProgramFile _currentSourceFile;
		private void PostPrepareScoping()
		{
			foreach (var (path, file) in _compiler.GetFiles())
			{
				_currentSourceFile = file;
				foreach (var stmt in file.Statements)
				{
					if (stmt is AstClassDecl classDecl)
					{
						PostPrepareClassScoping(classDecl);
					}
				}
			}
		}

		private void PostPrepareClassScoping(AstClassDecl classDecl)
		{
			classDecl.SourceFile = _currentSourceFile;
			foreach (var decl in classDecl.Declarations)
			{
				decl.Scope = new Scoping.Scope($"{classDecl.Name.Name}_scope", classDecl.Scope);
				decl.Parent = classDecl;

				if (decl is AstFuncDecl funcDecl)
				{
					funcDecl.ContainingClass = classDecl;
					PostPrepareFunction(funcDecl);
				}				
			}
		}

		private void PostPrepareFunction(AstFuncDecl funcDecl) 
		{
			funcDecl.SourceFile = _currentSourceFile;
			if (funcDecl.Body != null)
			{
				// body scope is the same
				funcDecl.Body.Scope = funcDecl.Scope;
				funcDecl.Body.Parent = funcDecl;
				PostPrepareBlock(funcDecl.Body, $"{funcDecl.Name.Name}_scope");
			}
		}

		private static ulong _blockCounter = 0;
		private void PostPrepareBlock(AstBlockStmt blockStmt, string scopename = "")
		{
			if (string.IsNullOrWhiteSpace(scopename))
				scopename = $"block_{_blockCounter++}_scope";

			blockStmt.SourceFile = _currentSourceFile;

			foreach (var stmt in blockStmt.Statements)
			{
				stmt.Scope = new Scoping.Scope(scopename, blockStmt.Scope);
				stmt.Parent = blockStmt;
				// todo: some check like if it is another block and etc.
			}
		}

		// TODO: recursively go through all of the statments and set Scope and Parent
	}
}
