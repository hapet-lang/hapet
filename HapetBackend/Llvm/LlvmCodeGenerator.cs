using HapetBackend.Llvm.Linkers;
using HapetBackend.Llvm.Linkers.Windows;
using HapetCommon;
using HapetFrontend;
using HapetFrontend.Entities;
using LLVMSharp;
using LLVMSharp.Interop;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

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

			if (_compiler.MainFunction == null && (CompilerSettings.TargetFormat == TargetFormat.Console || CompilerSettings.TargetFormat == TargetFormat.Windowed))
			{
				_errorHandler.ReportError("Main function could not be found...");
				return false;
			}

			this._targetTriple = GetTargetTriple(CompilerSettings.TargetPlatformData);

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

			// init built in operators for llvm
			InitOperators();

			// InitTypeInfoLLVMTypes(); // TODO: it is reflection
			GenerateCode();

			// generating main call
			GenerateMainFunction();

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

			// do not generate exe/dll if there are errors
			if (!errorHandler.HasErrors)
			{
                // emit machine code to object file
                var objFile = Path.Combine(irDir, $"{targetFile}{CompilerSettings.TargetPlatformData.ObjectFileExtension}");
				targetMachine.EmitToFile(_module, objFile, LLVMCodeGenFileType.LLVMObjectFile);
			}
			else
			{
                // TODO: info that the out file was not generated due errors
                return false;
            }

			// TODO: dispose before every return!!!
			_builder.Dispose();
			_module.Dispose();
			return true;
		}

		public bool CompileCode(IEnumerable<string> libraryIncludeDirectories, IEnumerable<string> libraries, string subsystem, IErrorHandler errorHandler, bool printLikerArgs)
		{
			if (!string.IsNullOrWhiteSpace(_outDir) && !Directory.Exists(_outDir))
				Directory.CreateDirectory(_outDir);

			string objFile = Path.Combine(_irDir, _targetFile + CompilerSettings.TargetPlatformData.ObjectFileExtension);
			string exeFile = Path.Combine(_outDir, _targetFile);


			switch (CompilerSettings.TargetPlatformData.TargetPlatform)
			{
				case TargetPlatform.Win86:
				case TargetPlatform.Win64:
					return WinLinker.Link(_compiler, exeFile, objFile, libraryIncludeDirectories, libraries, subsystem, errorHandler, printLikerArgs);
				case TargetPlatform.Linux86:
				case TargetPlatform.Linux64:
					// TODO: ... 
				default:
					throw new NotImplementedException();
			}
		}
	}
}
