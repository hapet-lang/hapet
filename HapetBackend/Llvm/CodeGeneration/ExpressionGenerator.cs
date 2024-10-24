using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using LLVMSharp.Interop;
using System.Text;

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
				case AstArrayCreateExpr arrayCreateExpr: return GenerateArrayCreateExprCode(arrayCreateExpr);
				case AstArrayAccessExpr arrayAccessExpr: return GenerateArrayAccessExprCode(arrayAccessExpr, getPtr);

				// statements
				case AstAssignStmt assignStmt: GenerateAssignStmt(assignStmt); return null;
				case AstForStmt forStmt: GenerateForStmt(forStmt); return null;
				case AstWhileStmt whileStmt: GenerateWhileStmt(whileStmt); return null;
				case AstIfStmt ifStmt: GenerateIfStmt(ifStmt); return null;
				case AstSwitchStmt switchStmt: GenerateSwitchStmt(switchStmt); return null;
				case AstBreakContStmt breakContStmt: GenerateBreakContStmt(breakContStmt); return null;
				case AstReturnStmt returnStmt: GenerateReturnStmt(returnStmt); return null;
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
				if (stmt == null)
					continue;

				GenerateExpressionCode(stmt);
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

					var rightExpr = (binExpr.Right as AstExpression);
					var right = GenerateExpressionCode(rightExpr);

					var bo = builtInBinOperators[(binExpr.Operator, leftExpr.OutType, rightExpr.OutType)];
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
				// if really has to be an AstIdExpr
				if (expr.RightPart is not AstIdExpr idExpr)
				{
                    _errorHandler.ReportError(_currentSourceFile.Text, expr.RightPart, $"The part of the expression has to be an identifier");
					return null;
                }

				// TODO: could be refactored :)
				// WARN: the same as in PostPrepareNestedExprInference
				uint elementIndex = 0;
                if (expr.LeftPart.OutType is PointerType ptr && ptr.TargetType is ClassType classT)
                {
					// TODO: check if it is a part of a module name :))))
					var leftPart = GenerateExpressionCode(expr.LeftPart);

					var fieldDecls = classT.Declaration.Declarations.Where(x => x is AstVarDecl).ToList();
					elementIndex = GetElementIndex(idExpr.Name, fieldDecls) + 1; // + 1 because the first element in class struct is its reflection data

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
				else if (expr.LeftPart.OutType is StructType structT)
				{
					// TODO: check if it is a part of a module name :))))
					var leftPart = GenerateExpressionCode(expr.LeftPart, true); // we have to get the ptr to it. because idk

					var fieldDecls = structT.Declaration.Declarations;
					elementIndex = GetElementIndex(idExpr.Name, fieldDecls);

					var tp = _typeMap[structT];
					var ret = _builder.BuildStructGEP2(tp, leftPart, elementIndex, idExpr.Name);
					// if we need ptr for the shite. usually used to store some values inside vars
					if (getPtr)
						return ret;
					// loading the field because it is not registered in _typeMap like a normal variable.
					// it should be ok for all types of the fields including classes and other shite
					var retLoaded = _builder.BuildLoad2(HapetTypeToLLVMType(idExpr.OutType), ret, $"{idExpr.Name}Loaded");
					return retLoaded;
				}
				else if (expr.LeftPart.OutType is ArrayType arrayT)
				{
					// TODO: check if it is a part of a module name :))))
					var leftPart = GenerateExpressionCode(expr.LeftPart, true); // we have to get the ptr to it. because idk

					var fieldDecls = AstArrayExpr.ArrayStruct.Declarations;
					elementIndex = GetElementIndex(idExpr.Name, fieldDecls);

					var tp = HapetTypeToLLVMType(arrayT);
					var ret = _builder.BuildStructGEP2(tp, leftPart, elementIndex, idExpr.Name);
					// if we need ptr for the shite. usually used to store some values inside vars
					if (getPtr)
						return ret;
					// loading the field because it is not registered in _typeMap like a normal variable.
					// it should be ok for all types of the fields including classes and other shite
					var retLoaded = _builder.BuildLoad2(HapetTypeToLLVMType(idExpr.OutType), ret, $"{idExpr.Name}Loaded");
					return retLoaded;
				}
				// TODO: strings and other
            }
            _errorHandler.ReportError(_currentSourceFile.Text, expr, $"The nested expr could not be generated, fatal :^( ");
			return null;
		}

		private uint GetElementIndex(string name, List<AstDeclaration> decls)
		{
			// search for the name in decl
			for (uint i = 0; i < decls.Count; ++i)
			{
				var decl = decls[(int)i];
				if (decl.Name.Name == name)
				{
					return i; // getting the field index
				}
			}
			return 0;
		}

		private LLVMValueRef GenerateArrayCreateExprCode(AstArrayCreateExpr expr)
		{
			// TODO: check if it could be allocated on stack

			var cloned = expr.Clone() as AstArrayCreateExpr;
			return GenerateArrayInternal(cloned);
		}

		private LLVMValueRef GenerateArrayAccessExprCode(AstArrayAccessExpr expr, bool getPtr = false)
		{
			if (expr.ParameterExpr.OutType is not IntType)
			{
				// error here? i cannot access array if it is not an int type
				_errorHandler.ReportError(_currentSourceFile.Text, expr.ParameterExpr, $"Type of the index has to be an integer type");
			}

			// the buffer to be indexed
			LLVMValueRef buffer = null;

			// for now they are identical
			if (expr.ObjectName.OutType is ArrayType || expr.ObjectName.OutType is StringType)
			{
				// getting arrayBuf from struct and pointer to it
				LLVMValueRef ptrToArray = GenerateExpressionCode(expr.ObjectName, true);
				var ptrToBuffer = _builder.BuildStructGEP2(HapetTypeToLLVMType(expr.ObjectName.OutType), ptrToArray, 1, "arrayBuf");
				buffer = _builder.BuildLoad2(HapetTypeToLLVMType(expr.OutType).GetPointerTo(), ptrToBuffer);
			}
			else if (expr.ObjectName.OutType is PointerType)
			{
				// getting pointer to the var
				LLVMValueRef ptrToPtr = GenerateExpressionCode(expr.ObjectName, true);
				buffer = _builder.BuildLoad2(HapetTypeToLLVMType(expr.ObjectName.OutType), ptrToPtr);
			}

			// if the gotten buffer is not null
			if (buffer != null)
			{
				// getting an element from the arrayBuf
				LLVMValueRef llvmElementIndex = GenerateExpressionCode(expr.ParameterExpr);
				var arrayEl = _builder.BuildGEP2(HapetTypeToLLVMType(expr.OutType), buffer, new LLVMValueRef[] { llvmElementIndex });

				if (getPtr)
					return arrayEl;

				var retLoaded = _builder.BuildLoad2(HapetTypeToLLVMType(expr.OutType), arrayEl);
				return retLoaded;
			}

			_errorHandler.ReportError(_currentSourceFile.Text, expr, $"Could not generate access code for the {expr.ObjectName.OutType} type");
			return null;
		}

		// statements
		private void GenerateAssignStmt(AstAssignStmt stmt)
		{
			LLVMValueRef theVar = GenerateNestedExpr(stmt.Target, true);

			// check for initializer
			if (stmt.Value == null)
			{
				// error here!!!!! it could not be null
				_errorHandler.ReportError(_currentSourceFile.Text, stmt, $"Expression expected on the right side of assignment");
			}

			AssignToVar(theVar, stmt.Target.OutType, stmt.Value);

			// TODO: WARN: always returns null because Assign is a stmt and does not returns anything. could be changed to expr
			// so stmts like 'a = (b = 3);' would be allowed...
		}

		private static ulong _forCounter = 0;
		// these blocks are needed for break and continue statements
		private LLVMBasicBlockRef _currentLoopInc = null;
		private LLVMBasicBlockRef _currentLoopEnd = null;
		private unsafe void GenerateForStmt(AstForStmt stmt)
		{
			// WARN: this strange code is not just for 'fun'
			// when creating nested 'for' loops it would be easier to read LLVM IR code with that shite
			// so for example if we have two nested 'for' loops it would look like this:
			// for1.cond: ...
			// for1.body: ...
			//   for2.cond: ...
			//   for2.body: ...
			//   for2.inc: ...
			//   for2.end: ...
			// for1.inc: ...
			// for1.end: ...

			_forCounter++;

			// saving previous blocks because of nesting
			var prevForInc = _currentLoopInc;
			var prevForEnd = _currentLoopEnd;

			if (stmt.FirstParam != null)
				GenerateExpressionCode(stmt.FirstParam);

			var bbCond = _lastFunctionValueRef.AppendBasicBlock($"for{_forCounter}.cond");
			var bbBody = _lastFunctionValueRef.AppendBasicBlock($"for{_forCounter}.body");

			// creating other blocks
			var bbInc = _context.CreateBasicBlock($"for{_forCounter}.inc");
			var bbEnd = _context.CreateBasicBlock($"for{_forCounter}.end");

			// directly br into loop condition
			_builder.BuildBr(bbCond);

			_currentLoopInc = bbInc;
			_currentLoopEnd = bbEnd;

			// condition
			_builder.PositionAtEnd(bbCond);
			if (stmt.SecondParam != null)
			{
				// building the condition
				var cmp = GenerateExpressionCode(stmt.SecondParam);
				_builder.BuildCondBr(cmp, bbBody, bbEnd);
			}
			else
			{
				// if the second param is null - just move to the body block
				_builder.BuildBr(bbBody);
			}

			// body
			_builder.PositionAtEnd(bbBody);
			if (stmt.Body != null)
			{
				// generating body code
				GenerateExpressionCode(stmt.Body);
			}

			// appending them sooner
			LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbInc);
			LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbEnd);

			
			if (stmt.Body != null && 
				stmt.Body.Statements.Count > 0 &&
				(stmt.Body.Statements.Last() is AstReturnStmt ||
				stmt.Body.Statements.Last() is AstBreakContStmt))
			{
				// if the last statement of the block is already
				// a return or break or continue then there is no
				// need to create our own!!!
				// so this case is empty
			}
			else
			{
				// setting br without condition into inc block from body block
				_builder.BuildBr(bbInc);
			}

			// inc
			_builder.PositionAtEnd(bbInc);
			if (stmt.ThirdParam != null)
			{
				// generating inc code
				GenerateExpressionCode(stmt.ThirdParam);
			}
			_builder.BuildBr(bbCond);
			_builder.PositionAtEnd(bbEnd);

			// restoring prev blocks
			_currentLoopInc = prevForInc;
			_currentLoopEnd = prevForEnd;
		}

        private static ulong _whileCounter = 0;
        private unsafe void GenerateWhileStmt(AstWhileStmt stmt)
		{
            // WARN: this strange code is not just for 'fun'
            // when creating nested 'while' loops it would be easier to read LLVM IR code with that shite
            // so for example if we have two nested 'while' loops it would look like this:
            // while1.cond: ...
            // while1.body: ...
            //   while2.cond: ...
            //   while2.body: ...
            //   while2.end: ...
            // while1.end: ...

            _whileCounter++;

            // saving previous blocks because of nesting
			// WARN: for 'while' loops there are no Inc block
			// so the Cond block is used directly
            var prevWhileInc = _currentLoopInc;
            var prevWhileEnd = _currentLoopEnd;

            var bbCond = _lastFunctionValueRef.AppendBasicBlock($"while{_whileCounter}.cond");
            var bbBody = _lastFunctionValueRef.AppendBasicBlock($"while{_whileCounter}.body");

            // creating other blocks
            var bbEnd = _context.CreateBasicBlock($"while{_whileCounter}.end");

            // directly br into loop condition
            _builder.BuildBr(bbCond);

            _currentLoopInc = bbCond; // check upper WARN
            _currentLoopEnd = bbEnd;

            // condition
            _builder.PositionAtEnd(bbCond);
            if (stmt.ConditionParam != null)
            {
                // building the condition
                var cmp = GenerateExpressionCode(stmt.ConditionParam);
                _builder.BuildCondBr(cmp, bbBody, bbEnd);
            }
            else
            {
                // if the second param is null (should not happen!!! - checked in Parsing) - just move to the body block
                _builder.BuildBr(bbBody);
            }

            // body
            _builder.PositionAtEnd(bbBody);
            if (stmt.Body != null)
            {
                // generating body code
                GenerateExpressionCode(stmt.Body);
            }

            if (stmt.Body != null &&
                stmt.Body.Statements.Count > 0 &&
                (stmt.Body.Statements.Last() is AstReturnStmt ||
                stmt.Body.Statements.Last() is AstBreakContStmt))
            {
                // if the last statement of the block is already
                // a return or break or continue then there is no
                // need to create our own!!!
                // so this case is empty
            }
            else
            {
                // setting br without condition into inc block from body block
                _builder.BuildBr(bbCond);
            }

			// appending them sooner
			LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbEnd);

			_builder.PositionAtEnd(bbEnd);

            // restoring prev blocks
            _currentLoopInc = prevWhileInc;
            _currentLoopEnd = prevWhileEnd;
        }

		private static ulong _ifCounter = 0;
		private unsafe void GenerateIfStmt(AstIfStmt stmt)
		{
			// WARN: this strange code is not just for 'fun'
			// when creating nested 'if' stmts it would be easier to read LLVM IR code with that shite
			// so for example if we have two nested 'if' stmts it would look like this:
			// if1.cond: ...
			// if1.body: ...
			//   if2.cond: ...
			//   if2.body: ...
			//   if2.end: ...
			// if1.else
			//	 ...
			// if1.end: ...

			_ifCounter++;

			var bbCond = _lastFunctionValueRef.AppendBasicBlock($"if{_ifCounter}.cond"); // TODO: WARN: cond block is redunant
			var bbBody = _lastFunctionValueRef.AppendBasicBlock($"if{_ifCounter}.body");

			// creating other blocks
			var bbElse = _context.CreateBasicBlock($"if{_ifCounter}.else");
			var bbEnd = _context.CreateBasicBlock($"if{_ifCounter}.end");

			// directly br into loop condition
			_builder.BuildBr(bbCond);

			// condition
			_builder.PositionAtEnd(bbCond);
			if (stmt.Condition != null)
			{
				// building the condition
				var cmp = GenerateExpressionCode(stmt.Condition);
				if (stmt.BodyFalse != null)
				{
					_builder.BuildCondBr(cmp, bbBody, bbElse);
				}
				else
				{
					// going directly to end block because there is no else block
					_builder.BuildCondBr(cmp, bbBody, bbEnd);
				}
			}
			else
			{
				// if the second param is null (should not happen!!! - checked in Parsing) - just move to the body block
				_builder.BuildBr(bbBody);
			}

			// body
			_builder.PositionAtEnd(bbBody);
			if (stmt.BodyTrue != null)
			{
				// generating body code
				GenerateExpressionCode(stmt.BodyTrue);
			}

			if (stmt.BodyTrue != null &&
				stmt.BodyTrue.Statements.Count > 0 &&
				(stmt.BodyTrue.Statements.Last() is AstReturnStmt))
			{
				// if the last statement of the block is already
				// a return then there is no
				// need to create our own!!!
				// so this case is empty
			}
			else
			{
				// setting br without condition into inc block from body block
				_builder.BuildBr(bbEnd);
			}

			// else
			if (stmt.BodyFalse != null)
			{
				LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbElse);
				_builder.PositionAtEnd(bbElse);
				// generating else code
				GenerateExpressionCode(stmt.BodyFalse);

				if (stmt.BodyFalse != null &&
					stmt.BodyFalse.Statements.Count > 0 &&
					(stmt.BodyFalse.Statements.Last() is AstReturnStmt))
				{
					// if the last statement of the block is already
					// a return then there is no
					// need to create our own!!!
					// so this case is empty
				}
				else
				{
					// setting br without condition into inc block from body block
					_builder.BuildBr(bbEnd);
				}
			}

			// appending them sooner
			LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbEnd);

			_builder.PositionAtEnd(bbEnd);
		}

		private static ulong _switchCounter = 0;
		private unsafe void GenerateSwitchStmt(AstSwitchStmt stmt)
		{
			_switchCounter++;

			// checking if there is a user defined default case
			bool userDefinedDefaultCase = stmt.Cases.Any(x => x.DefaultCase);

			var bbDefault = _context.CreateBasicBlock($"switch{_switchCounter}.default");
			var bbEnd = _context.CreateBasicBlock($"switch{_switchCounter}.end");

			var subExprOfSwitch = GenerateExpressionCode(stmt.SubExpression);
			// this cringe shite is because the default case always exists even if user has not defined it!!!
			var theSwitchValueRef = _builder.BuildSwitch(subExprOfSwitch, bbDefault, (uint)(userDefinedDefaultCase ? stmt.Cases.Count : stmt.Cases.Count + 1));

			// counter for the names of the cases
			int caseCounter = 0;

			// this list holds all the falling cases.
			// when the non-falling occured all the falling are also going to be prepared
			List<AstCaseStmt> fallingCases = new List<AstCaseStmt>();
			foreach (var cc in stmt.Cases)
			{
				// just wait for a normal case
				if (cc.FallingCase)
				{
					fallingCases.Add(cc);
					continue;
				}

				// creating a block for the case
				LLVMBasicBlockRef currBb;
				if (cc.DefaultCase)
				{
					LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbDefault);
					currBb = bbDefault;
				}
				else
				{
					currBb = _lastFunctionValueRef.AppendBasicBlock($"switch{_switchCounter}.case{caseCounter++}");
				}
				_builder.PositionAtEnd(currBb);

				// generating the block
				// TODO: the return value could be used for returnable switch-case exprs :))
				var _ = GenerateExpressionCode(cc.Body);
				_builder.BuildBr(bbEnd);

				// there is no pattern in default case
				if (!cc.DefaultCase)
				{
					// the pattern of the case
					var patt = GenerateExpressionCode(cc.Pattern);
					// creating the LLVM case 
					theSwitchValueRef.AddCase(patt, currBb);
				}

				// going through all the falling cases
				foreach (var fc in fallingCases)
				{
					// the pattern of the case
					var pattFc = GenerateExpressionCode(fc.Pattern);
					// creating the LLVM case 
					theSwitchValueRef.AddCase(pattFc, currBb);
				}
				// clear the falling cases
				fallingCases.Clear();
			}

			// if user has not been defined its 'default' case
			if (!userDefinedDefaultCase)
			{
				// just braking into end block
				LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbDefault);
				_builder.PositionAtEnd(bbDefault);
				_builder.BuildBr(bbEnd);
			}

			// the end block
			LLVM.AppendExistingBasicBlock(_lastFunctionValueRef, bbEnd);
			_builder.PositionAtEnd(bbEnd);
		}

		private void GenerateBreakContStmt(AstBreakContStmt stmt)
		{
			// just generating shite that jumps between blocks :)
			if (stmt.IsSwitchParent)
			{
				// TODO: for switch-case
			}
			else
			{
				if (stmt.IsBreak)
				{
					if (_currentLoopEnd == null)
					{
						_errorHandler.ReportError(_currentSourceFile.Text, stmt, $"Loop to break could not be found");
						return;
					}
					_builder.BuildBr(_currentLoopEnd);
				}
				else
				{
					if (_currentLoopInc == null)
					{
						_errorHandler.ReportError(_currentSourceFile.Text, stmt, $"Loop to continue could not be found");
						return;
					}
					_builder.BuildBr(_currentLoopInc);
				}
			}
		}

		private void GenerateReturnStmt(AstReturnStmt returnStmt)
		{
			// TODO: also check if return expr is empty and method has to return smth - error it
			LLVMValueRef result = null;
			if (returnStmt.ReturnExpression != null)
                result = GenerateExpressionCode(returnStmt.ReturnExpression);

            // return logics
            if (result != null)
            {
                // TODO: return value (what did i mean by this?? ahahaha)
                _builder.BuildRet(result);
            }
            else if (_currentFunction.Returns.OutType is VoidType)
            {
                // ret if void
                // PopStackTrace(); // TODO: stack trace
                _builder.BuildRetVoid();
            }
            else
            {
                // error because the func is not void but with a type return
                // but the 'return' statement was not found
                _errorHandler.ReportError(_currentSourceFile.Text, returnStmt, "The 'return' statement returns a type that does not match the type specified in the function declaration");
                _builder.BuildRetVoid();
            }
        }
    }
}
