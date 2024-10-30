using HapetBackend.Llvm.Linkers.Windows;
using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Parsing.PostPrepare;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
	public partial class LlvmCodeGenerator 
	{
		private IMessageHandler _messageHandler;
		private Compiler _compiler;
		private PostPrepare _postPreparer;
		/// <summary>
		/// Intermediate lang LLVM IR
		/// </summary>
		private string _outDir;
		private string _targetFile;
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
					return "i686-pc-windows-msvc";
				case TargetPlatform.Win64:
					return "x86_64-pc-windows-msvc";
				case TargetPlatform.Linux86:
					return "i686-pc-linux-gnu";
				case TargetPlatform.Linux64:
					return "x86_64-pc-linux-gnu";
			}
			throw new NotImplementedException();
		}

		public unsafe bool GenerateCode(Compiler compiler, PostPrepare postPreparer, IMessageHandler messageHandler)
		{
			LLVM.InitializeAllTargetMCs();
			LLVM.InitializeAllTargets();
			LLVM.InitializeAllTargetInfos();
			LLVM.InitializeAllAsmParsers();
			LLVM.InitializeAllAsmPrinters();

			this._compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
			this._postPreparer = postPreparer ?? throw new ArgumentNullException(nameof(postPreparer));
			this._messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
			this._outDir = _compiler.CurrentProjectSettings.OutputDirectory;
			this._targetFile = _compiler.CurrentProjectSettings.ProjectName;

			if (_compiler.MainFunction == null && (_compiler.CurrentProjectSettings.TargetFormat == TargetFormat.Console || _compiler.CurrentProjectSettings.TargetFormat == TargetFormat.Windowed))
			{
				_messageHandler.ReportMessage("Main function could not be found...");
				OnGenerateCodeExit();
				return false;
			}

			this._targetTriple = GetTargetTriple(_compiler.CurrentProjectSettings.TargetPlatformData);

			_module = LLVMModuleRef.CreateWithName($"{_compiler.CurrentProjectSettings.ProjectName}-module");
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
			GenerateMetadataShite();
			GenerateCode();

			// no need to gen main func for library typed project
			if (_compiler.CurrentProjectSettings.TargetFormat == TargetFormat.Console || _compiler.CurrentProjectSettings.TargetFormat == TargetFormat.Windowed)
			{
				// generating main call
				GenerateMainFunction();
			}

			// verify module
			{
				if (_module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message))
				{
					Console.Error.WriteLine($"[LLVM-validate-module] {message}");
				}
			}

			// generate out dir
			if (!string.IsNullOrWhiteSpace(_outDir) && !Directory.Exists(_outDir))
				Directory.CreateDirectory(_outDir);

			// create .ll file
			if (_compiler.CurrentProjectSettings.OutputIrFile)
			{
				_module.PrintToFile(Path.Combine(_outDir, _targetFile + ".ll"));
			}

			// do not generate exe/dll if there are errors
			if (!_messageHandler.HasErrors)
			{
                // emit machine code to object file
                var objFile = Path.Combine(_outDir, $"{_targetFile}{_compiler.CurrentProjectSettings.TargetPlatformData.ObjectFileExtension}");
				targetMachine.EmitToFile(_module, objFile, LLVMCodeGenFileType.LLVMObjectFile);
			}
			else
			{
				// info that the out file was not generated due errors
				_messageHandler.ReportMessage("Object file could not be generated due to some errors...");
				OnGenerateCodeExit();
				return false;
            }
			OnGenerateCodeExit();
			return true;
		}

		private void OnGenerateCodeExit()
		{
			_builder.Dispose();
			_module.Dispose();
		}

		public bool CompileCode(IEnumerable<string> libraryIncludeDirectories, IEnumerable<string> libraries, IMessageHandler messageHandler)
		{
			if (!string.IsNullOrWhiteSpace(_outDir) && !Directory.Exists(_outDir))
				Directory.CreateDirectory(_outDir);

			string objFile = Path.Combine(_outDir, _targetFile + _compiler.CurrentProjectSettings.TargetPlatformData.ObjectFileExtension);
			string exeFile = Path.Combine(_outDir, _targetFile);

			switch (_compiler.CurrentProjectSettings.TargetPlatformData.TargetPlatform)
			{
				case TargetPlatform.Win86:
				case TargetPlatform.Win64:
					return WinLinker.Link(_compiler, exeFile, objFile, libraryIncludeDirectories, libraries, messageHandler, _compiler.CurrentProjectSettings.Verbose);
				case TargetPlatform.Linux86:
				case TargetPlatform.Linux64:
					// TODO: ... 
				default:
					throw new NotImplementedException();
			}
		}
	}
}
