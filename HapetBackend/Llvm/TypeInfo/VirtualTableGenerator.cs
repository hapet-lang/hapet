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
            ClassType parent;
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
                // compiler error - could not generate vtable
                _messageHandler.ReportMessage(_currentSourceFile.Text, null, [HapetType.AsString(type)], ErrorCode.Get(CTEN.CouldNotGenerateVTable));
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
                    _vTablesToInit.Add((type, typeNameString, parent));
                else
                    GenerateInitializerForVTable(type, typeNameString, parent);
            }
            _virtualTableDictionary.Add(type, globConst);

            return globConst;
        }

        private readonly List<(HapetType type, string typeName, ClassType parent)> _vTablesToInit = new List<(HapetType, string, ClassType)>();
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
                GenerateInitializerForVTable(tp.type, tp.typeName, tp.parent);
            }
            _builder.BuildRetVoid();

            return (ltype, lfunc);
        }

        public void GenerateInitializerForVTable(HapetType type, string typeNameString, ClassType parent)
        {
            var virtualTableType = GetVirtualTableType();
            var globConst = _virtualTableDictionary[type];

            // parent param
            var ptrT = LLVMTypeRef.CreatePointer(virtualTableType, 0);
            var nullPtr = LLVMValueRef.CreateConstPointerNull(ptrT);
            LLVMValueRef parentRef = parent == null ? nullPtr : GenerateVirtualTableConst(parent, true);
            // virtual methods
            var virtualMethods = GetVtableArray(type, out int virtualMethodsCount);
            LLVMValueRef virtualMethodsCountRef = LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)virtualMethodsCount);
            // interfaces
            var (interfaces, interfaceOffsets) = GetVtableInterfacesArray(type, out int interfacesCount);
            LLVMValueRef interfacesCountRef = LLVMValueRef.CreateConstInt(_context.Int8Type, (ulong)interfacesCount);

            _builder.BuildStore(LLVMValueRef.CreateConstNamedStruct(virtualTableType,
                new LLVMValueRef[] { parentRef, virtualMethods, virtualMethodsCountRef, interfaces, interfaceOffsets, interfacesCountRef }), globConst);
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
                _messageHandler.ReportMessage(_currentSourceFile.Text, null, [HapetType.AsString(type)], ErrorCode.Get(CTEN.CouldNotGenerateVTable));
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

        private (LLVMValueRef, LLVMValueRef) GetVtableInterfacesArray(HapetType type, out int amount)
        {
            LLVMTypeRef arrayElementType = LLVMTypeRef.CreatePointer(GetVirtualTableType(), 0);
            List<(ClassType, int[])> interfaces = GetAllInterfacesWithOffsetsForVtable(type);
            if (interfaces.Count == 0)
            {
                amount = 0;
                var ptrT = LLVMTypeRef.CreatePointer(arrayElementType, 0);
                var nullPtr = LLVMValueRef.CreateConstPointerNull(ptrT);

                var ptrTInt = LLVMTypeRef.CreatePointer(_context.Int32Type, 0);
                var nullPtrInt = LLVMValueRef.CreateConstPointerNull(ptrTInt);
                return (nullPtr, nullPtrInt);
            }

            string typeNameString;
            if (type is ClassType clsType)
            {
                typeNameString = GenericsHelper.GetCodegenGenericName(clsType.Declaration.Name, _messageHandler);
            }
            else if (type is StructType strType)
            {
                typeNameString = GenericsHelper.GetCodegenGenericName(strType.Declaration.Name, _messageHandler);
            }
            else
            {
                // compiler error - could not generate vtable
                amount = 0;
                _messageHandler.ReportMessage(_currentSourceFile.Text, null, [HapetType.AsString(type)], ErrorCode.Get(CTEN.CouldNotGenerateVTable));
                return (default, default);
            }

            LLVMTypeRef interfacesArrayType = LLVMTypeRef.CreateArray(arrayElementType, (uint)(interfaces.Count));
            LLVMValueRef interfacesArray = _module.AddGlobal(interfacesArrayType, $"VirtualTableInterfacesArray::{typeNameString}");
            LLVMValueRef interfaceOffsetsArray = _module.AddGlobal(LLVMTypeRef.CreateArray(LLVMTypeRef.CreatePointer(_context.Int32Type, 0), (uint)(interfaces.Count)), $"VirtualTableInterfaceOffsetsArray::{typeNameString}");

            List<LLVMValueRef> intPtrs = new List<LLVMValueRef>(interfaces.Count);
            List<LLVMValueRef> offsetArrays = new List<LLVMValueRef>(interfaces.Count);
            foreach (var intf in interfaces)
            {
                intPtrs.Add(GenerateVirtualTableConst(intf.Item1, true));

                LLVMValueRef interfaceOffsetsCurr = _module.AddGlobal(LLVMTypeRef.CreateArray(_context.Int32Type, (uint)(intf.Item2.Length)), $"VirtualTableInterfaceOffsets{offsetArrays.Count}::{typeNameString}");

                interfaceOffsetsCurr.Initializer = LLVMValueRef.CreateConstArray(_context.Int32Type, intf.Item2.Select(x => LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)x)).ToArray());
                interfaceOffsetsCurr.IsGlobalConstant = true;
                offsetArrays.Add(interfaceOffsetsCurr);
            }

            interfacesArray.Initializer = LLVMValueRef.CreateConstNull(interfacesArrayType);
            _builder.BuildStore(LLVMValueRef.CreateConstArray(arrayElementType, intPtrs.ToArray()), interfacesArray);

            interfaceOffsetsArray.Initializer = LLVMValueRef.CreateConstArray(LLVMTypeRef.CreatePointer(_context.Int32Type, 0), offsetArrays.ToArray());
            interfaceOffsetsArray.IsGlobalConstant = true;

            amount = interfaces.Count;
            return (interfacesArray, interfaceOffsetsArray);
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
                _messageHandler.ReportMessage(_currentSourceFile.Text, null, [HapetType.AsString(type)], ErrorCode.Get(CTEN.CouldNotGenerateVTable));
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
                        _messageHandler.ReportMessage(_currentSourceFile.Text, iM.Name, [], ErrorCode.Get(CTEN.VirtualMethodNotFound));

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
