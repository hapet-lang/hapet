using HapetFrontend.Types;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
	public partial class LlvmCodeGenerator
	{
		private Dictionary<(string, HapetType), Func<LLVMValueRef, LLVMValueRef, string, LLVMValueRef>> builtInOperators;

		private Func<LLVMValueRef, LLVMValueRef, string, LLVMValueRef> GetICompare(LLVMIntPredicate pred)
		{
			return (b, c, d) => _builder.BuildICmp(pred, b, c, d);
		}

		private Func<LLVMValueRef, LLVMValueRef, string, LLVMValueRef> GetFCompare(LLVMRealPredicate pred)
		{
			return (b, c, d) => _builder.BuildFCmp(pred, b, c, d);
		}

		private void InitOperators()
		{
			builtInOperators = new Dictionary<(string, HapetType), Func<LLVMValueRef, LLVMValueRef, string, LLVMValueRef>>
		   {
                // 
                { ("+", IntType.GetIntType(1, true)), _builder.BuildAdd },
				{ ("+", IntType.GetIntType(2, true)), _builder.BuildAdd },
				{ ("+", IntType.GetIntType(4, true)), _builder.BuildAdd },
				{ ("+", IntType.GetIntType(8, true)), _builder.BuildAdd },

				{ ("+", IntType.GetIntType(1, false)), _builder.BuildAdd },
				{ ("+", IntType.GetIntType(2, false)), _builder.BuildAdd },
				{ ("+", IntType.GetIntType(4, false)), _builder.BuildAdd },
				{ ("+", IntType.GetIntType(8, false)), _builder.BuildAdd },

				{ ("-", IntType.GetIntType(1, true)), _builder.BuildSub },
				{ ("-", IntType.GetIntType(2, true)), _builder.BuildSub },
				{ ("-", IntType.GetIntType(4, true)), _builder.BuildSub },
				{ ("-", IntType.GetIntType(8, true)), _builder.BuildSub },

				{ ("-", IntType.GetIntType(1, false)), _builder.BuildSub },
				{ ("-", IntType.GetIntType(2, false)), _builder.BuildSub },
				{ ("-", IntType.GetIntType(4, false)), _builder.BuildSub },
				{ ("-", IntType.GetIntType(8, false)), _builder.BuildSub },

				{ ("*", IntType.GetIntType(1, true)), _builder.BuildMul },
				{ ("*", IntType.GetIntType(2, true)), _builder.BuildMul },
				{ ("*", IntType.GetIntType(4, true)), _builder.BuildMul },
				{ ("*", IntType.GetIntType(8, true)), _builder.BuildMul },

				{ ("*", IntType.GetIntType(1, false)), _builder.BuildMul },
				{ ("*", IntType.GetIntType(2, false)), _builder.BuildMul },
				{ ("*", IntType.GetIntType(4, false)), _builder.BuildMul },
				{ ("*", IntType.GetIntType(8, false)), _builder.BuildMul },

				{ ("/", IntType.GetIntType(1, true)), _builder.BuildSDiv },
				{ ("/", IntType.GetIntType(2, true)), _builder.BuildSDiv },
				{ ("/", IntType.GetIntType(4, true)), _builder.BuildSDiv },
				{ ("/", IntType.GetIntType(8, true)), _builder.BuildSDiv },

				{ ("/", IntType.GetIntType(1, false)), _builder.BuildUDiv },
				{ ("/", IntType.GetIntType(2, false)), _builder.BuildUDiv },
				{ ("/", IntType.GetIntType(4, false)), _builder.BuildUDiv },
				{ ("/", IntType.GetIntType(8, false)), _builder.BuildUDiv },

				{ ("%", IntType.GetIntType(1, true)), _builder.BuildSRem },
				{ ("%", IntType.GetIntType(2, true)), _builder.BuildSRem },
				{ ("%", IntType.GetIntType(4, true)), _builder.BuildSRem },
				{ ("%", IntType.GetIntType(8, true)), _builder.BuildSRem },

				{ ("%", IntType.GetIntType(1, false)), _builder.BuildURem },
				{ ("%", IntType.GetIntType(2, false)), _builder.BuildURem },
				{ ("%", IntType.GetIntType(4, false)), _builder.BuildURem },
				{ ("%", IntType.GetIntType(8, false)), _builder.BuildURem },

				{ ("+", FloatType.GetFloatType(4)), _builder.BuildFAdd },
				{ ("+", FloatType.GetFloatType(8)), _builder.BuildFAdd },

				{ ("-", FloatType.GetFloatType(4)), _builder.BuildFSub },
				{ ("-", FloatType.GetFloatType(8)), _builder.BuildFSub },

				{ ("*", FloatType.GetFloatType(4)), _builder.BuildFMul },
				{ ("*", FloatType.GetFloatType(8)), _builder.BuildFMul },

				{ ("/", FloatType.GetFloatType(4)), _builder.BuildFDiv },
				{ ("/", FloatType.GetFloatType(8)), _builder.BuildFDiv },

				{ ("%", FloatType.GetFloatType(4)), _builder.BuildFRem },
				{ ("%", FloatType.GetFloatType(8)), _builder.BuildFRem },

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

				{ ("==", FloatType.GetFloatType(4)), GetFCompare(LLVMRealPredicate.LLVMRealOEQ) },
				{ ("==", FloatType.GetFloatType(8)), GetFCompare(LLVMRealPredicate.LLVMRealOEQ) },

				{ ("!=", FloatType.GetFloatType(4)), GetFCompare(LLVMRealPredicate.LLVMRealONE) },
				{ ("!=", FloatType.GetFloatType(8)), GetFCompare(LLVMRealPredicate.LLVMRealONE) },

				{ ("<", FloatType.GetFloatType(4)), GetFCompare(LLVMRealPredicate.LLVMRealOLT) },
				{ ("<", FloatType.GetFloatType(8)), GetFCompare(LLVMRealPredicate.LLVMRealOLT) },

				{ ("<=", FloatType.GetFloatType(4)), GetFCompare(LLVMRealPredicate.LLVMRealOLE) },
				{ ("<=", FloatType.GetFloatType(8)), GetFCompare(LLVMRealPredicate.LLVMRealOLE) },

				{ (">", FloatType.GetFloatType(4)), GetFCompare(LLVMRealPredicate.LLVMRealOGT) },
				{ (">", FloatType.GetFloatType(8)), GetFCompare(LLVMRealPredicate.LLVMRealOGT) },

				{ (">=", FloatType.GetFloatType(4)), GetFCompare(LLVMRealPredicate.LLVMRealOGE) },
				{ (">=", FloatType.GetFloatType(8)), GetFCompare(LLVMRealPredicate.LLVMRealOGE) },

                //
                { ("==", BoolType.Instance), GetICompare(LLVMIntPredicate.LLVMIntEQ) },
				{ ("!=", BoolType.Instance), GetICompare(LLVMIntPredicate.LLVMIntNE) },

                //
                { ("+", CharType.GetCharType(1)), _builder.BuildAdd },
				{ ("-", CharType.GetCharType(1)), _builder.BuildSub },
				{ ("==", CharType.GetCharType(1)), GetICompare(LLVMIntPredicate.LLVMIntEQ) },
				{ ("!=", CharType.GetCharType(1)), GetICompare(LLVMIntPredicate.LLVMIntNE) },
				{ (">", CharType.GetCharType(1)), GetICompare(LLVMIntPredicate.LLVMIntSGT) },
				{ (">=", CharType.GetCharType(1)), GetICompare(LLVMIntPredicate.LLVMIntSGE) },
				{ ("<", CharType.GetCharType(1)), GetICompare(LLVMIntPredicate.LLVMIntSLT) },
				{ ("<=", CharType.GetCharType(1)), GetICompare(LLVMIntPredicate.LLVMIntSLE) },

				{ ("+", CharType.GetCharType(2)), _builder.BuildAdd },
				{ ("-", CharType.GetCharType(2)), _builder.BuildSub },
				{ ("==", CharType.GetCharType(2)), GetICompare(LLVMIntPredicate.LLVMIntEQ) },
				{ ("!=", CharType.GetCharType(2)), GetICompare(LLVMIntPredicate.LLVMIntNE) },
				{ (">", CharType.GetCharType(2)), GetICompare(LLVMIntPredicate.LLVMIntSGT) },
				{ (">=", CharType.GetCharType(2)), GetICompare(LLVMIntPredicate.LLVMIntSGE) },
				{ ("<", CharType.GetCharType(2)), GetICompare(LLVMIntPredicate.LLVMIntSLT) },
				{ ("<=", CharType.GetCharType(2)), GetICompare(LLVMIntPredicate.LLVMIntSLE) },

				{ ("+", CharType.GetCharType(4)), _builder.BuildAdd },
				{ ("-", CharType.GetCharType(4)), _builder.BuildSub },
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
