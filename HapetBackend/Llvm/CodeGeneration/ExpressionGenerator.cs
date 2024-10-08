using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
	public partial class LlvmCodeGenerator
	{
		private LLVMValueRef GenerateExpressionCode(AstExpression expr, bool deref = false)
		{
			// if the value already evaluated (usually literals or consts)
			if (expr.OutValue != null)
			{
				var result = HapetValueToLLVMValue(expr.OutType, expr.OutValue);
				if (result.Handle.ToInt64() != 0)
					return result;
			}

			switch (expr)
			{
				case AstBinaryExpr binExpr: return GenerateBinaryExprCode(binExpr);
				case AstIdExpr idExpr: return GenerateIdExpr(idExpr, deref);
				case AstNewExpr newExpr: return GenerateNewExpr(newExpr);
				case AstCallExpr callExpr: return GenerateCallExpr(callExpr);
				case AstArgumentExpr argExpr: return GenerateArgumentExpr(argExpr);
				case AstCastExpr castExpr: return GenerateCastExpr(castExpr);
				case AstNestedExpr nestExpr: return GenerateNestedExpr(nestExpr);
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

		private LLVMValueRef GenerateIdExpr(AstIdExpr expr, bool deref = false)
		{
			// TODO: check for AstNestedIdExpr
			LLVMValueRef v = default;
			v = _valueMap[expr.FindSymbol];
			return v;
		}

		private unsafe LLVMValueRef GenerateNewExpr(AstNewExpr expr)
		{
			LLVMValueRef v = default;
			if (expr.OutType is ClassType classType)
			{
				var hpt = classType.Declaration.Type.OutType;
				var tp = _typeMap[hpt];

				// all declarations except funcs
				// TODO: there could be not only vardecls?
				//List<AstVarDecl> declarations = classType.Declaration.Declarations.Where(x => x is not AstFuncDecl).Select(x => x as AstVarDecl).ToList();
				//LLVMValueRef undef = LLVM.GetUndef(tp);
				//v = declarations.Aggregate(undef, (und, field) =>
				//{
				//	return _builder.BuildInsertValue(und, GenerateExpressionCode(field.Initializer), 0); // TODO: offsets here
				//});
				var mallocSymbol = classType.Declaration.Scope.GetSymbol("malloc") as DeclSymbol;
				var mallocFunc = _valueMap[mallocSymbol];
				LLVMTypeRef funcType = _typeMap[mallocSymbol.Decl.Type.OutType];
				LLVMValueRef mallocSize = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(IntType.GetIntType(4, true)), 1); // TODO: replace 1 with class size
				v = _builder.BuildCall2(funcType, mallocFunc, new LLVMValueRef[] { mallocSize }, "allocated");

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

			return _builder.BuildCall2(funcType, hapetFunc, args.ToArray(), "funcReturnValue");
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

		private unsafe LLVMValueRef GenerateNestedExpr(AstNestedExpr expr)
		{
			if (expr.LeftPart == null)
			{
				// func call, ident or pure expr
				return GenerateExpressionCode(expr.RightPart);
			}
			else
			{
				// TODO: here you should prepare smth like few.dasd.ggg.ds etc.
			}
			_errorHandler.ReportError(_currentSourceFile.Text, expr, $"The nested expr could not be generated, fatal :^( ");
			return null;
		}
	}
}
