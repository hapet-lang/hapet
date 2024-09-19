using HapetFrontend.Types;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
	public partial class LlvmCodeGenerator
	{
		private Dictionary<(string, HapetType), Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef>> builtInOperators;

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
			builtInOperators = new Dictionary<(string, HapetType), Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef>>
		   {
                // 
                { ("+", IntType.GetIntType(1, true)), LlvmExtensions.BuildAdd },
				{ ("+", IntType.GetIntType(2, true)), LlvmExtensions.BuildAdd },
				{ ("+", IntType.GetIntType(4, true)), LlvmExtensions.BuildAdd },
				{ ("+", IntType.GetIntType(8, true)), LlvmExtensions.BuildAdd },

				{ ("+", IntType.GetIntType(1, false)), LlvmExtensions.BuildAdd },
				{ ("+", IntType.GetIntType(2, false)), LlvmExtensions.BuildAdd },
				{ ("+", IntType.GetIntType(4, false)), LlvmExtensions.BuildAdd },
				{ ("+", IntType.GetIntType(8, false)), LlvmExtensions.BuildAdd },

				{ ("-", IntType.GetIntType(1, true)), LlvmExtensions.BuildSub },
				{ ("-", IntType.GetIntType(2, true)), LlvmExtensions.BuildSub },
				{ ("-", IntType.GetIntType(4, true)), LlvmExtensions.BuildSub },
				{ ("-", IntType.GetIntType(8, true)), LlvmExtensions.BuildSub },

				{ ("-", IntType.GetIntType(1, false)), LlvmExtensions.BuildSub },
				{ ("-", IntType.GetIntType(2, false)), LlvmExtensions.BuildSub },
				{ ("-", IntType.GetIntType(4, false)), LlvmExtensions.BuildSub },
				{ ("-", IntType.GetIntType(8, false)), LlvmExtensions.BuildSub },

				{ ("*", IntType.GetIntType(1, true)), LlvmExtensions.BuildMul },
				{ ("*", IntType.GetIntType(2, true)), LlvmExtensions.BuildMul },
				{ ("*", IntType.GetIntType(4, true)), LlvmExtensions.BuildMul },
				{ ("*", IntType.GetIntType(8, true)), LlvmExtensions.BuildMul },

				{ ("*", IntType.GetIntType(1, false)), LlvmExtensions.BuildMul },
				{ ("*", IntType.GetIntType(2, false)), LlvmExtensions.BuildMul },
				{ ("*", IntType.GetIntType(4, false)), LlvmExtensions.BuildMul },
				{ ("*", IntType.GetIntType(8, false)), LlvmExtensions.BuildMul },

				{ ("/", IntType.GetIntType(1, true)), LlvmExtensions.BuildSDiv },
				{ ("/", IntType.GetIntType(2, true)), LlvmExtensions.BuildSDiv },
				{ ("/", IntType.GetIntType(4, true)), LlvmExtensions.BuildSDiv },
				{ ("/", IntType.GetIntType(8, true)), LlvmExtensions.BuildSDiv },

				{ ("/", IntType.GetIntType(1, false)), LlvmExtensions.BuildUDiv },
				{ ("/", IntType.GetIntType(2, false)), LlvmExtensions.BuildUDiv },
				{ ("/", IntType.GetIntType(4, false)), LlvmExtensions.BuildUDiv },
				{ ("/", IntType.GetIntType(8, false)), LlvmExtensions.BuildUDiv },

				{ ("%", IntType.GetIntType(1, true)), LlvmExtensions.BuildSRem },
				{ ("%", IntType.GetIntType(2, true)), LlvmExtensions.BuildSRem },
				{ ("%", IntType.GetIntType(4, true)), LlvmExtensions.BuildSRem },
				{ ("%", IntType.GetIntType(8, true)), LlvmExtensions.BuildSRem },

				{ ("%", IntType.GetIntType(1, false)), LlvmExtensions.BuildURem },
				{ ("%", IntType.GetIntType(2, false)), LlvmExtensions.BuildURem },
				{ ("%", IntType.GetIntType(4, false)), LlvmExtensions.BuildURem },
				{ ("%", IntType.GetIntType(8, false)), LlvmExtensions.BuildURem },

				{ ("+", FloatType.GetFloatType(2)), LlvmExtensions.BuildFAdd },
				{ ("+", FloatType.GetFloatType(4)), LlvmExtensions.BuildFAdd },
				{ ("+", FloatType.GetFloatType(8)), LlvmExtensions.BuildFAdd },

				{ ("-", FloatType.GetFloatType(2)), LlvmExtensions.BuildFSub },
				{ ("-", FloatType.GetFloatType(4)), LlvmExtensions.BuildFSub },
				{ ("-", FloatType.GetFloatType(8)), LlvmExtensions.BuildFSub },

				{ ("*", FloatType.GetFloatType(2)), LlvmExtensions.BuildFMul },
				{ ("*", FloatType.GetFloatType(4)), LlvmExtensions.BuildFMul },
				{ ("*", FloatType.GetFloatType(8)), LlvmExtensions.BuildFMul },

				{ ("/", FloatType.GetFloatType(2)), LlvmExtensions.BuildFDiv },
				{ ("/", FloatType.GetFloatType(4)), LlvmExtensions.BuildFDiv },
				{ ("/", FloatType.GetFloatType(8)), LlvmExtensions.BuildFDiv },

				{ ("%", FloatType.GetFloatType(2)), LlvmExtensions.BuildFRem },
				{ ("%", FloatType.GetFloatType(4)), LlvmExtensions.BuildFRem },
				{ ("%", FloatType.GetFloatType(8)), LlvmExtensions.BuildFRem },

                //
                { ("==", IntType.GetIntType(1, false)), GetICompare(LLVMIntPredicate.LLVMIntEQ) },
				{ ("==", IntType.GetIntType(2, false)), GetICompare(LLVMIntPredicate.LLVMIntEQ) },
				{ ("==", IntType.GetIntType(4, false)), GetICompare(LLVMIntPredicate.LLVMIntEQ) },
				{ ("==", IntType.GetIntType(8, false)), GetICompare(LLVMIntPredicate.LLVMIntEQ) },
				{ ("==", IntType.GetIntType(1, true)), GetICompare(LLVMIntPredicate.LLVMIntEQ) },
				{ ("==", IntType.GetIntType(2, true)), GetICompare(LLVMIntPredicate.LLVMIntEQ) },
				{ ("==", IntType.GetIntType(4, true)), GetICompare(LLVMIntPredicate.LLVMIntEQ) },
				{ ("==", IntType.GetIntType(8, true)), GetICompare(LLVMIntPredicate.LLVMIntEQ) },

				{ ("!=", IntType.GetIntType(1, false)), GetICompare(LLVMIntPredicate.LLVMIntNE) },
				{ ("!=", IntType.GetIntType(2, false)), GetICompare(LLVMIntPredicate.LLVMIntNE) },
				{ ("!=", IntType.GetIntType(4, false)), GetICompare(LLVMIntPredicate.LLVMIntNE) },
				{ ("!=", IntType.GetIntType(8, false)), GetICompare(LLVMIntPredicate.LLVMIntNE) },
				{ ("!=", IntType.GetIntType(1, true)), GetICompare(LLVMIntPredicate.LLVMIntNE) },
				{ ("!=", IntType.GetIntType(2, true)), GetICompare(LLVMIntPredicate.LLVMIntNE) },
				{ ("!=", IntType.GetIntType(4, true)), GetICompare(LLVMIntPredicate.LLVMIntNE) },
				{ ("!=", IntType.GetIntType(8, true)), GetICompare(LLVMIntPredicate.LLVMIntNE) },

				{ ("<", IntType.GetIntType(1, false)), GetICompare(LLVMIntPredicate.LLVMIntULT) },
				{ ("<", IntType.GetIntType(2, false)), GetICompare(LLVMIntPredicate.LLVMIntULT) },
				{ ("<", IntType.GetIntType(4, false)), GetICompare(LLVMIntPredicate.LLVMIntULT) },
				{ ("<", IntType.GetIntType(8, false)), GetICompare(LLVMIntPredicate.LLVMIntULT) },
				{ ("<", IntType.GetIntType(1, true)), GetICompare(LLVMIntPredicate.LLVMIntSLT) },
				{ ("<", IntType.GetIntType(2, true)), GetICompare(LLVMIntPredicate.LLVMIntSLT) },
				{ ("<", IntType.GetIntType(4, true)), GetICompare(LLVMIntPredicate.LLVMIntSLT) },
				{ ("<", IntType.GetIntType(8, true)), GetICompare(LLVMIntPredicate.LLVMIntSLT) },

				{ ("<=", IntType.GetIntType(1, false)), GetICompare(LLVMIntPredicate.LLVMIntULE) },
				{ ("<=", IntType.GetIntType(2, false)), GetICompare(LLVMIntPredicate.LLVMIntULE) },
				{ ("<=", IntType.GetIntType(4, false)), GetICompare(LLVMIntPredicate.LLVMIntULE) },
				{ ("<=", IntType.GetIntType(8, false)), GetICompare(LLVMIntPredicate.LLVMIntULE) },
				{ ("<=", IntType.GetIntType(1, true)), GetICompare(LLVMIntPredicate.LLVMIntSLE) },
				{ ("<=", IntType.GetIntType(2, true)), GetICompare(LLVMIntPredicate.LLVMIntSLE) },
				{ ("<=", IntType.GetIntType(4, true)), GetICompare(LLVMIntPredicate.LLVMIntSLE) },
				{ ("<=", IntType.GetIntType(8, true)), GetICompare(LLVMIntPredicate.LLVMIntSLE) },

				{ (">", IntType.GetIntType(1, false)), GetICompare(LLVMIntPredicate.LLVMIntUGT) },
				{ (">", IntType.GetIntType(2, false)), GetICompare(LLVMIntPredicate.LLVMIntUGT) },
				{ (">", IntType.GetIntType(4, false)), GetICompare(LLVMIntPredicate.LLVMIntUGT) },
				{ (">", IntType.GetIntType(8, false)), GetICompare(LLVMIntPredicate.LLVMIntUGT) },
				{ (">", IntType.GetIntType(1, true)), GetICompare(LLVMIntPredicate.LLVMIntSGT) },
				{ (">", IntType.GetIntType(2, true)), GetICompare(LLVMIntPredicate.LLVMIntSGT) },
				{ (">", IntType.GetIntType(4, true)), GetICompare(LLVMIntPredicate.LLVMIntSGT) },
				{ (">", IntType.GetIntType(8, true)), GetICompare(LLVMIntPredicate.LLVMIntSGT) },

				{ (">=", IntType.GetIntType(1, false)), GetICompare(LLVMIntPredicate.LLVMIntUGE) },
				{ (">=", IntType.GetIntType(2, false)), GetICompare(LLVMIntPredicate.LLVMIntUGE) },
				{ (">=", IntType.GetIntType(4, false)), GetICompare(LLVMIntPredicate.LLVMIntUGE) },
				{ (">=", IntType.GetIntType(8, false)), GetICompare(LLVMIntPredicate.LLVMIntUGE) },
				{ (">=", IntType.GetIntType(1, true)), GetICompare(LLVMIntPredicate.LLVMIntSGE) },
				{ (">=", IntType.GetIntType(2, true)), GetICompare(LLVMIntPredicate.LLVMIntSGE) },
				{ (">=", IntType.GetIntType(4, true)), GetICompare(LLVMIntPredicate.LLVMIntSGE) },
				{ (">=", IntType.GetIntType(8, true)), GetICompare(LLVMIntPredicate.LLVMIntSGE) },

				{ ("==", FloatType.GetFloatType(2)), GetFCompare(LLVMRealPredicate.LLVMRealOEQ) },
				{ ("==", FloatType.GetFloatType(4)), GetFCompare(LLVMRealPredicate.LLVMRealOEQ) },
				{ ("==", FloatType.GetFloatType(8)), GetFCompare(LLVMRealPredicate.LLVMRealOEQ) },

				{ ("!=", FloatType.GetFloatType(2)), GetFCompare(LLVMRealPredicate.LLVMRealONE) },
				{ ("!=", FloatType.GetFloatType(4)), GetFCompare(LLVMRealPredicate.LLVMRealONE) },
				{ ("!=", FloatType.GetFloatType(8)), GetFCompare(LLVMRealPredicate.LLVMRealONE) },

				{ ("<", FloatType.GetFloatType(2)), GetFCompare(LLVMRealPredicate.LLVMRealOLT) },
				{ ("<", FloatType.GetFloatType(4)), GetFCompare(LLVMRealPredicate.LLVMRealOLT) },
				{ ("<", FloatType.GetFloatType(8)), GetFCompare(LLVMRealPredicate.LLVMRealOLT) },

				{ ("<=", FloatType.GetFloatType(2)), GetFCompare(LLVMRealPredicate.LLVMRealOLE) },
				{ ("<=", FloatType.GetFloatType(4)), GetFCompare(LLVMRealPredicate.LLVMRealOLE) },
				{ ("<=", FloatType.GetFloatType(8)), GetFCompare(LLVMRealPredicate.LLVMRealOLE) },

				{ (">", FloatType.GetFloatType(2)), GetFCompare(LLVMRealPredicate.LLVMRealOGT) },
				{ (">", FloatType.GetFloatType(4)), GetFCompare(LLVMRealPredicate.LLVMRealOGT) },
				{ (">", FloatType.GetFloatType(8)), GetFCompare(LLVMRealPredicate.LLVMRealOGT) },

				{ (">=", FloatType.GetFloatType(2)), GetFCompare(LLVMRealPredicate.LLVMRealOGE) },
				{ (">=", FloatType.GetFloatType(4)), GetFCompare(LLVMRealPredicate.LLVMRealOGE) },
				{ (">=", FloatType.GetFloatType(8)), GetFCompare(LLVMRealPredicate.LLVMRealOGE) },

                //
                { ("==", BoolType.Instance), GetICompare(LLVMIntPredicate.LLVMIntEQ) },
				{ ("!=", BoolType.Instance), GetICompare(LLVMIntPredicate.LLVMIntNE) },

                //
                { ("+", CharType.GetCharType(1)), LlvmExtensions.BuildAdd },
				{ ("-", CharType.GetCharType(1)), LlvmExtensions.BuildSub },
				{ ("==", CharType.GetCharType(1)), GetICompare(LLVMIntPredicate.LLVMIntEQ) },
				{ ("!=", CharType.GetCharType(1)), GetICompare(LLVMIntPredicate.LLVMIntNE) },
				{ (">", CharType.GetCharType(1)), GetICompare(LLVMIntPredicate.LLVMIntSGT) },
				{ (">=", CharType.GetCharType(1)), GetICompare(LLVMIntPredicate.LLVMIntSGE) },
				{ ("<", CharType.GetCharType(1)), GetICompare(LLVMIntPredicate.LLVMIntSLT) },
				{ ("<=", CharType.GetCharType(1)), GetICompare(LLVMIntPredicate.LLVMIntSLE) },

				{ ("+", CharType.GetCharType(2)), LlvmExtensions.BuildAdd },
				{ ("-", CharType.GetCharType(2)), LlvmExtensions.BuildSub },
				{ ("==", CharType.GetCharType(2)), GetICompare(LLVMIntPredicate.LLVMIntEQ) },
				{ ("!=", CharType.GetCharType(2)), GetICompare(LLVMIntPredicate.LLVMIntNE) },
				{ (">", CharType.GetCharType(2)), GetICompare(LLVMIntPredicate.LLVMIntSGT) },
				{ (">=", CharType.GetCharType(2)), GetICompare(LLVMIntPredicate.LLVMIntSGE) },
				{ ("<", CharType.GetCharType(2)), GetICompare(LLVMIntPredicate.LLVMIntSLT) },
				{ ("<=", CharType.GetCharType(2)), GetICompare(LLVMIntPredicate.LLVMIntSLE) },

				{ ("+", CharType.GetCharType(4)), LlvmExtensions.BuildAdd },
				{ ("-", CharType.GetCharType(4)), LlvmExtensions.BuildSub },
				{ ("==", CharType.GetCharType(4)), GetICompare(LLVMIntPredicate.LLVMIntEQ) },
				{ ("!=", CharType.GetCharType(4)), GetICompare(LLVMIntPredicate.LLVMIntNE) },
				{ (">", CharType.GetCharType(4)), GetICompare(LLVMIntPredicate.LLVMIntSGT) },
				{ (">=", CharType.GetCharType(4)), GetICompare(LLVMIntPredicate.LLVMIntSGE) },
				{ ("<", CharType.GetCharType(4)), GetICompare(LLVMIntPredicate.LLVMIntSLT) },
				{ ("<=", CharType.GetCharType(4)), GetICompare(LLVMIntPredicate.LLVMIntSLE) },
		   };
		}
	}
}
