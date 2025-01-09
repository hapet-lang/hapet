using HapetBackend.Llvm.Linkers;
using HapetBackend.Llvm.Linkers.Windows;
using HapetFrontend;
using HapetFrontend.Entities;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing.PostPrepare;
using LLVMSharp.Interop;
using Newtonsoft.Json;
using System.Diagnostics;

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
            this._targetFile = _compiler.CurrentProjectSettings.ProjectName;

            if (_compiler.MainFunction == null && (_compiler.CurrentProjectSettings.TargetFormat == TargetFormat.Console || _compiler.CurrentProjectSettings.TargetFormat == TargetFormat.Windowed))
            {
                _messageHandler.ReportMessage("Main function could not be found...");
                OnGenerateCodeExit();
                return false;
            }

            // getting the target LLVM triple
            this._targetTriple = CompilerSettings.GetTargetTriple(_compiler.CurrentProjectSettings.TargetPlatformData);

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
                if (!_module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message))
                {
                    messageHandler.ReportMessage($"[LLVM-validate-module] {message}", ReportType.Error);
                }
            }

            // generate out dir
            if (!string.IsNullOrWhiteSpace(_outDir) && !Directory.Exists(_outDir))
                Directory.CreateDirectory(_outDir);

            if (!_compiler.CurrentProjectSettings.IsReferencedCompilation)
                messageHandler.ReportMessage($"{Funcad.GetPrettyTimeString(_compiler.CompilationStopwatch.Elapsed)} Emmiting...", ReportType.Info);

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

        public bool CompileCode(List<string> libraryIncludeDirectories, List<string> libraries, IMessageHandler messageHandler)
        {
            // no need to print info when it is a referenced build
            if (!_compiler.CurrentProjectSettings.IsReferencedCompilation)
                messageHandler.ReportMessage($"{Funcad.GetPrettyTimeString(_compiler.CompilationStopwatch.Elapsed)} Linking...", ReportType.Info);

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
                    _compiler.MessageHandler.ReportMessage($"Assembly {r} could not be found. Please check extern functions properly");
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
                    return WinLinker.Link(_compiler, exeFile, objFile, libraryIncludeDirectories, libraries, messageHandler);
                case TargetPlatform.Linux86:
                case TargetPlatform.Linux64:
                // TODO: ... 
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
