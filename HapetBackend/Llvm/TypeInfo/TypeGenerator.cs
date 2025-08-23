using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using HapetFrontend.Types;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private void GenerateTypeConstGlobal(HapetType type)
        {
            GenerateTypeInfoConst(type);
            GenerateVirtualTableConst(type);
            GenerateTypeConst(type);
        }

        private (LLVMTypeRef, LLVMValueRef) CreateTypeInitializer()
        {
            // create typeinfo and vtable initers
            var typeInfoInitializer = CreateTypeInfoInitializer();
            var vTableInitializer = CreateVTableInitializer();
            return CreateTypeInitializer(typeInfoInitializer, vTableInitializer);
        }

        private readonly Dictionary<HapetType, LLVMValueRef> _typeDictionary = new Dictionary<HapetType, LLVMValueRef>();

        private unsafe LLVMValueRef GenerateTypeConst(HapetType type)
        {
            if (_typeDictionary.TryGetValue(type, out LLVMValueRef value))
                return value;

            AstDeclaration decl;
            string typeNameString;
            if (type is ClassType clsType)
            {
                decl = clsType.Declaration;
                typeNameString = GenericsHelper.GetCodegenGenericName(clsType.Declaration.Name, _messageHandler);
            }
            else if (type is StructType strType)
            {
                decl = strType.Declaration;
                typeNameString = GenericsHelper.GetCodegenGenericName(strType.Declaration.Name, _messageHandler);
            }
            else
            {
                // compiler error - could not generate type info 
                _messageHandler.ReportMessage(_currentSourceFile.Text, null, [HapetType.AsString(type)], ErrorCode.Get(CTEN.CouldNotGenerateTypeInfo));
                return default;
            }

            LLVMTypeRef typeInfoType = GetTypeType();
            string name = $"Type::{typeNameString}";
            var globConst = _module.AddGlobal(typeInfoType, name);
            globConst.Linkage = LLVMLinkage.LLVMExternalLinkage;

            if (decl.IsImported && !decl.IsImplOfGeneric && !(decl.IsNestedDecl && decl.ParentDecl.IsImplOfGeneric))
            {
                globConst.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLImportStorageClass;
            }
            else
            {
                globConst.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLExportStorageClass;
                globConst.Initializer = LLVMValueRef.CreateConstNull(typeInfoType);

                _typeToInit.Add((type, typeNameString));
            }
            _typeDictionary.Add(type, globConst);

            return globConst;
        }

        private readonly List<(HapetType type, string typeName)> _typeToInit = new List<(HapetType, string)>();
        private (LLVMTypeRef, LLVMValueRef) CreateTypeInitializer((LLVMTypeRef, LLVMValueRef) tpInfoIniter, (LLVMTypeRef, LLVMValueRef) vTableIniter)
        {
            // create a func to init all the type infos
            var ltype = LLVMTypeRef.CreateFunction(HapetTypeToLLVMType(HapetType.CurrentTypeContext.VoidTypeInstance), [], false);
            var lfunc = _module.AddFunction($"{_compiler.CurrentProjectSettings.ProjectName}_type_initer", ltype);
            lfunc.Linkage = LLVMLinkage.LLVMExternalLinkage;

            // make check that is already inited
            var entry = lfunc.AppendBasicBlockInContext(_context, "entry");
            _builder.PositionAtEnd(entry);

            _builder.BuildCall2(tpInfoIniter.Item1, tpInfoIniter.Item2, []);
            _builder.BuildCall2(vTableIniter.Item1, vTableIniter.Item2, []);

            // make all inites here
            foreach (var tp in _typeToInit)
            {
                GenerateInitializerForType(tp.type, tp.typeName);
            }
            _builder.BuildRetVoid();

            return (ltype, lfunc);
        }

        private LLVMTypeRef _typeType;
        private LLVMTypeRef GetTypeType()
        {
            if (_typeType != null)
                return _typeType;

            // WARN: hard cock
            var typeDecl = _currentSourceFile.NamespaceScope.GetSymbolInNamespace("System", new AstIdExpr("Type"));
            _typeType = _typeMap[typeDecl.Decl.Type.OutType];
            return _typeType;
        }

        private void GenerateInitializerForType(HapetType type, string _)
        {
            var typeType = GetTypeType();
            var globConst = _typeDictionary[type];

            var constValue = LLVMValueRef.CreateConstNamedStruct(typeType,
                new LLVMValueRef[] { _typeInfoDictionary[type], _virtualTableDictionary[type] });
            _builder.BuildStore(constValue, globConst);
        }
    }
}
