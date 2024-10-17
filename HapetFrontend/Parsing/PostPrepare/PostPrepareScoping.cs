using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Scoping;
using System.Security.Cryptography.X509Certificates;

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

                    /// defining in a scope is done in <see cref="PostPrepareFunctionInference"/>

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
			_currentFunction = funcDecl;

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

		private void PostPrepareExprScoping(AstStatement expr)
		{
			switch (expr)
			{
				// special case at least for 'for' loop
				// when 'for (int i = 0;...)' where 'int i' 
				// would not be handled by blockExpr
				case AstVarDecl varDecl:
					PostPrepareVarScoping(varDecl);
					break;

				case AstBlockExpr blockExpr:
					PostPrepareBlockScoping(blockExpr);
					break;
				case AstUnaryExpr unExpr:
					PostPrepareUnaryExprScoping(unExpr);
					break;
				case AstBinaryExpr binExpr:
					PostPrepareBinaryExprScoping(binExpr);
					break;
				case AstPointerExpr pointerExpr:
					PostPreparePointerExprScoping(pointerExpr);
					break;
				case AstAddressOfExpr addrExpr:
					PostPrepareAddressOfExprScoping(addrExpr);
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
				case AstNestedExpr nestExpr:
					PostPrepareNestedExprScoping(nestExpr);
					break;
				case AstDefaultExpr _:
					break;
				case AstArrayExpr arrayExpr:
					PostPrepareArrayExprScoping(arrayExpr);
					break;
				case AstArrayAccessExpr arrayAccExpr:
					PostPrepareArrayAccessExprScoping(arrayAccExpr);
					break;

				// statements
				case AstAssignStmt assignStmt:
					PostPrepareAssignStmtScoping(assignStmt);
					break;
				case AstForStmt forStmt:
					PostPrepareForStmtScoping(forStmt);
					break;
				// TODO: check other expressions

				default:
					{
						// TODO: anything to do here?
						break;
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
				else if (stmt is AstStatement expr)
				{
					PostPrepareExprScoping(expr);
				}
			}

			return blockScope;
		}

		private void PostPrepareUnaryExprScoping(AstUnaryExpr unExpr)
		{
			unExpr.SubExpr.Scope = unExpr.Scope;
			// error if it is not an expr
			if (unExpr.SubExpr is not AstExpression expr)
			{
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, unExpr.SubExpr, $"Expression expected after {unExpr.Operator}");
				return;
			}
			PostPrepareExprScoping(expr);
		}

		private void PostPrepareBinaryExprScoping(AstBinaryExpr binExpr)
		{
			// these scopes are probably the same for the bin expr parts
			binExpr.Left.Scope = binExpr.Scope;
			binExpr.Right.Scope = binExpr.Scope;
			// error if it is not an expr
			if (binExpr.Left is not AstExpression leftExpr)
			{
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, binExpr.Left, $"Expression expected before {binExpr.Operator}");
				return;
			}
			// error if it is not an expr
			if (binExpr.Right is not AstExpression rightExpr)
			{
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, binExpr.Right, $"Expression expected after {binExpr.Operator}");
				return;
			}
			PostPrepareExprScoping(leftExpr);
			PostPrepareExprScoping(rightExpr);
		}

		private void PostPreparePointerExprScoping(AstPointerExpr pointerExpr)
		{
			pointerExpr.SubExpression.Scope = pointerExpr.Scope;
			PostPrepareExprScoping(pointerExpr.SubExpression);
		}

		private void PostPrepareAddressOfExprScoping(AstAddressOfExpr addrExpr)
		{
			addrExpr.SubExpression.Scope = addrExpr.Scope;
			PostPrepareExprScoping(addrExpr.SubExpression);
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
			// error if it is not an exprv
			if (castExpr.SubExpression is not AstExpression subExpr)
			{
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, castExpr.SubExpression, $"Expression expected");
				return;
			}
			PostPrepareExprScoping(subExpr);
			castExpr.TypeExpr.Scope = castExpr.Scope;
			// error if it is not an expr
			if (castExpr.TypeExpr is not AstExpression typeExpr)
			{
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, castExpr.TypeExpr, $"Expression expected as a result type");
				return;
			}
			PostPrepareExprScoping(typeExpr);
		}

		private void PostPrepareNestedExprScoping(AstNestedExpr nestExpr)
		{
			nestExpr.RightPart.Scope = nestExpr.Scope;
			PostPrepareExprScoping(nestExpr.RightPart);
			if (nestExpr.LeftPart != null)
			{
				nestExpr.LeftPart.Scope = nestExpr.Scope;
				PostPrepareExprScoping(nestExpr.LeftPart);
			}
		}

		private void PostPrepareArrayExprScoping(AstArrayExpr arrayExpr)
		{
			arrayExpr.SizeExpr.Scope = arrayExpr.Scope;
			PostPrepareExprScoping(arrayExpr.SizeExpr);
			arrayExpr.TypeName.Scope = arrayExpr.Scope;
			PostPrepareExprScoping(arrayExpr.TypeName);
			foreach (var e in arrayExpr.Elements)
			{
				e.Scope = arrayExpr.Scope;
				PostPrepareExprScoping(e);
			}
		}

		private void PostPrepareArrayAccessExprScoping(AstArrayAccessExpr arrayAccExpr)
		{
			arrayAccExpr.ParameterExpr.Scope = arrayAccExpr.Scope;
			PostPrepareExprScoping(arrayAccExpr.ParameterExpr);
			arrayAccExpr.ObjectName.Scope = arrayAccExpr.Scope;
			PostPrepareExprScoping(arrayAccExpr.ObjectName);
		}

		// statements
		private void PostPrepareAssignStmtScoping(AstAssignStmt assignStmt)
		{
			assignStmt.Target.Scope = assignStmt.Scope;
			PostPrepareExprScoping(assignStmt.Target);
			if (assignStmt.Value != null)
			{
				assignStmt.Value.Scope = assignStmt.Scope;
				PostPrepareExprScoping(assignStmt.Value);
			}
		}

		private static ulong _forCounter = 0;
		private void PostPrepareForStmtScoping(AstForStmt forStmt)
		{
			forStmt.Body.Scope = forStmt.Scope;

			string scopename = $"for_{_forCounter++}_scope";
			var forScope = PostPrepareBlockScoping(forStmt.Body, scopename);

			if (forStmt.FirstParam != null)
			{
				forStmt.FirstParam.Scope = forScope;
				PostPrepareExprScoping(forStmt.FirstParam);
			}
			if (forStmt.SecondParam != null)
			{
				forStmt.SecondParam.Scope = forScope;
				PostPrepareExprScoping(forStmt.SecondParam);
			}
			if (forStmt.ThirdParam != null)
			{
				forStmt.ThirdParam.Scope = forScope;
				PostPrepareExprScoping(forStmt.ThirdParam);
			}
		}

		// TODO: recursively go through all of the statments and set Scope and Parent
	}
}
