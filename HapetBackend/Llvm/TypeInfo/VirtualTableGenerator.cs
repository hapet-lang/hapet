using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Types;
using LLVMSharp.Interop;
using System;
using System.Xml.Linq;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private readonly Dictionary<HapetType, LLVMValueRef> _virtualTableDictionary = new Dictionary<HapetType, LLVMValueRef>();

        #region Type info 
        private unsafe LLVMValueRef GenerateVirtualTableConst(HapetType type, bool initialize = false)
        {
            if (_virtualTableDictionary.TryGetValue(type, out LLVMValueRef value))
                return value;

            // create if does not exists
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
                // compiler error - could not generate vtable
                _messageHandler.ReportMessage(_currentSourceFile, null, [HapetType.AsString(type)], ErrorCode.Get(CTEN.CouldNotGenerateVTable));
                return default;
            }

            LLVMTypeRef virtualTableType = GetVirtualTableType();
            string name = $"VirtualTable::{typeNameString}";
            var globConst = _module.AddGlobal(virtualTableType, name);
            globConst.Linkage = LLVMLinkage.LLVMExternalLinkage;

            if (decl.IsImported && !decl.IsImplOfGeneric && !(decl.IsNestedDecl && decl.ParentDecl.IsImplOfGeneric))
            {
                globConst.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLImportStorageClass;
            }
            else
            {
                globConst.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLExportStorageClass;
                globConst.Initializer = LLVMValueRef.CreateConstNull(virtualTableType);

                if (!initialize)
                    _vTablesToInit.Add(type);
                else
                    GenerateInitializerForVTable(type);
            }
            _virtualTableDictionary.Add(type, globConst);

            return globConst;
        }

        private readonly List<HapetType> _vTablesToInit = new List<HapetType>();
        private (LLVMTypeRef, LLVMValueRef) CreateVTableInitializer()
        {
            // create a func to init all the v tables
            var ltype = LLVMTypeRef.CreateFunction(HapetTypeToLLVMType(HapetType.CurrentTypeContext.VoidTypeInstance), [], false);
            var lfunc = _module.AddFunction($"{_compiler.CurrentProjectSettings.ProjectName}_vtable_initer", ltype);
            lfunc.Linkage = LLVMLinkage.LLVMExternalLinkage;

            // make check that is already inited
            var entry = lfunc.AppendBasicBlockInContext(_context, "entry");

            _builder.PositionAtEnd(entry);

            // make all inites here
            foreach (var tp in _vTablesToInit)
            {
                GenerateInitializerForVTable(tp);
            }
            _builder.BuildRetVoid();

            return (ltype, lfunc);
        }

        public void GenerateInitializerForVTable(HapetType type)
        {
            var virtualTableType = GetVirtualTableType();
            var globConst = _virtualTableDictionary[type];

            // virtual methods
            var virtualMethods = GetVtableArray(type, out int virtualMethodsCount);
            LLVMValueRef virtualMethodsCountRef = LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)virtualMethodsCount);
            // interfaces
            var interfaceOffsets = GetVtableInterfaceOffsetsArray(type, out int _);

            _builder.BuildStore(LLVMValueRef.CreateConstNamedStruct(virtualTableType,
                new LLVMValueRef[] { virtualMethods, virtualMethodsCountRef, interfaceOffsets }), globConst);
        }

        private LLVMTypeRef _virtualTableType;
        private LLVMTypeRef GetVirtualTableType()
        {
            if (_virtualTableType != null)
                return _virtualTableType;

            // WARN: hard cock
            var virtualTableUnsafeDecl = _currentSourceFile.NamespaceScope.GetSymbolInNamespace("System.Runtime", new AstIdExpr("VirtualTableUnsafe"));
            _virtualTableType = _typeMap[virtualTableUnsafeDecl.Decl.Type.OutType];
            return _virtualTableType;
        }

        private LLVMValueRef GetVtableArray(HapetType type, out int amount)
        {
            List<AstFuncDecl> allVirtualMethods;
            string typeNameString;
            if (type is ClassType classType)
            {
                allVirtualMethods = classType.Declaration.AllVirtualMethods;
                typeNameString = GenericsHelper.GetCodegenGenericName(classType.Declaration.Name, _messageHandler);
            }
            else if (type is StructType structType)
            {
                allVirtualMethods = structType.Declaration.AllVirtualMethods;
                typeNameString = GenericsHelper.GetCodegenGenericName(structType.Declaration.Name, _messageHandler);
            }
            else
            {
                amount = 0; // compiler error
                _messageHandler.ReportMessage(_currentSourceFile, null, [HapetType.AsString(type)], ErrorCode.Get(CTEN.CouldNotGenerateVTable));
                return default;
            }

            LLVMTypeRef arrayElementType = LLVMTypeRef.CreatePointer(_context.Int8Type, 0);

            // nullptr t
            var ptrT = LLVMTypeRef.CreatePointer(arrayElementType, 0);
            var nullPtr = LLVMValueRef.CreateConstPointerNull(ptrT);

            // return nullptr if there are no virtual methods
            if (allVirtualMethods.Count == 0)
            {
                amount = 0;
                return nullPtr;
            }

            LLVMValueRef methodsArray = _module.AddGlobal(LLVMTypeRef.CreateArray(arrayElementType, (uint)(allVirtualMethods.Count)), $"VirtualTableMethodsArray::{typeNameString}");
            List<LLVMValueRef> methPtrs = new List<LLVMValueRef>(allVirtualMethods.Count);
            foreach (var mtd in allVirtualMethods)
            {
                // if it is an abstract method - just add nullptr
                if (mtd.SpecialKeys.Contains(HapetFrontend.Parsing.TokenType.KwAbstract))
                    methPtrs.Add(nullPtr);
                else
                    methPtrs.Add(_valueMap[mtd.Symbol]);
            }
            methodsArray.Initializer = LLVMValueRef.CreateConstArray(arrayElementType, methPtrs.ToArray());
            methodsArray.IsGlobalConstant = true;

            amount = allVirtualMethods.Count;
            return methodsArray;
        }

        private LLVMValueRef GetVtableInterfaceOffsetsArray(HapetType type, out int amount)
        {
            LLVMTypeRef arrayElementType = LLVMTypeRef.CreatePointer(GetVirtualTableType(), 0);
            List<(ClassType, int[])> interfaces = GetAllInterfacesWithOffsetsForVtable(type);
            if (interfaces.Count == 0)
            {
                amount = 0;
                var ptrTInt = LLVMTypeRef.CreatePointer(_context.Int32Type, 0);
                var nullPtrInt = LLVMValueRef.CreateConstPointerNull(ptrTInt);
                return nullPtrInt;
            }

            string typeNameString;
            if (type is ClassType clsType)
                typeNameString = GenericsHelper.GetCodegenGenericName(clsType.Declaration.Name, _messageHandler);
            else if (type is StructType strType)
                typeNameString = GenericsHelper.GetCodegenGenericName(strType.Declaration.Name, _messageHandler);
            else
            {
                // compiler error - could not generate vtable
                amount = 0;
                _messageHandler.ReportMessage(_currentSourceFile, null, [HapetType.AsString(type)], ErrorCode.Get(CTEN.CouldNotGenerateVTable));
                return default;
            }
            LLVMValueRef interfaceOffsetsArray = _module.AddGlobal(LLVMTypeRef.CreateArray(LLVMTypeRef.CreatePointer(_context.Int32Type, 0), (uint)(interfaces.Count)), $"VirtualTableInterfaceOffsetsArray::{typeNameString}");
            List<LLVMValueRef> offsetArrays = new List<LLVMValueRef>(interfaces.Count);
            foreach (var intf in interfaces)
            {
                LLVMValueRef interfaceOffsetsCurr = _module.AddGlobal(LLVMTypeRef.CreateArray(_context.Int32Type, (uint)(intf.Item2.Length)), $"VirtualTableInterfaceOffsets{offsetArrays.Count}::{typeNameString}");

                interfaceOffsetsCurr.Initializer = LLVMValueRef.CreateConstArray(_context.Int32Type, intf.Item2.Select(x => LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)x)).ToArray());
                interfaceOffsetsCurr.IsGlobalConstant = true;
                offsetArrays.Add(interfaceOffsetsCurr);
            }

            interfaceOffsetsArray.Initializer = LLVMValueRef.CreateConstArray(LLVMTypeRef.CreatePointer(_context.Int32Type, 0), offsetArrays.ToArray());
            interfaceOffsetsArray.IsGlobalConstant = true;

            amount = interfaces.Count;
            return interfaceOffsetsArray;
        }

        private List<(ClassType, int[])> GetAllInterfacesWithOffsetsForVtable(HapetType type)
        {
            List<(ClassType, int[])> allInterfacesWithOffsets = new List<(ClassType, int[])>();

            List<AstFuncDecl> allClassVirtuals;
            if (type is ClassType clsType)
                allClassVirtuals = clsType.Declaration.AllVirtualMethods;
            else if (type is StructType strType)
                allClassVirtuals = strType.Declaration.AllVirtualMethods;
            else
            {
                _messageHandler.ReportMessage(_currentSourceFile, null, [HapetType.AsString(type)], ErrorCode.Get(CTEN.CouldNotGenerateVTable));
                return new List<(ClassType, int[])>(); // compiler error
            }

            var allInterfaces = GetAllInterfaces(type, true);

            foreach (var intrf in allInterfaces)
            {
                List<int> offsets = new List<int>();
                for (int i = 0; i < intrf.Declaration.AllVirtualMethods.Count; ++i)
                {
                    var savedFile = _currentSourceFile;

                    var iM = intrf.Declaration.AllVirtualMethods[i];
                    _currentSourceFile = iM.SourceFile;
                    var m = allClassVirtuals.GetSameByNameAndTypes(iM, out int index);
                    // check m for not null and error if null (compiler error)
                    if (m == null)
                        _messageHandler.ReportMessage(_currentSourceFile, iM.Name, [], ErrorCode.Get(CTEN.VirtualMethodNotFound));

                    offsets.Add(index);

                    _currentSourceFile = savedFile;
                }
                allInterfacesWithOffsets.Add((intrf, offsets.ToArray()));
            }

            return allInterfacesWithOffsets;
        }
        #endregion
    }
}
