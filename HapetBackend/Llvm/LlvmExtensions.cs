using LLVMSharp.Interop;

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
	}
}
