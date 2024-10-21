using LLVMSharp.Interop;
using System.Xml.Linq;

namespace HapetBackend.Llvm
{
	public static class LlvmExtensions
	{
		public unsafe static void SetDataLayout(this LLVMModuleRef module, LLVMTargetDataRef data)
		{
			LLVM.SetModuleDataLayout(module, data);
		}

		public unsafe static LLVMTypeRef GetPointerTo(this LLVMTypeRef self)
		{
			return LLVM.PointerType(self, 0);
		}

		public unsafe static LLVMOpaqueType* GetPointerToOpaque(this LLVMTypeRef self)
		{
			return LLVM.PointerType(self, 0);
		}

		#region Builder remake
		public unsafe static LLVMValueRef BuildAdd(LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string op)
		{
			using var marshaledName = new MarshaledString(op.AsSpan());
			return LLVM.BuildAdd(builder, left, right, marshaledName);
		}
		public unsafe static LLVMValueRef BuildSub(LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string op)
		{
			using var marshaledName = new MarshaledString(op.AsSpan());
			return LLVM.BuildSub(builder, left, right, marshaledName);
		}
		public unsafe static LLVMValueRef BuildMul(LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string op)
		{
			using var marshaledName = new MarshaledString(op.AsSpan());
			return LLVM.BuildMul(builder, left, right, marshaledName);
		}
		public unsafe static LLVMValueRef BuildSDiv(LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string op)
		{
			using var marshaledName = new MarshaledString(op.AsSpan());
			return LLVM.BuildSDiv(builder, left, right, marshaledName);
		}
		public unsafe static LLVMValueRef BuildUDiv(LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string op)
		{
			using var marshaledName = new MarshaledString(op.AsSpan());
			return LLVM.BuildUDiv(builder, left, right, marshaledName);
		}
		public unsafe static LLVMValueRef BuildSRem(LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string op)
		{
			using var marshaledName = new MarshaledString(op.AsSpan());
			return LLVM.BuildSRem(builder, left, right, marshaledName);
		}
		public unsafe static LLVMValueRef BuildURem(LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string op)
		{
			using var marshaledName = new MarshaledString(op.AsSpan());
			return LLVM.BuildURem(builder, left, right, marshaledName);
		}

		public unsafe static LLVMValueRef BuildAnd(LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string op)
		{
			using var marshaledName = new MarshaledString(op.AsSpan());
			return LLVM.BuildAnd(builder, left, right, marshaledName);
		}
		public unsafe static LLVMValueRef BuildOr(LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string op)
		{
			using var marshaledName = new MarshaledString(op.AsSpan());
			return LLVM.BuildOr(builder, left, right, marshaledName);
		}

		public unsafe static LLVMValueRef BuildFAdd(LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string op)
		{
			using var marshaledName = new MarshaledString(op.AsSpan());
			return LLVM.BuildFAdd(builder, left, right, marshaledName);
		}
		public unsafe static LLVMValueRef BuildFSub(LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string op)
		{
			using var marshaledName = new MarshaledString(op.AsSpan());
			return LLVM.BuildFSub(builder, left, right, marshaledName);
		}
		public unsafe static LLVMValueRef BuildFMul(LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string op)
		{
			using var marshaledName = new MarshaledString(op.AsSpan());
			return LLVM.BuildFMul(builder, left, right, marshaledName);
		}
		public unsafe static LLVMValueRef BuildFDiv(LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string op)
		{
			using var marshaledName = new MarshaledString(op.AsSpan());
			return LLVM.BuildFDiv(builder, left, right, marshaledName);
		}
		public unsafe static LLVMValueRef BuildFRem(LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string op)
		{
			using var marshaledName = new MarshaledString(op.AsSpan());
			return LLVM.BuildFRem(builder, left, right, marshaledName);
		}
		#endregion
	}
}
