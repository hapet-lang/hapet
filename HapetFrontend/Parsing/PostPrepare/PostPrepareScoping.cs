using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
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
						file.FileScope.DefineTypeSymbol(classDecl.Name.Name, classDecl.Type.OutType);
						PostPrepareClassScoping(classDecl);
					}
				}
			}
		}

		private void PostPrepareClassScoping(AstClassDecl classDecl)
		{
			classDecl.SourceFile = _currentSourceFile;
			var classScope = new Scoping.Scope($"{classDecl.Name.Name}_scope", classDecl.Scope);
			
			foreach (var decl in classDecl.Declarations)
			{
				decl.Scope = classScope;
				decl.Parent = classDecl;

				if (decl is AstFuncDecl funcDecl)
				{
					funcDecl.ContainingClass = classDecl;
					classDecl.Scope.DefineTypeSymbol(funcDecl.Name.Name, funcDecl.Type.OutType);
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
				var blockScope = PostPrepareBlock(funcDecl.Body, $"{funcDecl.Name.Name}_scope");
				// defining parameters in the func scope
				foreach (var p in funcDecl.Parameters)
				{
					blockScope.DefineDeclSymbol(p.Name.Name, p);
				}
			}
		}

		private static ulong _blockCounter = 0;
		private Scope PostPrepareBlock(AstBlockExpr blockStmt, string scopename = "")
		{
			if (string.IsNullOrWhiteSpace(scopename))
				scopename = $"block_{_blockCounter++}_scope";

			blockStmt.SourceFile = _currentSourceFile;
			var blockScope = new Scoping.Scope(scopename, blockStmt.Scope);

			foreach (var stmt in blockStmt.Statements)
			{
				stmt.Scope = blockScope;
				stmt.Parent = blockStmt;
				// todo: some check like if it is another block and etc.
			}

			return blockScope;
		}

		// TODO: recursively go through all of the statments and set Scope and Parent
	}
}
