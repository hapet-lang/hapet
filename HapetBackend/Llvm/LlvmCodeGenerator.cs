using HapetBackend.Llvm.Linkers;
using HapetBackend.Llvm.Linkers.Windows;
using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using HapetPostPrepare;
using LLVMSharp;
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
        private LLVMTypeRef _voidPointerType;

        private List<string> _libsToBeLinked;

        public unsafe bool GenerateCode(Compiler compiler, PostPrepare postPreparer, IMessageHandler messageHandler)
        {
            // this list is used to store all [DllImport("...")] shite
            _libsToBeLinked = new List<string>();

            LLVM.InitializeAllTargetMCs();
            LLVM.InitializeAllTargets();
            LLVM.InitializeAllTargetInfos();
            LLVM.InitializeAllAsmParsers();
            LLVM.InitializeAllAsmPrinters();

            this._compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            this._postPreparer = postPreparer ?? throw new ArgumentNullException(nameof(postPreparer));
            this._messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
            this._outDir = _compiler.CurrentProjectSettings.OutputDirectory;
            this._targetFile = _compiler.CurrentProjectSettings.AssemblyName;

            // getting the target LLVM triple
            this._targetTriple = CompilerSettings.GetTargetTriple(_compiler.CurrentProjectSettings.TargetPlatformData);

            _context = LLVMContextRef.Create();
            _module = _context.CreateModuleWithName($"{_compiler.CurrentProjectSettings.ProjectName}-module");
            _module.Target = _targetTriple;

            var target = LLVMTargetRef.GetTargetFromTriple(_targetTriple);
            var targetMachine = target.CreateTargetMachine(_targetTriple, "", "",
                LLVMCodeGenOptLevel.LLVMCodeGenLevelNone, LLVMRelocMode.LLVMRelocDefault,
                LLVMCodeModel.LLVMCodeModelDefault);

            _targetData = targetMachine.CreateTargetDataLayout();
            _module.SetDataLayout(_targetData);
            LLVM.EnablePrettyStackTrace();

            _builder = _context.CreateBuilder();
            _voidPointerType = ((LLVMTypeRef)_context.Int8Type).GetPointerTo();

            // init built in operators for llvm
            InitOperators();
            // declare overflow function checkers
            AddCheckedNumericOps();

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
                if (!_module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message))
                {
                    messageHandler.ReportMessage([message], ErrorCode.Get(CTEN.LLVMValidateError), ReportType.Error);
                }
            }

            // generate out dir
            if (!string.IsNullOrWhiteSpace(_outDir) && !Directory.Exists(_outDir))
                Directory.CreateDirectory(_outDir);

#if DEBUG
            if (!_compiler.CurrentProjectSettings.IsReferencedCompilation && !CompilerSettings.IsInRunContext)
                messageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(_compiler.CompilationStopwatch.Elapsed)} Emmiting..."], null, ReportType.Info);
#endif

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
                _messageHandler.ReportMessage([], ErrorCode.Get(CTEN.NoObjectFileGenerated));
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
            _context.Dispose();
        }

#pragma warning disable CA1002 // Do not expose generic lists
        public bool CompileCode(
            List<string> libraryIncludeDirectories, 
            List<string> libraries, 
            IMessageHandler messageHandler,
            out string outFilePath)
#pragma warning restore CA1002 // Do not expose generic lists
        {
            ArgumentNullException.ThrowIfNull(messageHandler);
            ArgumentNullException.ThrowIfNull(libraries);
            ArgumentNullException.ThrowIfNull(libraryIncludeDirectories);

#if DEBUG
            // no need to print info when it is a referenced build
            if (!_compiler.CurrentProjectSettings.IsReferencedCompilation && !CompilerSettings.IsInRunContext)
                messageHandler.ReportMessage([$"{Funcad.GetPrettyTimeString(_compiler.CompilationStopwatch.Elapsed)} Linking..."], null, ReportType.Info);
#endif

            // if there is no out dir - create it
            if (!string.IsNullOrWhiteSpace(_outDir) && !Directory.Exists(_outDir))
                Directory.CreateDirectory(_outDir);

            // getting paths to the obj/bin files
            string objFile = Path.Combine(_outDir, _targetFile + _compiler.CurrentProjectSettings.TargetPlatformData.ObjectFileExtension);
            string exeFile = Path.Combine(_outDir, _targetFile);

            // merging dependency libraries that were found in code with 
            // [DllImport(...)] attr
            foreach (var r in _libsToBeLinked)
            {
                // getting the proper data
                bool isOk = LinkHelper.GetLibraryPaths(r, _outDir, out (string, string) data);
                if (!isOk)
                {
                    _compiler.MessageHandler.ReportMessage([r], ErrorCode.Get(CTEN.AssemblyToLinkNotFound));
                    continue;
                }
                libraryIncludeDirectories.Add(data.Item1);
                // TODO: is there .lib file when we are on linux?
                libraries.Add($"{data.Item2}.lib");
            }

            switch (_compiler.CurrentProjectSettings.TargetPlatformData.TargetPlatform)
            {
                case TargetPlatform.Win86:
                case TargetPlatform.Win64:
                    return (new WinLinker()).Link(_compiler, exeFile, objFile, libraryIncludeDirectories, libraries, messageHandler, out outFilePath);
                case TargetPlatform.Linux86:
                case TargetPlatform.Linux64:
                // TODO: ... 
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
