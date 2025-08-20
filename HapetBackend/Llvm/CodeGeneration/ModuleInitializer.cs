using HapetFrontend.Extensions;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        public (LLVMTypeRef, LLVMValueRef) CreateModuleInitializer()
        {
            // make all inites here
            var typeInitializer = CreateTypeInitializer();
            var constsInitializer = CreateConstsInitializer();

            // to check if it was inited only once
            var globStatic = _module.AddGlobal(HapetTypeToLLVMType(HapetType.CurrentTypeContext.BoolTypeInstance),
                $"is_{_compiler.CurrentProjectSettings.ProjectName}_module_initer_called");
            globStatic.Linkage = LLVMLinkage.LLVMExternalLinkage;
            globStatic.Initializer = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(HapetType.CurrentTypeContext.BoolTypeInstance), 0);

            // create a func to init all the type infos
            var ltype = LLVMTypeRef.CreateFunction(HapetTypeToLLVMType(HapetType.CurrentTypeContext.VoidTypeInstance), [], false);
            var lfunc = _module.AddFunction($"{_compiler.CurrentProjectSettings.ProjectName}_module_initer", ltype);
            lfunc.Linkage = LLVMLinkage.LLVMExternalLinkage;
            lfunc.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLExportStorageClass;

            // make check that is already inited
            var entry = lfunc.AppendBasicBlockInContext(_context, "entry");
            var notInited = lfunc.AppendBasicBlockInContext(_context, "not.inited");
            var end = lfunc.AppendBasicBlockInContext(_context, "end");

            _builder.PositionAtEnd(entry);
            var loadedVar = _builder.BuildLoad2(HapetTypeToLLVMType(HapetType.CurrentTypeContext.BoolTypeInstance), globStatic, "isInited");
            _builder.BuildCondBr(loadedVar, end, notInited);

            _builder.PositionAtEnd(notInited);
            _builder.BuildStore(LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(HapetType.CurrentTypeContext.BoolTypeInstance), 1), globStatic);

            // need to call all dependent initers
            foreach (var d in _compiler.CurrentProjectData.AllReferencedProjectNames)
            {
                string funcName = $"{d}_module_initer";
                LLVMValueRef lfuncDep = _module.AddFunction(funcName, ltype);
                lfuncDep.Linkage = LLVMLinkage.LLVMExternalLinkage;
                lfuncDep.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLImportStorageClass;
                _builder.BuildCall2(ltype, lfuncDep, []);
            }

            // need to initialize typeinfos and vtables before any code
            _builder.BuildCall2(typeInitializer.Item1, typeInitializer.Item2, []);
            // need to initialize consts
            _builder.BuildCall2(typeInitializer.Item1, typeInitializer.Item2, []);

            _builder.BuildBr(end);
            _builder.PositionAtEnd(end);
            _builder.BuildRetVoid();

            return (ltype, lfunc);
        }

        private (LLVMTypeRef, LLVMValueRef) CreateConstsInitializer()
        {
            // create a func to init all the type infos
            var ltype = LLVMTypeRef.CreateFunction(HapetTypeToLLVMType(HapetType.CurrentTypeContext.VoidTypeInstance), [], false);
            var lfunc = _module.AddFunction($"{_compiler.CurrentProjectSettings.ProjectName}_consts_initer", ltype);
            lfunc.Linkage = LLVMLinkage.LLVMExternalLinkage;

            // make check that is already inited
            var entry = lfunc.AppendBasicBlockInContext(_context, "entry");

            _builder.PositionAtEnd(entry);

            foreach (var ini in _initializersMapList)
            {
                var field = _valueMap[ini.Item1];
                var decl = (ini.Item1 as DeclSymbol).Decl;

                // special check for const
                if (!decl.SpecialKeys.Contains(TokenType.KwConst))
                    continue;
                _builder.BuildStore(HapetValueToLLVMValue(decl.Type.OutType, ini.Item2.OutValue), field);
            }
            _builder.BuildRetVoid();

            return (ltype, lfunc);
        }
    }
}
