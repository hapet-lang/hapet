using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Scoping;

namespace HapetFrontend.Parsing.PostPrepare
{
    public partial class PostPrepare
	{
		private void PostPrepareScoping()
		{
			foreach (var (path, file) in _compiler.GetFiles())
			{
				_currentSourceFile = file;
				foreach (var stmt in file.Statements)
				{
					if (stmt is AstClassDecl classDecl)
					{
						file.FileScope.DefineDeclSymbol(classDecl.Name.Name, classDecl);
						PostPrepareClassScoping(classDecl);
					}
					else if (stmt is AstFuncDecl funcDecl)
					{
						// usually extern funcs
						file.FileScope.DefineDeclSymbol(funcDecl.Name.Name, funcDecl);
						PostPrepareFunctionScoping(funcDecl);
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

					// if it is public func - it should be visible in the scope in which func's class is
					if (funcDecl.SpecialKeys.Contains(Parsing.TokenType.KwPublic)) // TODO: not only public
						classDecl.Scope.Parent.DefineDeclSymbol(funcDecl.Name.Name, funcDecl);
					else
						classDecl.Scope.DefineDeclSymbol(funcDecl.Name.Name, funcDecl);

					PostPrepareFunctionScoping(funcDecl);
				}
				else if (decl is AstVarDecl fieldDecl) // field or property
				{
					fieldDecl.ContainingClass = classDecl;

					// if it is public field/property - it should be visible in the scope in which var's class is
					if (fieldDecl.SpecialKeys.Contains(Parsing.TokenType.KwPublic)) // TODO: not only public
						classDecl.Scope.Parent.DefineDeclSymbol(fieldDecl.Name.Name, fieldDecl);
					else
						classDecl.Scope.DefineDeclSymbol(fieldDecl.Name.Name, fieldDecl);

					PostPrepareVarScoping(fieldDecl);
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
					p.Name.Scope = blockScope;
					p.Type.Scope = blockScope;
					PostPrepareExprScoping(p.Type);
					if (p.DefaultValue != null)
					{
						// preparing scopes of default values if they exist
						p.DefaultValue.Scope = blockScope;
						PostPrepareExprScoping(p.DefaultValue);
					}
					// defining the symbol in the scope so it can be easily found
					blockScope.DefineDeclSymbol(p.Name.Name, p);
				}
				// return type is the same
				funcDecl.Returns.Scope = funcDecl.Scope;
				funcDecl.Returns.Parent = funcDecl;
				PostPrepareExprScoping(funcDecl.Returns);
			}
			else
			{
				// defining parameters in the func scope
				foreach (var p in funcDecl.Parameters)
				{
					// settings the block scope to the parameters (so they are in the scope of the block)
					p.Scope = funcDecl.Scope;
					if (p.Name != null)
						p.Name.Scope = funcDecl.Scope;
					p.Type.Scope = funcDecl.Scope;
					PostPrepareExprScoping(p.Type);
					if (p.DefaultValue != null)
					{
						// preparing scopes of default values if they exist
						p.DefaultValue.Scope = funcDecl.Scope;
						PostPrepareExprScoping(p.DefaultValue);
					}
				}
				// return type is the same
				funcDecl.Returns.Scope = funcDecl.Scope;
				funcDecl.Returns.Parent = funcDecl;
				PostPrepareExprScoping(funcDecl.Returns);
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
				// preparing variable declaration parts scoping
				if (stmt is AstVarDecl varDecl) 
				{
					PostPrepareVarScoping(varDecl);
					blockScope.DefineDeclSymbol(varDecl.Name.Name, varDecl);
				}
				else if (stmt is AstReturnStmt returnStmt)
				{
					if (returnStmt.ReturnExpression != null)
					{
						returnStmt.ReturnExpression.Scope = blockScope;
						PostPrepareExprScoping(returnStmt.ReturnExpression);
					}
				}
				else if (stmt is AstExpression expr)
				{
					PostPrepareExprScoping(expr);
				}
				// todo: some check like if it is another block and etc.
			}

			return blockScope;
		}

		private void PostPrepareVarScoping(AstVarDecl varDecl)
		{
			varDecl.Name.Scope = varDecl.Scope;
			varDecl.Type.Scope = varDecl.Scope;
			PostPrepareExprScoping(varDecl.Type);
			if (varDecl.Initializer != null)
			{
				varDecl.Initializer.Scope = varDecl.Scope;
				PostPrepareExprScoping(varDecl.Initializer);
			}
		}

		private void PostPrepareExprScoping(AstExpression expr)
		{
			switch (expr)
			{
				case AstBinaryExpr binExpr:
					PostPrepareBinaryExprScoping(binExpr);
					break;
				case AstPointerExpr pointerExpr:
					PostPreparePointerExprScoping(pointerExpr);
					break;
				case AstNewExpr newExpr:
					PostPrepareNewExprScoping(newExpr);
					break;
				case AstArgumentExpr argumentExpr:
					PostPrepareArgumentExprScoping(argumentExpr);
					break;
				case AstIdExpr _:
					break;
				case AstCallExpr callExpr:
					PostPrepareCallExprScoping(callExpr);
					break;
				case AstCastExpr castExpr:
					PostPrepareCastExprScoping(castExpr);
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

		private void PostPreparePointerExprScoping(AstPointerExpr pointerExpr)
		{
			pointerExpr.SubExpression.Scope = pointerExpr.Scope;
			PostPrepareExprScoping(pointerExpr.SubExpression);
		}

		private void PostPrepareNewExprScoping(AstNewExpr newExpr)
		{
			newExpr.TypeName.Scope = newExpr.Scope;
			PostPrepareExprScoping(newExpr.TypeName);
			foreach (var a in newExpr.Arguments)
			{
				a.Scope = newExpr.Scope;
				PostPrepareExprScoping(a);
			}
		}

		private void PostPrepareArgumentExprScoping(AstArgumentExpr argumentExpr)
		{
			argumentExpr.Expr.Scope = argumentExpr.Scope;
			PostPrepareExprScoping(argumentExpr.Expr);
			if (argumentExpr.Name != null)
			{
				argumentExpr.Name.Scope = argumentExpr.Scope;
				PostPrepareExprScoping(argumentExpr.Name);
			}
		}

		private void PostPrepareCallExprScoping(AstCallExpr callExpr)
		{
			callExpr.TypeOrObjectName.Scope = callExpr.Scope;
			PostPrepareExprScoping(callExpr.TypeOrObjectName);
			callExpr.FuncName.Scope = callExpr.Scope;
			PostPrepareExprScoping(callExpr.FuncName);
			foreach (var a in callExpr.Arguments)
			{
				a.Scope = callExpr.Scope;
				PostPrepareExprScoping(a);
			}
		}

		private void PostPrepareCastExprScoping(AstCastExpr castExpr)
		{
			castExpr.SubExpression.Scope = castExpr.Scope;
			PostPrepareExprScoping(castExpr.SubExpression as AstExpression); // TODO: error if it is not an expr
			castExpr.TypeExpr.Scope = castExpr.Scope;
			PostPrepareExprScoping(castExpr.TypeExpr as AstExpression); // TODO: error if it is not an expr
		} 

		// TODO: recursively go through all of the statments and set Scope and Parent
	}
}
