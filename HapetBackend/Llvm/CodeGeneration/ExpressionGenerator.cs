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
		private LLVMValueRef GenerateExpressionCode(AstStatement expr, bool getPtr = false)
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
				// special case at least for 'for' loop
				// when 'for (int i = 0;...)' where 'int i' 
				// would not be handled by blockExpr
				case AstVarDecl varDecl: GenerateVarDeclCode(varDecl); return null;

				case AstBlockExpr blockExpr: return GenerateBlockExprCode(blockExpr);
				case AstBinaryExpr binExpr: return GenerateBinaryExprCode(binExpr);
				case AstPointerExpr pointerExpr: return GeneratePointerExprCode(pointerExpr);
				case AstAddressOfExpr addrExpr: return GenerateAddressOfExprCode(addrExpr);
				case AstIdExpr idExpr: return GenerateIdExpr(idExpr, getPtr);
				case AstNewExpr newExpr: return GenerateNewExpr(newExpr);
				case AstCallExpr callExpr: return GenerateCallExpr(callExpr);
				case AstArgumentExpr argExpr: return GenerateArgumentExpr(argExpr);
				case AstCastExpr castExpr: return GenerateCastExpr(castExpr);
				case AstNestedExpr nestExpr: return GenerateNestedExpr(nestExpr, getPtr);
				case AstArrayExpr arrayExpr: return GenerateArrayExprCode(arrayExpr);
				case AstArrayAccessExpr arrayAccessExpr: return GenerateArrayAccessExprCode(arrayAccessExpr, getPtr);

				// statements
				case AstAssignStmt assignStmt: GenerateAssignStmt(assignStmt); return null;
				case AstForStmt forStmt: GenerateForStmt(forStmt); return null;
				// TODO: check other expressions

				default:
				{
					_errorHandler.ReportError(_currentSourceFile.Text, expr, $"The expr {expr} is not implemented");
					return new LLVMValueRef();
				}
			}
		}

		private LLVMValueRef GenerateBlockExprCode(AstBlockExpr blockExpr)
		{
			LLVMValueRef result = null;
			foreach (var stmt in blockExpr.Statements)
			{
				if (stmt is AstReturnStmt returnStmt)
				{
					// TODO: also check if return expr is empty and method has to return smth - error it
					result = GenerateExpressionCode(returnStmt.ReturnExpression);
					break; // there is nothing to do in the block after return
				}
				else if (stmt is not null)
				{
					GenerateExpressionCode(stmt);
				}
			}
			return result;
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
					var leftExpr = (binExpr.Left as AstExpression);
					var left = GenerateExpressionCode(leftExpr);
					if (leftExpr.OutType != binExpr.OutType)
					{
						// TODO: this should not be here - move it into TypeInference with PostPrepareExpressionWithType
						// cast if they are not the same haha
						left = CreateCast(left, leftExpr.OutType, binExpr.OutType);
					}

					var rightExpr = (binExpr.Right as AstExpression);
					var right = GenerateExpressionCode(rightExpr);
					if (rightExpr.OutType != binExpr.OutType)
					{
						// TODO: this should not be here - move it into TypeInference with PostPrepareExpressionWithType
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
				// internal error here
				_errorHandler.ReportError(_currentSourceFile.Text, expr, $"Internal compiler error (AstPointerExpr could not be generated here)");
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
			// internal error here
			_errorHandler.ReportError(_currentSourceFile.Text, addrExpr, $"Internal compiler error (AstAddressOfExpr could not be generated here)");
			return null;
		}

		private LLVMValueRef GenerateIdExpr(AstIdExpr expr, bool getPtr = false)
		{
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

				// error if ctor not found
				if (ctorSymbol == null)
				{
					_errorHandler.ReportError(_currentSourceFile.Text, expr, $"Constructor with specified argument types was not found in the {classType.Declaration.Name.Name} class");
					return v;
				}

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
				return GenerateExpressionCode(expr.RightPart, getPtr);
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

		private LLVMValueRef _lastArraySizeValueRef = default;
		private LLVMValueRef GenerateArrayExprCode(AstArrayExpr expr)
		{
			// TODO: check if it could be allocated on stack

			// allocating memory for the array
			var mallocSymbol = expr.Scope.GetSymbol("malloc") as DeclSymbol; // TODO: rewrite it when there would be a default project of Hapet
			var mallocFunc = _valueMap[mallocSymbol];
			LLVMTypeRef funcType = _typeMap[mallocSymbol.Decl.Type.OutType];
			// calc size to malloc = amount * typeSize
			_lastArraySizeValueRef = GenerateExpressionCode(expr.SizeExpr);
			var typeSize = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(IntType.GetIntType(4, true)), (ulong)expr.TypeName.OutType.GetSize());
			var sizeToMalloc = _builder.BuildMul(_lastArraySizeValueRef, typeSize, "sizeToMalloc");

			var allocated = _builder.BuildCall2(funcType, mallocFunc, new LLVMValueRef[] { sizeToMalloc }, "allocatedForArray");

			for (int i = 0; i < expr.Elements.Count; ++i)
			{
				var el = expr.Elements[i];
				LLVMValueRef llvmElement = GenerateExpressionCode(el);

				var elementNum = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(IntType.GetIntType(4, true)), (ulong)i);
				var arrayEl = _builder.BuildGEP2(HapetTypeToLLVMType(expr.TypeName.OutType), allocated, new LLVMValueRef[] { elementNum }, $"element{i}");
				_builder.BuildStore(llvmElement, arrayEl);
			}

			if (expr.Elements.Count == 0)
			{
				// TODO: create here a loop that loops SizeExpr times and inites with defaults!
			}

			return allocated;
		}

		private LLVMValueRef GenerateArrayAccessExprCode(AstArrayAccessExpr expr, bool getPtr = false)
		{
			if (expr.ParameterExpr.OutType is not IntType)
			{
				// error here? i cannot access array if it is not an int type
				_errorHandler.ReportError(_currentSourceFile.Text, expr.ParameterExpr, $"Type of the index has to be an integer type");
			}

			// for now they are identical
			if (expr.ObjectName.OutType is ArrayType || expr.ObjectName.OutType is StringType)
			{
				// getting arrayBuf from struct and pointer to it
				LLVMValueRef ptrToArray = GenerateExpressionCode(expr.ObjectName, true);
				var ptrToBuffer = _builder.BuildStructGEP2(HapetTypeToLLVMType(expr.ObjectName.OutType), ptrToArray, 1, "arrayBuf");
				var bufferItself = _builder.BuildLoad2(HapetTypeToLLVMType(expr.OutType).GetPointerTo(), ptrToBuffer);

				// getting an element from the arrayBuf
				LLVMValueRef llvmElementIndex = GenerateExpressionCode(expr.ParameterExpr);
				var arrayEl = _builder.BuildGEP2(HapetTypeToLLVMType(expr.OutType), bufferItself, new LLVMValueRef[] { llvmElementIndex });

				if (getPtr)
					return arrayEl;

				var retLoaded = _builder.BuildLoad2(HapetTypeToLLVMType(expr.OutType), arrayEl);
				return retLoaded;
			}

			_errorHandler.ReportError(_currentSourceFile.Text, expr, $"Could not generate access code for the {expr.ObjectName.OutType} type");
			return null;
		}

		// statements
		private void GenerateAssignStmt(AstAssignStmt assignStmt)
		{
			LLVMValueRef theVar = GenerateNestedExpr(assignStmt.Target, true);

			// check for initializer
			if (assignStmt.Value == null)
			{
				// error here!!!!! it could not be null
				_errorHandler.ReportError(_currentSourceFile.Text, assignStmt, $"Expression expected on the right side of assignment");
			}

			AssignToVar(theVar, assignStmt.Target.OutType, assignStmt.Value);

			// TODO: WARN: always returns null because Assign is a stmt and does not returns anything. could be changed to expr
			// so stmts like 'a = (b = 3);' would be allowed...
		}

		private void GenerateForStmt(AstForStmt forStmt)
		{
			if (forStmt.FirstParam != null)
				GenerateExpressionCode(forStmt.FirstParam);

			var bbCond = _lastFunctionValueRef.AppendBasicBlock("for.cond");
			var bbBody = _lastFunctionValueRef.AppendBasicBlock("for.body");
			var bbInc = _lastFunctionValueRef.AppendBasicBlock("for.inc");
			var bbEnd = _lastFunctionValueRef.AppendBasicBlock("for.end");

			_builder.BuildBr(bbCond);
			_builder.PositionAtEnd(bbCond);

			// condition
			if (forStmt.SecondParam != null)
			{
				// building the condition
				var cmp = GenerateExpressionCode(forStmt.SecondParam);
				_builder.BuildCondBr(cmp, bbBody, bbEnd);
			}
			else
			{
				// if the second param is null - just move to the body block
				_builder.BuildBr(bbBody);
			}
			_builder.PositionAtEnd(bbBody);

			// body
			if (forStmt.Body != null)
			{
				// generating body code
				GenerateExpressionCode(forStmt.Body);
			}
			_builder.BuildBr(bbInc);
			_builder.PositionAtEnd(bbInc);

			// inc
			if (forStmt.ThirdParam != null)
			{
				// generating inc code
				GenerateExpressionCode(forStmt.ThirdParam);
			}
			_builder.BuildBr(bbCond);
			_builder.PositionAtEnd(bbEnd);
		}
	}
}
