using HapetCommon;
using HapetFrontend;
using HapetFrontend.Entities;
using LLVMSharp;
using LLVMSharp.Interop;
using System;
using System.Reflection;

namespace HapetBackend.Llvm
{
	public partial class LlvmCodeGenerator 
	{
		private IErrorHandler _errorHandler;
		private Compiler _compiler;
		/// <summary>
		/// Intermediate lang LLVM IR
		/// </summary>
		private string _irDir;
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

		// TODO: mb move it to settings?
		private string GetTargetTriple(PlatformData arch)
		{
			switch (arch.TargetPlatform)
			{
				case TargetPlatform.Win86:
					return "i686-pc-windows-gnu";
				case TargetPlatform.Win64:
					return "x86_64-pc-windows-gnu";
				case TargetPlatform.Linux86:
					return "i386-pc-linux-gnu";
				case TargetPlatform.Linux64:
					return "x86_64-pc-linux-gnu";
			}
			throw new NotImplementedException();
		}

		public unsafe bool GenerateCode(Compiler compiler, IErrorHandler errorHandler, string irDir, string outDir, string targetFile, bool optimize, bool outputIntermediateFile)
		{
			LLVM.InitializeAllTargetMCs();
			LLVM.InitializeAllTargets();
			LLVM.InitializeAllTargetInfos();
			LLVM.InitializeAllAsmParsers();
			LLVM.InitializeAllAsmPrinters();

			this._compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
			this._errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
			this._irDir = Path.GetFullPath(irDir ?? "");
			this._outDir = Path.GetFullPath(outDir ?? "");
			this._targetFile = targetFile;
			this._emitDebugInfo = !optimize;

			this._targetTriple = GetTargetTriple(CompilerSettings.PlatformData);

			_module = LLVMModuleRef.CreateWithName("hapetlang-module"); // TODO: project name here
			_module.Target = _targetTriple;

			var target = LLVMTargetRef.GetTargetFromTriple(_targetTriple);
			var targetMachine = target.CreateTargetMachine(_targetTriple, "", "",
				LLVMCodeGenOptLevel.LLVMCodeGenLevelNone, LLVMRelocMode.LLVMRelocDefault,
				LLVMCodeModel.LLVMCodeModelDefault);

			_targetData = targetMachine.CreateTargetDataLayout();
			_module.SetDataLayout(_targetData);
			LLVM.EnablePrettyStackTrace();
			_context = _module.Context;

			_builder = _context.CreateBuilder();

			_rawBuilder = _module.Context.CreateBuilder();
			_voidPointerType = ((LLVMTypeRef)_context.Int8Type).GetPointerTo();

			// InitTypeInfoLLVMTypes(); // TODO: it is reflection
			GenerateCode();

			// verify module
			{
				if (_module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message))
				{
					Console.Error.WriteLine($"[LLVM-validate-module] {message}");
				}
			}

			// generate ir dir
			if (!string.IsNullOrWhiteSpace(irDir) && !Directory.Exists(irDir))
				Directory.CreateDirectory(irDir);

			// create .ll file
			if (outputIntermediateFile)
			{
				_module.PrintToFile(Path.Combine(irDir, targetFile + ".ll"));
			}

			// emit machine code to object file
			{
				var objFile = Path.Combine(irDir, $"{targetFile}{CompilerSettings.PlatformData.ObjectFileExtension}");
				targetMachine.EmitToFile(_module, objFile, LLVMCodeGenFileType.LLVMObjectFile);
			}

			_module.Dispose();
			return true;
		}
	}
}
