using HapetFrontend.Types;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
	public partial class LlvmCodeGenerator
	{
		private Dictionary<(string, HapetType, HapetType), Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef>> builtInBinOperators;

		private unsafe Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef> GetICompare(LLVMIntPredicate pred)
		{
			return (a, b, c, d) =>
			{
				using var marshaledName = new MarshaledString(d.AsSpan());
				return LLVM.BuildICmp(a, pred, b, c, marshaledName);
			};
		}

		private unsafe Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef> GetFCompare(LLVMRealPredicate pred)
		{
			return (a, b, c, d) =>
			{
				using var marshaledName = new MarshaledString(d.AsSpan());
				return LLVM.BuildFCmp(a, pred, b, c, marshaledName);
			};
		}

		private void InitOperators()
		{
			builtInBinOperators = new Dictionary<(string, HapetType, HapetType), Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef>>();
			var globalScope = _compiler.GlobalScope;
			var allBuiltInOperators = globalScope.GetBuiltInBinaryOperators();
			foreach (var op in allBuiltInOperators)
			{
				Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef> theFunc;
				switch (op.Name)
				{
					case "+":
						{
							// checking if the result type of the OP is float - then use FAdd
							if (op.ResultType is FloatType) theFunc = LlvmExtensions.BuildFAdd;
							else theFunc = LlvmExtensions.BuildAdd;
							break;
						}
					case "-":
						{
							// checking if the result type of the OP is float - then use FSub
							if (op.ResultType is FloatType) theFunc = LlvmExtensions.BuildFSub;
							else theFunc = LlvmExtensions.BuildSub;
							break;
						}
					case "*":
						{
							// checking if the result type of the OP is float - then use FMul
							if (op.ResultType is FloatType) theFunc = LlvmExtensions.BuildFMul;
							else theFunc = LlvmExtensions.BuildMul;
							break;
						}
					case "/":
						{
							// checking if the result type of the OP is float - then use FDiv
							if (op.ResultType is FloatType) theFunc = LlvmExtensions.BuildFDiv;
							else if (op.ResultType is IntType intType && intType.Signed) theFunc = LlvmExtensions.BuildSDiv;
							else theFunc = LlvmExtensions.BuildUDiv; // here is also char type, so it is ok
							break;
						}
					case "%":
						{
							// checking if the result type of the OP is float - then use FRem
							if (op.ResultType is FloatType) theFunc = LlvmExtensions.BuildFRem;
							else if (op.ResultType is IntType intType && intType.Signed) theFunc = LlvmExtensions.BuildSRem;
							else theFunc = LlvmExtensions.BuildURem; // here is also char type, so it is ok
							break;
						}
					case "&":
						{
							// checking if the result type of the OP is float - set null
							if (op.ResultType is IntType || op.ResultType is CharType) theFunc = LlvmExtensions.BuildAnd;
							else theFunc = null;
							break;
						}
					case "|":
						{
							// checking if the result type of the OP is float - set null
							if (op.ResultType is IntType || op.ResultType is CharType) theFunc = LlvmExtensions.BuildOr;
							else theFunc = null;
							break;
						}
					case ">>":
						{
							// checking if the result type of the OP is float - set null
							if (op.ResultType is IntType || op.ResultType is CharType) theFunc = LlvmExtensions.BuildRShift;
							else theFunc = null;
							break;
						}
					case "<<":
						{
							// checking if the result type of the OP is float - set null
							if (op.ResultType is IntType || op.ResultType is CharType) theFunc = LlvmExtensions.BuildLShift;
							else theFunc = null;
							break;
						}
					case "==":
						{
							// checking if the result type of the OP is float
							if (op.ResultType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealOEQ);
							else theFunc = GetICompare(LLVMIntPredicate.LLVMIntEQ);
							break;
						}
					case "!=":
						{
							// checking if the result type of the OP is float
							if (op.ResultType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealONE);
							else theFunc = GetICompare(LLVMIntPredicate.LLVMIntNE);
							break;
						}
					case "<":
						{
							// checking if the result type of the OP is float
							if (op.ResultType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealOLT);
							else if (op.ResultType is IntType intType && intType.Signed) theFunc = GetICompare(LLVMIntPredicate.LLVMIntSLT);
							else theFunc = GetICompare(LLVMIntPredicate.LLVMIntULT); // here is also char type, so it is ok
							break;
						}
					case "<=":
						{
							// checking if the result type of the OP is float
							if (op.ResultType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealOLE);
							else if (op.ResultType is IntType intType && intType.Signed) theFunc = GetICompare(LLVMIntPredicate.LLVMIntSLE);
							else theFunc = GetICompare(LLVMIntPredicate.LLVMIntULE); // here is also char type, so it is ok
							break;
						}
					case ">":
						{
							// checking if the result type of the OP is float
							if (op.ResultType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealOGT);
							else if (op.ResultType is IntType intType && intType.Signed) theFunc = GetICompare(LLVMIntPredicate.LLVMIntSGT);
							else theFunc = GetICompare(LLVMIntPredicate.LLVMIntUGT); // here is also char type, so it is ok
							break;
						}
					case ">=":
						{
							// checking if the result type of the OP is float
							if (op.ResultType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealOGE);
							else if (op.ResultType is IntType intType && intType.Signed) theFunc = GetICompare(LLVMIntPredicate.LLVMIntSGE);
							else theFunc = GetICompare(LLVMIntPredicate.LLVMIntUGE); // here is also char type, so it is ok
							break;
						}
					// && and || are not checked here
					default:
						{
							// error here (internal compiler error, should not happen) (if not && and ||)
							if (op.Name != "&&" && op.Name != "||")
								_messageHandler.ReportMessage($"Compiler error (should not happen): unexpected operator {op.Name}");
							theFunc = null;
							break;
						}
				}
				if (theFunc != null)
					builtInBinOperators.Add((op.Name, op.LhsType, op.RhsType), theFunc);
			}
		}
	}
}
