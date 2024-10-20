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
				SetScopeAndParent(decl, classDecl, classScope);

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

					// setting already defined to 'true' because of some shite with access types
					PostPrepareVarScoping(fieldDecl, true);
				}
			}
		}

		private void PostPrepareFunctionScoping(AstFuncDecl funcDecl) 
		{
			_currentFunction = funcDecl;

			// TODO: refactor similar shite!
			funcDecl.SourceFile = _currentSourceFile;
			if (funcDecl.Body != null)
			{
				// body scope is the same
				SetScopeAndParent(funcDecl.Body, funcDecl);
				var blockScope = PostPrepareBlockScoping(funcDecl.Body, $"{funcDecl.Name.Name}_scope");
				// defining parameters in the func scope
				foreach (var p in funcDecl.Parameters)
				{
					// settings the block scope to the parameters (so they are in the scope of the block)
					SetScopeAndParent(p, funcDecl, blockScope);
					PostPrepareParamScoping(p);
				}
				// return type is the same
				SetScopeAndParent(funcDecl.Returns, funcDecl, blockScope);
				PostPrepareExprScoping(funcDecl.Returns);
			}
			else
			{
				// defining parameters in the func scope
				foreach (var p in funcDecl.Parameters)
				{
					// settings the block scope to the parameters (so they are in the scope of the block)
					// TODO: WARN!!!! do not set the scope the same as func scope because its params would be visible in class or smth
					// create an empty ast (?) and set its scope to params
					SetScopeAndParent(p, funcDecl);
					PostPrepareParamScoping(p);
				}
				// return type is the same
				SetScopeAndParent(funcDecl.Returns, funcDecl); // TODO: WARN!!! the same as above
				PostPrepareExprScoping(funcDecl.Returns);
			}
		}

		/// <summary>
		/// Post preparation of varDecl
		/// </summary>
		/// <param name="varDecl">The var decl</param>
		/// <param name="alreadyDefined">It could be already defined for example by classDecl (because of public/private shite)</param>
		private void PostPrepareVarScoping(AstVarDecl varDecl, bool alreadyDefined = false)
		{
			SetScopeAndParent(varDecl.Name, varDecl);
			SetScopeAndParent(varDecl.Type, varDecl);

			PostPrepareExprScoping(varDecl.Type);
			if (varDecl.Initializer != null)
			{
				SetScopeAndParent(varDecl.Initializer, varDecl);
				PostPrepareExprScoping(varDecl.Initializer);
			}
			// define it in the scope if it is not yet
			if (!alreadyDefined)
				varDecl.Scope.DefineDeclSymbol(varDecl.Name.Name, varDecl);
		}

		private void PostPrepareParamScoping(AstParamDecl paramDecl)
		{
			// it can be null when the func is only declared but not defined!
			if (paramDecl.Name != null)
				SetScopeAndParent(paramDecl.Name, paramDecl);
			SetScopeAndParent(paramDecl.Type, paramDecl);
			PostPrepareExprScoping(paramDecl.Type);
			if (paramDecl.DefaultValue != null)
			{
				// preparing scopes of default values if they exist
				SetScopeAndParent(paramDecl.DefaultValue, paramDecl);
				PostPrepareExprScoping(paramDecl.DefaultValue);
			}
			// it can be null when the func is only declared but not defined!
			if (paramDecl.Name != null)
			{
				// defining the symbol in the scope so it can be easily found
				paramDecl.Scope.DefineDeclSymbol(paramDecl.Name.Name, paramDecl);
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
				case AstBreakContStmt:
					// nothing to do
					break;
                case AstReturnStmt returnStmt:
                    PostPrepareReturnStmtScoping(returnStmt);
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
				if (stmt == null)
					continue;

                SetScopeAndParent(stmt, blockExpr, blockScope);
                PostPrepareExprScoping(stmt);
			}

			return blockScope;
		}

		private void PostPrepareUnaryExprScoping(AstUnaryExpr unExpr)
		{
			SetScopeAndParent(unExpr.SubExpr, unExpr);
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
			SetScopeAndParent(binExpr.Left, binExpr);
			SetScopeAndParent(binExpr.Right, binExpr);
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
			SetScopeAndParent(pointerExpr.SubExpression, pointerExpr);
			PostPrepareExprScoping(pointerExpr.SubExpression);
		}

		private void PostPrepareAddressOfExprScoping(AstAddressOfExpr addrExpr)
		{
			SetScopeAndParent(addrExpr.SubExpression, addrExpr);
			PostPrepareExprScoping(addrExpr.SubExpression);
		}

		private void PostPrepareNewExprScoping(AstNewExpr newExpr)
		{
			SetScopeAndParent(newExpr.TypeName, newExpr);
			PostPrepareExprScoping(newExpr.TypeName);
			foreach (var a in newExpr.Arguments)
			{
				SetScopeAndParent(a, newExpr);
				PostPrepareExprScoping(a);
			}
		}

		private void PostPrepareArgumentExprScoping(AstArgumentExpr argumentExpr)
		{
			SetScopeAndParent(argumentExpr.Expr, argumentExpr);
			PostPrepareExprScoping(argumentExpr.Expr);
			if (argumentExpr.Name != null)
			{
				SetScopeAndParent(argumentExpr.Name, argumentExpr);
				PostPrepareExprScoping(argumentExpr.Name);
			}
		}

		private void PostPrepareCallExprScoping(AstCallExpr callExpr)
		{
			SetScopeAndParent(callExpr.TypeOrObjectName, callExpr);
			PostPrepareExprScoping(callExpr.TypeOrObjectName);
			SetScopeAndParent(callExpr.FuncName, callExpr);
			PostPrepareExprScoping(callExpr.FuncName);
			foreach (var a in callExpr.Arguments)
			{
				SetScopeAndParent(a, callExpr);
				PostPrepareExprScoping(a);
			}
		}

		private void PostPrepareCastExprScoping(AstCastExpr castExpr)
		{
			SetScopeAndParent(castExpr.SubExpression, castExpr);
			// error if it is not an exprv
			if (castExpr.SubExpression is not AstExpression subExpr)
			{
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, castExpr.SubExpression, $"Expression expected");
				return;
			}
			PostPrepareExprScoping(subExpr);

			SetScopeAndParent(castExpr.TypeExpr, castExpr);
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
			SetScopeAndParent(nestExpr.RightPart, nestExpr);
			PostPrepareExprScoping(nestExpr.RightPart);
			if (nestExpr.LeftPart != null)
			{
				SetScopeAndParent(nestExpr.LeftPart, nestExpr);
				PostPrepareExprScoping(nestExpr.LeftPart);
			}
		}

		private void PostPrepareArrayExprScoping(AstArrayExpr arrayExpr)
		{
			SetScopeAndParent(arrayExpr.SizeExpr, arrayExpr);
			PostPrepareExprScoping(arrayExpr.SizeExpr);
			SetScopeAndParent(arrayExpr.TypeName, arrayExpr);
			PostPrepareExprScoping(arrayExpr.TypeName);
			foreach (var e in arrayExpr.Elements)
			{
				SetScopeAndParent(e, arrayExpr);
				PostPrepareExprScoping(e);
			}
		}

		private void PostPrepareArrayAccessExprScoping(AstArrayAccessExpr arrayAccExpr)
		{
			SetScopeAndParent(arrayAccExpr.ParameterExpr, arrayAccExpr);
			PostPrepareExprScoping(arrayAccExpr.ParameterExpr);
			SetScopeAndParent(arrayAccExpr.ObjectName, arrayAccExpr);
			PostPrepareExprScoping(arrayAccExpr.ObjectName);
		}

		// statements
		private void PostPrepareAssignStmtScoping(AstAssignStmt assignStmt)
		{
			SetScopeAndParent(assignStmt.Target, assignStmt);
			PostPrepareExprScoping(assignStmt.Target);
			if (assignStmt.Value != null)
			{
				SetScopeAndParent(assignStmt.Value, assignStmt);
				PostPrepareExprScoping(assignStmt.Value);
			}
		}

		private static ulong _forCounter = 0;
		private void PostPrepareForStmtScoping(AstForStmt forStmt)
		{
			SetScopeAndParent(forStmt.Body, forStmt);

			string scopename = $"for_{_forCounter++}_scope";
			var forScope = PostPrepareBlockScoping(forStmt.Body, scopename);

			if (forStmt.FirstParam != null)
			{
				SetScopeAndParent(forStmt.FirstParam, forStmt, forScope);
				PostPrepareExprScoping(forStmt.FirstParam);
			}
			if (forStmt.SecondParam != null)
			{
				SetScopeAndParent(forStmt.SecondParam, forStmt, forScope);
				PostPrepareExprScoping(forStmt.SecondParam);
			}
			if (forStmt.ThirdParam != null)
			{
				SetScopeAndParent(forStmt.ThirdParam, forStmt, forScope);
				PostPrepareExprScoping(forStmt.ThirdParam);
			}
		}

		private void PostPrepareReturnStmtScoping(AstReturnStmt returnStmt)
		{
            if (returnStmt.ReturnExpression != null)
            {
                SetScopeAndParent(returnStmt.ReturnExpression, returnStmt);
                PostPrepareExprScoping(returnStmt.ReturnExpression);
            }
        }

        // TODO: recursively go through all of the statments and set Scope and Parent

        /// <summary>
        /// Sets parent and scope to a child
        /// </summary>
        /// <param name="child">The child</param>
        /// <param name="parent">The parent</param>
        /// <param name="anotherScope">Scope to be set to a child. If null then parent scope is used</param>
        private void SetScopeAndParent(AstStatement child, AstStatement parent, Scope anotherScope = null)
		{
			anotherScope ??= parent.Scope;
			child.Scope = anotherScope;
			child.Parent = parent;
		}
	}
}
