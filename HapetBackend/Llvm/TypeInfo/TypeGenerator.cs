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

        private unsafe LLVMValueRef GenerateTypeConst(HapetType type, bool initialize = false)
        {
            if (_typeDictionary.TryGetValue(type, out LLVMValueRef value))
                return value;

            HapetType parent;
            AstDeclaration decl;
            string typeNameString;
            if (type is ClassType clsType)
            {
                decl = clsType.Declaration;
                parent = clsType.Declaration.InheritedFrom.FirstOrDefault(x => x.OutType is ClassType clss && !clss.Declaration.IsInterface)?.OutType as ClassType;
                typeNameString = GenericsHelper.GetCodegenGenericName(clsType.Declaration.Name, _messageHandler);
            }
            else if (type is StructType strType)
            {
                decl = strType.Declaration;
                parent = strType.Declaration.InheritedFrom.FirstOrDefault(x => x.OutType is ClassType clss && !clss.Declaration.IsInterface)?.OutType as ClassType;
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

                if (!initialize)
                    _typeToInit.Add((type, typeNameString, parent));
                else
                    GenerateInitializerForType(type, typeNameString, parent);
            }
            _typeDictionary.Add(type, globConst);

            return globConst;
        }

        private readonly List<(HapetType type, string name, HapetType parent)> _typeToInit = new List<(HapetType, string, HapetType)>();
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
                GenerateInitializerForType(tp.type, tp.name, tp.parent);
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

        private void GenerateInitializerForType(HapetType type, string typeNameString, HapetType parent)
        {
            var typeType = GetTypeType();
            var globConst = _typeDictionary[type];

            // name param
            LLVMValueRef typeName = _module.AddGlobal(LLVMTypeRef.CreateArray(_context.Int8Type, (uint)(typeNameString.Length + 1)), $"TypeInfoName::{typeNameString}");
            typeName.Initializer = _context.GetConstString(typeNameString, false);
            typeName.IsGlobalConstant = true;

            // parent param
            var ptrT = LLVMTypeRef.CreatePointer(typeType, 0);
            var nullPtr = LLVMValueRef.CreateConstPointerNull(ptrT);
            LLVMValueRef parentRef = parent == null ? nullPtr : GenerateTypeConst(parent, true);

            // interfaces
            var interfaces = GetTypeInterfacesArray(type, out int interfacesCount);
            LLVMValueRef interfacesCountRef = LLVMValueRef.CreateConstInt(_context.Int8Type, (ulong)interfacesCount);

            var constValue = LLVMValueRef.CreateConstNamedStruct(typeType,
                new LLVMValueRef[] { _typeInfoDictionary[type], _virtualTableDictionary[type], typeName, 00, parentRef, interfaces, interfacesCountRef });
            _builder.BuildStore(constValue, globConst);
        }

        private LLVMValueRef GetTypeInterfacesArray(HapetType type, out int amount)
        {
            LLVMTypeRef arrayElementType = LLVMTypeRef.CreatePointer(GetTypeType(), 0);
            var interfaces = GetAllInterfaces(type, true);
            if (interfaces.Count == 0)
            {
                amount = 0;
                var ptrT = LLVMTypeRef.CreatePointer(arrayElementType, 0);
                var nullPtr = LLVMValueRef.CreateConstPointerNull(ptrT);
                return nullPtr;
            }

            string typeNameString;
            if (type is ClassType clsType)
                typeNameString = GenericsHelper.GetCodegenGenericName(clsType.Declaration.Name, _messageHandler);
            else if (type is StructType strType)
                typeNameString = GenericsHelper.GetCodegenGenericName(strType.Declaration.Name, _messageHandler);
            else
            {
                // compiler error - could not generate type info 
                _messageHandler.ReportMessage(_currentSourceFile.Text, null, [HapetType.AsString(type)], ErrorCode.Get(CTEN.CouldNotGenerateTypeInfo));
                amount = 0;
                return default;
            }

            LLVMTypeRef interfacesArrayType = LLVMTypeRef.CreateArray(arrayElementType, (uint)(interfaces.Count));
            LLVMValueRef interfacesArray = _module.AddGlobal(interfacesArrayType, $"TypeInterfacesArray::{typeNameString}");
            
            List<LLVMValueRef> intPtrs = new List<LLVMValueRef>(interfaces.Count);
            foreach (var intf in interfaces)
            {
                intPtrs.Add(GenerateTypeConst(intf, true));
            }

            interfacesArray.Initializer = LLVMValueRef.CreateConstNull(interfacesArrayType);
            _builder.BuildStore(LLVMValueRef.CreateConstArray(arrayElementType, intPtrs.ToArray()), interfacesArray);

            amount = interfaces.Count;
            return interfacesArray;
        }
    }
}
