using HapetFrontend.Ast;
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
					PostPrepareFunctionScoping(funcDecl);
				}				
			}
		}

		private void PostPrepareFunctionScoping(AstFuncDecl funcDecl) 
		{
			funcDecl.SourceFile = _currentSourceFile;
			if (funcDecl.Body != null)
			{
				// body scope is the same
				funcDecl.Body.Scope = funcDecl.Scope;
				funcDecl.Body.Parent = funcDecl;
				var blockScope = PostPrepareBlockScoping(funcDecl.Body, $"{funcDecl.Name.Name}_scope");
				// defining parameters in the func scope
				foreach (var p in funcDecl.Parameters)
				{
					// settings the block scope to the parameters (so they are in the scope of the block)
					p.Scope = blockScope;
					if (p.DefaultValue != null)
					{
						// preparing scopes of default values if they exist
						p.DefaultValue.Scope = blockScope;
						PostPrepareExprScoping(p.DefaultValue);
					}
					// defining the symbol in the scope so it can be easily found
					blockScope.DefineDeclSymbol(p.Name.Name, p);
				}
			}
		}

		private static ulong _blockCounter = 0;
		private Scope PostPrepareBlockScoping(AstBlockExpr blockExpr, string scopename = "")
		{
			if (string.IsNullOrWhiteSpace(scopename))
				scopename = $"block_{_blockCounter++}_scope";

			blockExpr.SourceFile = _currentSourceFile;
			var blockScope = new Scoping.Scope(scopename, blockExpr.Scope);

			foreach (var stmt in blockExpr.Statements)
			{
				stmt.Scope = blockScope;
				stmt.Parent = blockExpr;
				if (stmt is AstVarDecl varDecl) 
				{
					if (varDecl.Initializer != null)
					{
						varDecl.Initializer.Scope = blockScope;
						PostPrepareExprScoping(varDecl.Initializer);
					}
					blockScope.DefineDeclSymbol(varDecl.Name.Name, varDecl);
				}
				// todo: some check like if it is another block and etc.
			}

			return blockScope;
		}

		private void PostPrepareExprScoping(AstExpression expr)
		{
			switch (expr)
			{
				case AstBinaryExpr binExpr:
					PostPrepareBinaryExprScoping(binExpr);
					break;
				// TODO: check other expressions

				default:
					{
						// TODO: anything to do here?
						break;
					}
			}
		}

		private void PostPrepareBinaryExprScoping(AstBinaryExpr binExpr)
		{
			// these scopes are probably the same for the bin expr parts
			binExpr.Left.Scope = binExpr.Scope;
			binExpr.Right.Scope = binExpr.Scope;
			PostPrepareExprScoping(binExpr.Left as AstExpression);
			PostPrepareExprScoping(binExpr.Right as AstExpression);
		}

		// TODO: recursively go through all of the statments and set Scope and Parent
	}
}
