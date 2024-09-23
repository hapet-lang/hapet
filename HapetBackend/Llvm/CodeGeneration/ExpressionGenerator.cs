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
		private LLVMValueRef GenerateExpressionCode(AstExpression expr, LLVMBasicBlockRef basicBlock, bool deref = false)
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
				case AstBinaryExpr binExpr: return GenerateBinaryExprCode(binExpr, basicBlock);
				case AstIdExpr idExpr: return GenerateIdExpr(idExpr, deref);
				case AstNewExpr newExpr: return GenerateNewExpr(newExpr, basicBlock);
				// TODO: check other expressions

				default:
				{
					_errorHandler.ReportError(_currentSourceFile.Text, expr, $"The expr {expr} is not implemented");
					return new LLVMValueRef();
				}
			}
		}

		private LLVMValueRef GenerateBinaryExprCode(AstBinaryExpr binExpr, LLVMBasicBlockRef basicBlock)
		{
			if (binExpr.ActualOperator is BuiltInBinaryOperator)
			{
				if (binExpr.Operator == "&&")
				{
					return GenerateAndExpr(binExpr, basicBlock);
				}
				else if (binExpr.Operator == "||")
				{
					return GenerateOrExpr(binExpr, basicBlock);
				}
				else
				{
					// TODO: check that left and right are really expressions and report error if not
					var left = GenerateExpressionCode(binExpr.Left as AstExpression, basicBlock);
					var right = GenerateExpressionCode(binExpr.Right as AstExpression, basicBlock);
					var bo = builtInOperators[(binExpr.Operator, (binExpr.Left as AstExpression).OutType)];
					var val = bo(_builder, left, right, "binOp");
					return val;
				} 
			}
			// TODO: check other operators
			_errorHandler.ReportError(_currentSourceFile.Text, binExpr, $"The expr {binExpr} is not implemented");
			return new LLVMValueRef();
		}

		private LLVMValueRef GenerateAndExpr(AstBinaryExpr bin, LLVMBasicBlockRef basicBlock)
		{
			// TODO: ... check it pls, idk what is going on here
			var result = CreateLocalVariable(BoolType.Instance, basicBlock);

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

		private LLVMValueRef GenerateOrExpr(AstBinaryExpr bin, LLVMBasicBlockRef basicBlock)
		{
			// TODO: ... check it pls, idk what is going on here
			var result = CreateLocalVariable(BoolType.Instance, basicBlock);

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
			LLVMValueRef v = default;
			if (expr.Symbol is DeclSymbol declSymbol)
			{
				v = _valueMap[declSymbol.Decl.Type.OutType];

				// deref it because in valueMap it is ptr
				// if (deref)
					v = _builder.BuildLoad2(_typeMap[declSymbol.Decl.Type.OutType], v, "");

				return v;
			}
			else if (expr.Symbol is ModuleSymbol moduleSymbol)
			{
				// TODO: do i need it or just add it to the scope in postprocess?
				// v = GenerateExpression(moduleSymbol, false);
			}
			else
			{
				// TODO: error here
			}

			return v;
		}

		private unsafe LLVMValueRef GenerateNewExpr(AstNewExpr expr, LLVMBasicBlockRef basicBlock)
		{
			LLVMValueRef v = default;
			if (expr.OutType is ClassType classType)
			{
				var hpt = classType.Declaration.Type.OutType;
				var tp = _typeMap[hpt];

				// all declarations except funcs
				// TODO: there could be not only vardecls?
				List<AstVarDecl> declarations = classType.Declaration.Declarations.Where(x => x is not AstFuncDecl).Select(x => x as AstVarDecl).ToList();
				LLVMValueRef undef = LLVM.GetUndef(tp);
				v = declarations.Aggregate(undef, (und, field) =>
				{
					return _builder.BuildInsertValue(und, GenerateExpressionCode(field.Initializer, basicBlock), 0); // TODO: offsets here
				});

				return v;
			}
			else
			{
				// TODO: other also could be created 
			}

			return v;
		}
	}
}
