using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Scoping;
using HapetFrontend.Types;

namespace HapetFrontend.Parsing.PostPrepare
{
	public partial class PostPrepare
	{
		private void PostPrepareTypeInference()
		{
			foreach (var (path, file) in _compiler.GetFiles())
			{
				_currentSourceFile = file;
				foreach (var stmt in file.Statements)
				{
					if (stmt is AstClassDecl classDecl)
					{
						PostPrepareClassInference(classDecl);
					}
					else if (stmt is AstFuncDecl funcDecl)
					{
						// usually extern funcs
						PostPrepareFunctionInference(funcDecl);
					}
				}
			}
		}

		private void PostPrepareClassInference(AstClassDecl classDecl)
		{
			// infer fields and props at first
			foreach (var decl in classDecl.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
			{
				// field or property
				PostPrepareVarInference(decl);
			}
			foreach (var decl in classDecl.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl))
			{
				PostPrepareFunctionInference(decl);
			}
		}

		private void PostPrepareFunctionInference(AstFuncDecl funcDecl)
		{
			// inferencing parameters 
			foreach (var p in funcDecl.Parameters)
			{
				PostPrepareExprInference(p.Type);
				if (p.DefaultValue != null)
					PostPrepareExprInference(p.DefaultValue);
			}

			// if the containing class is empty - it is external func
			if (funcDecl.ContainingClass != null)
			{
				// renaming func name from 'Anime' to 'Anime(int, float)'
				string newName = funcDecl.Name.Name + funcDecl.Parameters.GetParamsString();
				// if it is public func - it should be visible in the scope in which func's class is
				if (funcDecl.SpecialKeys.Contains(Parsing.TokenType.KwPublic)) // TODO: not only public
					funcDecl.ContainingClass.Scope.Parent.DefineDeclSymbol(newName, funcDecl);
				else
					funcDecl.ContainingClass.Scope.DefineDeclSymbol(newName, funcDecl);
				funcDecl.Name = funcDecl.Name.GetCopy(newName);
			}

			// inferencing return type 
			{
				PostPrepareExprInference(funcDecl.Returns);
			}

			if (funcDecl.Body != null)
			{
				PostPrepareBlockInference(funcDecl.Body);
			}
		}

		private void PostPrepareBlockInference(AstBlockExpr blockExpr)
		{
			foreach (var stmt in blockExpr.Statements)
			{
				if (stmt is AstVarDecl varDecl)
				{
					PostPrepareVarInference(varDecl);
				}
				else if (stmt is AstReturnStmt returnStmt)
				{
					if (returnStmt.ReturnExpression != null)
						PostPrepareExprInference(returnStmt.ReturnExpression);
				}
				else if (stmt is AstStatement expr)
				{
					PostPrepareExprInference(expr);
				}
				// todo: some check like if it is another block and etc.
			}
		}

		private void PostPrepareVarInference(AstVarDecl varDecl)
		{
			PostPrepareExprInference(varDecl.Type);

			if (varDecl.Type.OutType is ClassType)
			{
				// the var is actually a pointer to the class
				var astPtr = new AstPointerExpr(varDecl.Type, false, varDecl.Type.Location);
				astPtr.Scope = varDecl.Type.Scope;
				varDecl.Type = astPtr;
				PostPrepareExprInference(varDecl.Type);
			}

			if (varDecl.Initializer != null)
			{
				if (varDecl.Initializer is AstDefaultExpr)
				{
					// get the default value for the type (no need to infer)
					varDecl.Initializer = AstDefaultExpr.GetDefaultValueForType(varDecl.Type.OutType, varDecl.Initializer);
					if (varDecl.Initializer == null)
						_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, varDecl, "Default value for the type was not found");
				}
				else
				{
					// if it is not a default
					PostPrepareExprInference(varDecl.Initializer);
				}
				PostPrepareVariableAssign(varDecl);
            }
		}

		private void PostPrepareExprInference(AstStatement expr)
		{
			switch (expr)
			{
				case AstBinaryExpr binExpr: 
					PostPrepareBinaryExprInference(binExpr);
					break;
				case AstPointerExpr pointerExpr:
					PostPreparePointerExprInference(pointerExpr);
					break;
				case AstNewExpr newExpr:
					PostPrepareNewExprInference(newExpr);
					break;
				case AstArgumentExpr argumentExpr:
					PostPrepareArgumentExprInference(argumentExpr);
					break;
				case AstIdExpr idExpr:
					PostPrepareIdentifierInference(idExpr);
					return;
				case AstCallExpr callExpr:
					PostPrepareCallExprInference(callExpr);
					break;
				case AstCastExpr castExpr:
					PostPrepareCastExprInference(castExpr);
					break;
				case AstNestedExpr nestExpr:
					PostPrepareNestedExprInference(nestExpr);
					break;
				case AstDefaultExpr defaultExpr:
					_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, defaultExpr, "(Inner exception) The default had to be infered previously by caller");
					break;

				// statements
				case AstAssignStmt assignStmt:
					PostPrepareAssignStmtInference(assignStmt);
					break;
				// TODO: check other expressions

				default:
					{
						// TODO: anything to do here?
						break;
					}
			}
		}

		private void PostPrepareBinaryExprInference(AstBinaryExpr binExpr)
		{
			// resolve the actual operator in the current scope
			PostPrepareExprInference(binExpr.Left as AstExpression);
			PostPrepareExprInference(binExpr.Right as AstExpression);
			var operators = binExpr.Scope.GetBinaryOperators(binExpr.Operator, (binExpr.Left as AstExpression).OutType, (binExpr.Right as AstExpression).OutType);
			if (operators.Count == 0)
			{
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, binExpr, $"Undefined operator {binExpr.Operator} for types {(binExpr.Left as AstExpression).OutType} and {(binExpr.Right as AstExpression).OutType}");
			}
			else if (operators.Count > 1)
			{
				// TODO: tell em where are the operators defined
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, binExpr, $"Too many operators {binExpr.Operator} defined for types {(binExpr.Left as AstExpression).OutType} and {(binExpr.Right as AstExpression).OutType}");
			}
			else
			{
				binExpr.ActualOperator = operators[0];
				binExpr.OutType = binExpr.ActualOperator.ResultType;
			}
		}

		private void PostPreparePointerExprInference(AstPointerExpr pointerExpr)
		{
			// prepare the right side
			PostPrepareExprInference(pointerExpr.SubExpression);
			// create a new pointer type from the right side and set the type to itself
			pointerExpr.OutType = PointerType.GetPointerType(pointerExpr.SubExpression.OutType);
		}

		private void PostPrepareNewExprInference(AstNewExpr newExpr)
		{
			// prepare the right side
			PostPrepareExprInference(newExpr.TypeName);
			// the type of newExpr is the same as the type of its name expr
			newExpr.OutType = newExpr.TypeName.OutType;

			foreach (var a in newExpr.Arguments)
			{
				PostPrepareExprInference(a);
			}
		}

		private void PostPrepareArgumentExprInference(AstArgumentExpr argumentExpr)
		{
			PostPrepareExprInference(argumentExpr.Expr);

			if (argumentExpr.Name != null)
			{
				PostPrepareExprInference(argumentExpr.Name);
			}

			// the argument type is the same as its expr type
			argumentExpr.OutType = argumentExpr.Expr.OutType;
		}

		private void PostPrepareIdentifierInference(AstIdExpr idExpr)
		{
			var smbl = idExpr.Scope.GetSymbol(idExpr.Name);
			if (smbl is DeclSymbol typed)
			{
				idExpr.OutType = typed.Decl.Type.OutType;
			}
			else
			{
				// TODO: really give them a error? or mb there is smth harder?
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, idExpr, "The type could not be infered...");
			}
		}

		private void PostPrepareCallExprInference(AstCallExpr callExpr)
		{
			// resolve the object on which func is called
			PostPrepareExprInference(callExpr.TypeOrObjectName);
			// resolve args
			foreach (var a in callExpr.Arguments)
			{
				PostPrepareExprInference(a);
			}

			// we need to manually check if the function is an external. 
			// if it is not - try to search it like an internal
			var smbl = callExpr.FuncName.Scope.GetSymbol(callExpr.FuncName.Name);
			if (smbl is DeclSymbol declTyped)
			{
				callExpr.FuncName.OutType = declTyped.Decl.Type.OutType;
			}
			else
			{
				// TODO: also callExpr.TypeOrObjectName could be checked to find out if the func is static or not
				// renaming func call name from 'Anime' to 'Anime(int, float)' WITH OBJECT AS FIRST PARAM
				string newName = callExpr.FuncName.Name + callExpr.Arguments.GetArgsString(callExpr.TypeOrObjectName.OutType);
				var smbl2 = callExpr.FuncName.Scope.GetSymbol(newName);
				if (smbl2 is DeclSymbol declTyped2)
				{
					// if it is a non static func
					callExpr.FuncName.OutType = declTyped2.Decl.Type.OutType;
					callExpr.FuncName = callExpr.FuncName.GetCopy(newName);
				}
				else
				{
					// probably static
					newName = callExpr.FuncName.Name + callExpr.Arguments.GetArgsString();
					callExpr.FuncName = callExpr.FuncName.GetCopy(newName);
					PostPrepareIdentifierInference(callExpr.FuncName);
				}
			}

			// setting parameters
			var sym = callExpr.Scope.GetSymbol(callExpr.FuncName.Name);
			if (sym is DeclSymbol typed && typed.Decl is AstFuncDecl funcDecl)
			{
				// it is probably non static fun
				callExpr.StaticCall = funcDecl.SpecialKeys.Contains(TokenType.KwStatic);
			}
			else
			{
				// TODO: error here
			}

			// setting call expr out type
			if (callExpr.FuncName.OutType is FunctionType funcType)
			{
				// call expr type is the same as func return type
				callExpr.OutType = funcType.Declaration.Returns.OutType;
			}
			else
			{
				// TODO: error here
			}
		}

		private void PostPrepareCastExprInference(AstCastExpr castExpr)
		{
			PostPrepareExprInference(castExpr.SubExpression as AstExpression);
			PostPrepareExprInference(castExpr.TypeExpr as AstExpression);
			castExpr.OutType = (castExpr.TypeExpr as AstExpression).OutType;
		}

		private void PostPrepareNestedExprInference(AstNestedExpr nestExpr)
		{
			if (nestExpr.LeftPart == null)
			{
				PostPrepareExprInference(nestExpr.RightPart);
				nestExpr.OutType = nestExpr.RightPart.OutType;
			}
			else
			{
				Scope leftSideScope = null;
				PostPrepareExprInference(nestExpr.LeftPart);
				if (nestExpr.LeftPart.OutType is PointerType ptr && ptr.TargetType is ClassType classT)
				{
					leftSideScope = classT.Declaration.Scope;
				}
				// TODO: structs and other

				if (leftSideScope == null)
				{
					_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, nestExpr.LeftPart, "The type of the expression has to be a class or a struct");
					return;
				}

				// here could only be an AstIdExpr because AstCallExpr and AstExpression would be in 'if' block upper
				if (nestExpr.RightPart is not AstIdExpr idExpr)
				{
					_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, nestExpr.RightPart, "The expressions has to be an identifier");
					return;
				}

				// searching for the symbol in the class/struct
				var smbl = leftSideScope.GetSymbol(idExpr.Name);
				if (smbl is DeclSymbol typed)
				{
					idExpr.OutType = typed.Decl.Type.OutType;
					nestExpr.OutType = idExpr.OutType;
				}
				else
				{
					_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, idExpr, $"The type could not be infered in {leftSideScope} scope...");
				}
			}
		}

		// statements
		private void PostPrepareAssignStmtInference(AstAssignStmt assignStmt)
		{
			PostPrepareExprInference(assignStmt.Target);

			if (assignStmt.Value != null)
			{
				if (assignStmt.Value is AstDefaultExpr)
				{
					// get the default value for the type (no need to infer)
					assignStmt.Value = AstDefaultExpr.GetDefaultValueForType(assignStmt.Target.OutType, assignStmt.Value);
					if (assignStmt.Value == null)
						_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, assignStmt, "Default value for the type was not found");
				}
				else
				{
					// if it is not a default
					PostPrepareExprInference(assignStmt.Value);
				}
                PostPrepareVariableAssign(assignStmt);
            }
		}
	}
}
