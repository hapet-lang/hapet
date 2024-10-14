using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
	public partial class LlvmCodeGenerator
	{
		private LLVMValueRef GenerateExpressionCode(AstStatement expr)
		{
			// if the value already evaluated (usually literals or consts)
			if (expr is AstExpression realExpr && realExpr.OutValue != null)
			{
				var result = HapetValueToLLVMValue(realExpr.OutType, realExpr.OutValue);
				if (result.Handle.ToInt64() != 0)
					return result;
			}

			switch (expr)
			{
				case AstBinaryExpr binExpr: return GenerateBinaryExprCode(binExpr);
				case AstPointerExpr pointerExpr: return GeneratePointerExprCode(pointerExpr);
				case AstAddressOfExpr addrExpr: return GenerateAddressOfExprCode(addrExpr);
				case AstIdExpr idExpr: return GenerateIdExpr(idExpr);
				case AstNewExpr newExpr: return GenerateNewExpr(newExpr);
				case AstCallExpr callExpr: return GenerateCallExpr(callExpr);
				case AstArgumentExpr argExpr: return GenerateArgumentExpr(argExpr);
				case AstCastExpr castExpr: return GenerateCastExpr(castExpr);
				case AstNestedExpr nestExpr: return GenerateNestedExpr(nestExpr);
				case AstArrayExpr arrayExpr: return GenerateArrayExprCode(arrayExpr);

				// statements
				case AstAssignStmt assignStmt: return GenerateAssignStmt(assignStmt);
				// TODO: check other expressions

				default:
				{
					_errorHandler.ReportError(_currentSourceFile.Text, expr, $"The expr {expr} is not implemented");
					return new LLVMValueRef();
				}
			}
		}

		private LLVMValueRef GenerateBinaryExprCode(AstBinaryExpr binExpr)
		{
			if (binExpr.ActualOperator is BuiltInBinaryOperator)
			{
				if (binExpr.Operator == "&&")
				{
					return GenerateAndExpr(binExpr);
				}
				else if (binExpr.Operator == "||")
				{
					return GenerateOrExpr(binExpr);
				}
				else
				{
					// TODO: check that left and right are really expressions and report error if not
					var leftExpr = (binExpr.Left as AstExpression);
					var left = GenerateExpressionCode(leftExpr);
					if (leftExpr.OutType != binExpr.OutType)
					{
						// cast if they are not the same haha
						left = CreateCast(left, leftExpr.OutType, binExpr.OutType);
					}

					var rightExpr = (binExpr.Right as AstExpression);
					var right = GenerateExpressionCode(rightExpr);
					if (rightExpr.OutType != binExpr.OutType)
					{
						// cast if they are not the same haha
						right = CreateCast(right, rightExpr.OutType, binExpr.OutType);
					}

					var bo = builtInOperators[(binExpr.Operator, binExpr.OutType)];
					var val = bo(_builder, left, right, "binOp");
					return val;
				} 
			}
			// TODO: check other operators
			_errorHandler.ReportError(_currentSourceFile.Text, binExpr, $"The expr {binExpr} is not implemented");
			return new LLVMValueRef();
		}

		private LLVMValueRef GenerateAndExpr(AstBinaryExpr bin)
		{
			// TODO: ... check it pls, idk what is going on here
			// and use GenerateVarDeclCode
			var result = CreateLocalVariable(BoolType.Instance);

			//var bbRight = basicBlock.InsertBasicBlock("_and_right");
			//var bbEnd = basicBlock.InsertBasicBlock("_and_end");

			//var left = GenerateExpressionCode(bin.Left as AstExpression, basicBlock);
			//_builder.CreateStore(left, result);
			//_builder.CreateCondBr(builder.CreateLoad(result, ""), bbRight, bbEnd);

			//_builder.PositionAtEnd(bbRight);
			//var right = GenerateExpressionCode(bin.Right as AstExpression, bbRight);
			//_builder.CreateStore(right, result);
			//_builder.CreateBr(bbEnd);

			//_builder.PositionAtEnd(bbEnd);

			//result = _builder.BuildLoad2(result.TypeOf, result, "");
			return result;
		}

		private LLVMValueRef GenerateOrExpr(AstBinaryExpr bin)
		{
			// TODO: ... check it pls, idk what is going on here
			// and use GenerateVarDeclCode
			var result = CreateLocalVariable(BoolType.Instance);

			//var bbRight = LLVM.AppendBasicBlock(currentLLVMFunction, "_or_right");
			//var bbEnd = LLVM.AppendBasicBlock(currentLLVMFunction, "_or_end");

			//var left = GenerateExpression(bin.Left, true);
			//builder.CreateStore(left, result);
			//builder.CreateCondBr(builder.CreateLoad(result, ""), bbEnd, bbRight);

			//builder.PositionBuilderAtEnd(bbRight);
			//var right = GenerateExpression(bin.Right, true);
			//builder.CreateStore(right, result);
			//builder.CreateBr(bbEnd);

			//builder.PositionBuilderAtEnd(bbEnd);

			//result = builder.CreateLoad(result, "");
			return result;
		}

		private LLVMValueRef GeneratePointerExprCode(AstPointerExpr expr)
		{
			if (expr.IsDereference)
			{
				var theVar = GenerateExpressionCode(expr.SubExpression);
				var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(expr.SubExpression.OutType), theVar, $"derefed");
				return loaded;
			}
			else
			{
				// idk what to do here :_(
				// anyway it should not happen...
				// TODO: internal error here
			}
			return null;
		}

		private LLVMValueRef GenerateAddressOfExprCode(AstAddressOfExpr addrExpr)
		{
			// TODO: should be better. probably there won't be only AstNestedExpr or AstIdExpr but something else...
			if (addrExpr.SubExpression is AstNestedExpr nestExpr)
			{
				return GenerateNestedExpr(nestExpr, true);
			}
			else if (addrExpr.SubExpression is AstIdExpr idExpr)
			{
				return GenerateIdExpr(idExpr, true);
			}
			// TODO: internal error here
			return null;
		}

		private LLVMValueRef GenerateIdExpr(AstIdExpr expr, bool getPtr = false)
		{
			// TODO: check for AstNestedIdExpr
			LLVMValueRef v = default;
			v = _valueMap[expr.FindSymbol];
			// return the ptr to the val. used for AstAddressOf or storing values
			if (getPtr)
				return v;
			var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(expr.OutType), v, expr.Name);
			return loaded;
		}

		private unsafe LLVMValueRef GenerateNewExpr(AstNewExpr expr)
		{
			LLVMValueRef v = default;
			if (expr.OutType is ClassType classType)
			{
				// TODO: some shite with alignment here
				ulong structSize = 0;
				List<HapetType> structElements = _structTypeElementsMap[classType];
				foreach (var elem in structElements)
				{
					structSize += (ulong)elem.GetSize();
                }

				// allocating memory for struct
                var mallocSymbol = classType.Declaration.Scope.GetSymbol("malloc") as DeclSymbol; // TODO: rewrite it when there would be a default project of Hapet
				var mallocFunc = _valueMap[mallocSymbol];
				LLVMTypeRef funcType = _typeMap[mallocSymbol.Decl.Type.OutType];
				LLVMValueRef mallocSize = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(IntType.GetIntType(4, true)), structSize); 
				v = _builder.BuildCall2(funcType, mallocFunc, new LLVMValueRef[] { mallocSize }, "allocated");

				// other args
				List<LLVMValueRef> args = new List<LLVMValueRef>() { v };
				foreach (var a in expr.Arguments)
				{
					args.Add(GenerateExpressionCode(a));
				}

				var ctorName = $"{classType.Declaration.Name.Name}_ctor" + expr.Arguments.GetArgsString(PointerType.GetPointerType(classType));
				var ctorSymbol = classType.Declaration.Scope.GetSymbol(ctorName) as DeclSymbol;
				// TODO: error if ctor not found
				var ctorFunc = _valueMap[ctorSymbol];
				LLVMTypeRef ctorType = _typeMap[ctorSymbol.Decl.Type.OutType];
				_builder.BuildCall2(ctorType, ctorFunc, args.ToArray());  // calling ctor

				return v;
			}
			else
			{
				// TODO: other also could be created 
			}

			return v;
		}

		private unsafe LLVMValueRef GenerateCallExpr(AstCallExpr expr)
		{
			var hptType = expr.FuncName.OutType as HapetFrontend.Types.FunctionType;
			var hapetFunc = _valueMap[hptType.Declaration.GetSymbol];
			LLVMTypeRef funcType = _typeMap[expr.FuncName.OutType];

			// args shite
			List<LLVMValueRef> args = new List<LLVMValueRef>();
			if (!expr.StaticCall)
			{
				args.Add(GenerateExpressionCode(expr.TypeOrObjectName));
			}
			foreach (var a in expr.Arguments)
			{
				args.Add(GenerateExpressionCode(a));
			}

			// the return name has to be empty if ret value of func is void
			string funcRetName = "";
			if (hptType.Declaration.Returns.OutType is not VoidType)
				funcRetName = $"{expr.FuncName.Name}ReturnValue";

			return _builder.BuildCall2(funcType, hapetFunc, args.ToArray(), funcRetName);
		}

		private unsafe LLVMValueRef GenerateArgumentExpr(AstArgumentExpr expr)
		{
			// TODO: handle arg name and index
			return GenerateExpressionCode(expr.Expr);
		}

		private unsafe LLVMValueRef GenerateCastExpr(AstCastExpr expr)
		{
			var sub = GenerateExpressionCode(expr.SubExpression as AstExpression);
			return CreateCast(sub, (expr.SubExpression as AstExpression).OutType, expr.OutType);
		}

		private unsafe LLVMValueRef GenerateNestedExpr(AstNestedExpr expr, bool getPtr = false)
		{
			if (expr.LeftPart == null)
			{
				// func call, ident or pure expr
				return GenerateExpressionCode(expr.RightPart);
			}
			else
			{
                // TODO: check if it is a part of a module name :))))
                var leftPart = GenerateExpressionCode(expr.LeftPart);
				// if really has to be an AstIdExpr
				if (expr.RightPart is not AstIdExpr idExpr)
				{
                    _errorHandler.ReportError(_currentSourceFile.Text, expr.RightPart, $"The part of the expression has to be an identifier");
					return leftPart;
                }

				// WARN: the same as in PostPrepareNestedExprInference
				uint elementIndex = 0;
                if (expr.LeftPart.OutType is PointerType ptr && ptr.TargetType is ClassType classT)
                {
					var fieldDecls = classT.Declaration.Declarations.Where(x => x is AstVarDecl).ToList();
					// search for the name in decl
					for (uint i = 0; i < fieldDecls.Count; ++i)
					{
						var decl = fieldDecls[(int)i];
                        if (decl.Name.Name == idExpr.Name)
						{
							elementIndex = i + 1; // + 1 because the first element in class struct is its reflection data
							break;
                        }
					}

                    var tp = _typeMap[classT];
					var ret = _builder.BuildStructGEP2(tp, leftPart, elementIndex, idExpr.Name);
					// if we need ptr for the shite. usually used to store some values inside vars
					if (getPtr)
						return ret;
					// loading the field because it is not registered in _typeMap like a normal variable.
					// it should be ok for all types of the fields including classes and other shite
					var retLoaded = _builder.BuildLoad2(HapetTypeToLLVMType(idExpr.OutType), ret, $"{idExpr.Name}Loaded");
                    return retLoaded;
                }
				// TODO: structs and other
            }
            _errorHandler.ReportError(_currentSourceFile.Text, expr, $"The nested expr could not be generated, fatal :^( ");
			return null;
		}

		private LLVMValueRef GenerateArrayExprCode(AstArrayExpr arrayExpr)
		{
			// TODO: check if it could be allocated on stack

			// allocating memory for the array
			var mallocSymbol = arrayExpr.Scope.GetSymbol("malloc") as DeclSymbol; // TODO: rewrite it when there would be a default project of Hapet
			var mallocFunc = _valueMap[mallocSymbol];
			LLVMTypeRef funcType = _typeMap[mallocSymbol.Decl.Type.OutType];
			LLVMValueRef mallocSize = GenerateExpressionCode(arrayExpr.SizeExpr); // TODO: for now it is only it bytes
			var allocated = _builder.BuildCall2(funcType, mallocFunc, new LLVMValueRef[] { mallocSize }, "allocatedForArray");

			// TODO: handle initializer values or if they are empty - default on each element!!!

			return allocated;
		}

		// statements
		private LLVMValueRef GenerateAssignStmt(AstAssignStmt assignStmt)
		{
			LLVMValueRef theVar;
			if (assignStmt.Target.LeftPart == null)
			{
				// if it is just a local var
				// TODO: error if it is not an AstIdExpr
				theVar = GenerateIdExpr(assignStmt.Target.RightPart as AstIdExpr, true);
			}
			else
			{
				theVar = GenerateNestedExpr(assignStmt.Target, true);
			}

			// check for initializer
			if (assignStmt.Value == null)
			{
				// TODO: error here!!!!! it could not be null
			}

			var x = GenerateExpressionCode(assignStmt.Value);
			_builder.BuildStore(x, theVar);

			// WARN: always returns null because Assign is a stmt and does not returns anything. could be changed to expr
			// so stmts like 'a = (b = 3);' would be allowed...
			return null; 
		}
	}
}
