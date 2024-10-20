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
			_currentFunction = funcDecl;

			// inferencing parameters 
			foreach (var p in funcDecl.Parameters)
			{
				PostPrepareParamInference(p);
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

		private void PostPrepareParamInference(AstParamDecl paramDecl)
		{
			PostPrepareExprInference(paramDecl.Type);
			if (paramDecl.DefaultValue != null)
				PostPrepareExprInference(paramDecl.DefaultValue);
		}

		private void PostPrepareExprInference(AstStatement expr)
		{
			switch (expr)
			{
				// special case at least for 'for' loop
				// when 'for (int i = 0;...)' where 'int i' 
				// would not be handled by blockExpr
				case AstVarDecl varDecl:
					PostPrepareVarInference(varDecl);
					break;

				case AstBlockExpr blockExpr:
					PostPrepareBlockInference(blockExpr);
					break;
				case AstUnaryExpr unExpr:
					PostPrepareUnaryExprInference(unExpr);
					break;
				case AstBinaryExpr binExpr: 
					PostPrepareBinaryExprInference(binExpr);
					break;
				case AstPointerExpr pointerExpr:
					PostPreparePointerExprInference(pointerExpr);
					break;
				case AstAddressOfExpr addrExpr:
					PostPrepareAddressOfExprInference(addrExpr);
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
				case AstArrayExpr arrayExpr:
					PostPrepareArrayExprInference(arrayExpr);
					break;
				case AstArrayAccessExpr arrayAccExpr:
					PostPrepareArrayAccessExprInference(arrayAccExpr);
					break;

				// statements
				case AstAssignStmt assignStmt:
					PostPrepareAssignStmtInference(assignStmt);
					break;
				case AstForStmt forStmt:
					PostPrepareForStmtInference(forStmt);
					break;
                case AstWhileStmt whileStmt:
                    PostPrepareWhileStmtInference(whileStmt);
                    break;
                case AstBreakContStmt breakContStmt:
					PostPrepareBreakContStmtInference(breakContStmt);
					break;
                case AstReturnStmt returnStmt:
                    PostPrepareReturnStmtInference(returnStmt);
                    break;
                // TODO: check other expressions

                default:
					{
						// TODO: anything to do here?
						break;
					}
			}
		}

		private void PostPrepareBlockInference(AstBlockExpr blockExpr)
		{
			foreach (var stmt in blockExpr.Statements)
			{
				if (stmt == null)
					continue;

				PostPrepareExprInference(stmt);
			}
		}

		private void PostPrepareUnaryExprInference(AstUnaryExpr unExpr) 
		{
			// TODO: check for the right size for an existance value (compiletime evaluated) and do some shite (set unExpr OutValue)
			PostPrepareExprInference(unExpr.SubExpr as AstExpression);
			var operators = unExpr.Scope.GetUnaryOperators(unExpr.Operator, (unExpr.SubExpr as AstExpression).OutType);
			if (operators.Count == 0)
			{
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, unExpr, $"Undefined operator {unExpr.Operator} for type {(unExpr.SubExpr as AstExpression).OutType}");
			}
			else if (operators.Count > 1)
			{
				// TODO: tell em where are the operators defined
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, unExpr, $"Too many operators {unExpr.Operator} defined for type {(unExpr.SubExpr as AstExpression).OutType}");
			}
			else
			{
				unExpr.ActualOperator = operators[0];
				unExpr.OutType = unExpr.ActualOperator.ResultType;
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

				// making some type casts
				var leftExpr = (binExpr.Left as AstExpression);
				var rightExpr = (binExpr.Right as AstExpression);

				// creating cast to result type if it is not a bool expr
				if (leftExpr.OutType != binExpr.OutType && binExpr.OutType is not BoolType)
				{
					// cast if they are not the same haha
					binExpr.Left = PostPrepareExpressionWithType(binExpr.OutType, leftExpr);
				}
				// creating cast to result type if it is not a bool expr
				if (rightExpr.OutType != binExpr.OutType && binExpr.OutType is not BoolType)
				{
					// cast if they are not the same haha
					binExpr.Right = PostPrepareExpressionWithType(binExpr.OutType, rightExpr);
				}

				// creating cast to result type if it is a bool expr and left and right are not the same types
				if (rightExpr.OutType != leftExpr.OutType && binExpr.OutType is BoolType)
				{
					// cast if they are not the same haha
					HapetType castingType = HapetType.GetPreferredTypeOf(leftExpr.OutType, rightExpr.OutType, out bool tookLeft);
					// if the left type was taken then change the right expr
					if (tookLeft)
						binExpr.Right = PostPrepareExpressionWithType(castingType, rightExpr);
					else
						binExpr.Left = PostPrepareExpressionWithType(castingType, leftExpr);
				}
			}
		}

		private void PostPreparePointerExprInference(AstPointerExpr pointerExpr)
		{
			// prepare the right side
			PostPrepareExprInference(pointerExpr.SubExpression);
			// create a new pointer type from the right side and set the type to itself
			pointerExpr.OutType = PointerType.GetPointerType(pointerExpr.SubExpression.OutType);
		}

		private void PostPrepareAddressOfExprInference(AstAddressOfExpr addrExpr)
		{
			// prepare the right side
			PostPrepareExprInference(addrExpr.SubExpression);
			// create a new reference type from the right side and set the type to itself
			addrExpr.OutType = ReferenceType.GetRefType(addrExpr.SubExpression.OutType);
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
			string name = idExpr.Name;
			bool isArray = false;

			if (idExpr.Name.EndsWith("[]"))
			{
				// it is probably an array def
				name = name.Substring(0, name.Length - 2);
				isArray = true;
			}

			var smbl = idExpr.Scope.GetSymbol(name);
			if (smbl is DeclSymbol typed)
			{
				if (isArray)
					idExpr.OutType = ArrayType.GetArrayType(typed.Decl.Type.OutType);
				else
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
				// checking if it is a static func
				callExpr.StaticCall = funcDecl.SpecialKeys.Contains(TokenType.KwStatic);
			}
			else
			{
				// error here
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, callExpr, $"The function could not be found in the scope");
			}

			// setting call expr out type
			if (callExpr.FuncName.OutType is FunctionType funcType)
			{
				// call expr type is the same as func return type
				callExpr.OutType = funcType.Declaration.Returns.OutType;
			}
			else
			{
				// error here
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, callExpr, $"The calling thing has to be a function");
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

		private void PostPrepareArrayExprInference(AstArrayExpr arrayExpr)
		{
			PostPrepareExprInference(arrayExpr.SizeExpr);
			// TODO: you can check if the size is available at compile time and create the array on stack

			PostPrepareExprInference(arrayExpr.TypeName);

			for (int i = 0; i < arrayExpr.Elements.Count; ++i)
			{
				var e = arrayExpr.Elements[i];
				PostPrepareExprInference(e);
				// try to use implicit cast if it can be used
				arrayExpr.Elements[i] = PostPrepareExpressionWithType(arrayExpr.TypeName.OutType, e);
			}

			if (arrayExpr.Elements.Count > 0 && arrayExpr.SizeExpr.OutValue == null)
			{
				// expected a const value to be used when creating an array with elements
				// byte[] a2 = new byte[b] {1, b, 2, 4}; - would error in C#
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, arrayExpr, $"Array cannot has initialization values when its size is not a const");
			}
			else if (arrayExpr.Elements.Count > 0 && arrayExpr.SizeExpr.OutValue is NumberData numData && numData != arrayExpr.Elements.Count)
			{
				//  byte[] a2 = new byte[3] {1, 1, 2, 4}; - would error in C#
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, arrayExpr, $"Array initialization values amount and its size different but they haму to be the same");
			}

			arrayExpr.OutType = PointerType.GetPointerType(arrayExpr.TypeName.OutType);
		}

		private void PostPrepareArrayAccessExprInference(AstArrayAccessExpr arrayAccExpr)
		{
			PostPrepareExprInference(arrayAccExpr.ParameterExpr);
			PostPrepareExprInference(arrayAccExpr.ObjectName);

			HapetType outType = null;
			if (arrayAccExpr.ObjectName.OutType is ArrayType arrayType)
				outType = arrayType.TargetType;
			else if (arrayAccExpr.ObjectName.OutType is StringType)
				outType = CharType.DefaultType; // TODO: mb non default could be here? idk :)
			else
			{
				// error because expected an array 
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, arrayAccExpr.ObjectName, $"Array/String type expected to be indexed");
			}
			arrayAccExpr.OutType = outType;
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

		private void PostPrepareForStmtInference(AstForStmt forStmt)
		{
			if (forStmt.FirstParam != null)
				PostPrepareExprInference(forStmt.FirstParam);
			if (forStmt.SecondParam != null)
			{
				PostPrepareExprInference(forStmt.SecondParam);

				// error if it is not a bool type because it has to be
				if (forStmt.SecondParam.OutType is not BoolType)
				{
					_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, forStmt.SecondParam, "Type of the expression has to be boolean type");
				}
			}
			if (forStmt.ThirdParam != null)
				PostPrepareExprInference(forStmt.ThirdParam);

			PostPrepareExprInference(forStmt.Body);
		}

		private void PostPrepareWhileStmtInference(AstWhileStmt whileStmt)
		{
            if (whileStmt.ConditionParam != null)
            {
                PostPrepareExprInference(whileStmt.ConditionParam);

                // error if it is not a bool type because it has to be
                if (whileStmt.ConditionParam.OutType is not BoolType)
                {
                    _compiler.ErrorHandler.ReportError(_currentSourceFile.Text, whileStmt.ConditionParam, "Type of the expression has to be boolean type");
                }
            }

            PostPrepareExprInference(whileStmt.Body);
        }


        private void PostPrepareBreakContStmtInference(AstBreakContStmt breakContStmt)
		{
			// there is no inferences but just checks if it is in switch-case
			AstStatement currentParent = breakContStmt.NormalParent;
			// TODO: check if the breakContStmt is for switch-case via loop and error if there is nothing
			// TODO: also check if there is something after and warn! (add warnings to error handler?)
		}

		private void PostPrepareReturnStmtInference(AstReturnStmt returnStmt)
		{
            if (returnStmt.ReturnExpression != null)
            {
                PostPrepareExprInference(returnStmt.ReturnExpression);
                returnStmt.ReturnExpression = PostPrepareExpressionWithType(_currentFunction.Returns.OutType, returnStmt.ReturnExpression);
            }
			else if (returnStmt.ReturnExpression == null && _currentFunction.Returns.OutType is not VoidType)
			{
                _compiler.ErrorHandler.ReportError(_currentSourceFile.Text, returnStmt, $"Empty 'return' statement in function that has to return {_currentFunction.Returns.OutType}");
            }
        }
    }
}
