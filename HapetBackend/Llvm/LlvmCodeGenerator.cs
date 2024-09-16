using HapetCommon;
using HapetFrontend;
using LLVMSharp;
using LLVMSharp.Interop;
using System;

namespace HapetBackend.Llvm
{
	public partial class LlvmCodeGenerator
	{
		private Compiler _compiler;
		private string _inDir;
		private string _outDir;
		private string _targetFile;
		private bool _emitDebugInfo;
		private string _targetTriple;

		private LLVMModuleRef _module;
		private LLVMTargetDataRef _targetData;
		private LLVMContextRef _context;
		private LLVMBuilderRef _builder;
		private LLVMBuilderRef _rawBuilder;
		private LLVMTypeRef _voidPointerType;

		private string GetTargetTriple(PlatformData arch)
		{
			switch (arch.TargetPlatform)
			{
				case TargetPlatform.Win86:
					return "i386-pc-win32";
				case TargetPlatform.Win64:
					return "x86_64-pc-windows-gnu";
				case TargetPlatform.Linux86:
					return "i386-pc-linux-gnu";
				case TargetPlatform.Linux64:
					return "x86_64-pc-linux-gnu";
			}
			throw new NotImplementedException();
		}

		public unsafe bool GenerateCode(Compiler compiler, string inDir, string outDir, string targetFile, bool optimize, bool outputIntermediateFile)
		{
			this._compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
			this._inDir = Path.GetFullPath(inDir ?? "");
			this._outDir = Path.GetFullPath(outDir ?? "");
			this._targetFile = targetFile;
			this._emitDebugInfo = !optimize;

			this._targetTriple = GetTargetTriple(CompilerSettings.PlatformData);

			_module = LLVMModuleRef.CreateWithName("hapetlang-module");
			_module.Target = _targetTriple;

			var target = LLVMTargetRef.GetTargetFromTriple(_targetTriple);
			var targetMachine = target.CreateTargetMachine(_targetTriple, "", "",
				LLVMCodeGenOptLevel.LLVMCodeGenLevelDefault, LLVMRelocMode.LLVMRelocDefault,
				LLVMCodeModel.LLVMCodeModelDefault);

			_targetData = targetMachine.CreateTargetDataLayout();
			_module.SetDataLayout(_targetData);
			LLVM.EnablePrettyStackTrace();
			_context = _module.Context;
			
			_rawBuilder = _module.Context.CreateBuilder();
			_voidPointerType = ((LLVMTypeRef)LLVM.Int8Type()).GetPointerTo();

			// InitTypeInfoLLVMTypes(); // TODO: it is reflection
			GenerateCode();
		}
	}
}
