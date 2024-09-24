using HapetCommon;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
	public partial class LlvmCodeGenerator
	{
		private unsafe void GenerateMainFunction()
		{
			string mainFuncName = null;
			LLVMTypeRef returnType = _context.VoidType;
			LLVMTypeRef[] paramTypes = Array.Empty<LLVMTypeRef>();

			LLVMTypeRef crtMainFuncType = null;
			LLVMValueRef crtMainFunc = null;

			switch (CompilerSettings.TargetPlatformData.TargetPlatform)
			{
				case TargetPlatform.Win86:
					if (CompilerSettings.TargetRepresentation == TargetRepresentation.Console)
					{
						// creating crt main func extern
						crtMainFuncType = LLVMTypeRef.CreateFunction(_context.VoidType, Array.Empty<LLVMTypeRef>(), false);
						crtMainFunc = _module.AddFunction("__main", crtMainFuncType);
						crtMainFunc.Linkage = LLVMLinkage.LLVMExternalLinkage;
						var crtEntry = crtMainFunc.AppendBasicBlock("entry");
						_builder.PositionAtEnd(crtEntry);
						_builder.BuildRetVoid();

						mainFuncName = "main";
						returnType = _context.Int32Type;
						// i32 %argc, ptr %argv
						paramTypes = new LLVMTypeRef[] { _context.Int32Type, _voidPointerType };
					}
					else if (CompilerSettings.TargetRepresentation == TargetRepresentation.Windowed)
					{
						mainFuncName = "WinMain";
						returnType = _context.Int32Type;
						// ptr %hInstance, ptr %hPrevInstance, ptr %lpCmdLine, i32 %nCmdShow
						paramTypes = new LLVMTypeRef[] { _voidPointerType, _voidPointerType, _voidPointerType, _context.Int32Type };
					}
					break;
				case TargetPlatform.Win64:
					if (CompilerSettings.TargetRepresentation == TargetRepresentation.Console)
					{
						mainFuncName = "main";
						returnType = _context.Int32Type;
						// i32 %argc, ptr %argv
						paramTypes = new LLVMTypeRef[] { _context.Int32Type, _voidPointerType };
					}
					else if (CompilerSettings.TargetRepresentation == TargetRepresentation.Windowed)
					{
						mainFuncName = "WinMain";
						returnType = _context.Int32Type;
						// ptr %hInstance, ptr %hPrevInstance, ptr %lpCmdLine, i32 %nCmdShow
						paramTypes = new LLVMTypeRef[] { _voidPointerType, _voidPointerType, _voidPointerType, _context.Int32Type };
					}
					break;
				case TargetPlatform.Linux86:
					mainFuncName = "main";
					returnType = _context.Int32Type;
					break;
				case TargetPlatform.Linux64:
					mainFuncName = "main";
					returnType = _context.Int32Type;
					break;
			}

			var ltype = LLVMTypeRef.CreateFunction(returnType, paramTypes, false);
			var lfunc = _module.AddFunction(mainFuncName, ltype);
			lfunc.Linkage = LLVMLinkage.LLVMExternalLinkage;

			// calling conventions
			// https://github.com/llvm/llvm-project/blob/main/llvm/include/llvm/IR/CallingConv.h
			switch (CompilerSettings.TargetPlatformData.TargetPlatform)
			{
				case TargetPlatform.Win86:
					if (CompilerSettings.TargetRepresentation == TargetRepresentation.Windowed)
						lfunc.FunctionCallConv = (uint)LLVMCallConv.LLVMX86StdcallCallConv; // X86_StdCall
					break;
				case TargetPlatform.Win64:
					// no need for this because it is defaulted by LLVM itself
					// lfunc.FunctionCallConv = 79; // Win64
					break;
				case TargetPlatform.Linux86:
					break;
				case TargetPlatform.Linux64:
					break;
			}

			var entry = lfunc.AppendBasicBlock("entry");
			var main = lfunc.AppendBasicBlock("mainpart");

			_builder.PositionAtEnd(entry);

			// if crt main func call should be placed 
			if (crtMainFunc != null)
			{
				_builder.BuildCall2(crtMainFuncType, crtMainFunc, Array.Empty<LLVMValueRef>());
			}

			_builder.BuildBr(main);
			_builder.PositionAtEnd(main);

			{ // call main function
				var hapetMain = _valueMap[_compiler.MainFunction.GetSymbol];
				LLVMTypeRef funcType = _typeMap[_compiler.MainFunction.Type.OutType];
				var exitCode = _builder.BuildCall2(funcType, hapetMain, Array.Empty<LLVMValueRef>(), "exitCode");
				_builder.BuildRet(exitCode);
			}
		}
	}
}
