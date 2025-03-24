using HapetFrontend.Ast.Declarations;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Types;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private readonly Dictionary<HapetType, LLVMValueRef> _virtualTableDictionary = new Dictionary<HapetType, LLVMValueRef>();

        #region Type info 
        private unsafe LLVMValueRef GenerateVirtualTableConst(HapetType type)
        {
            if (_virtualTableDictionary.TryGetValue(type, out LLVMValueRef value))
                return value;

            // create if does not exists
            ClassType parent;
            string typeNameString;
            if (type is ClassType clsType)
            {
                parent = clsType.Declaration.InheritedFrom.FirstOrDefault(x => x.OutType is ClassType clss && !clss.Declaration.IsInterface)?.OutType as ClassType;
                typeNameString = clsType.Declaration.Name.Name;
            }
            else if (type is StructType strType)
            {
                // TODO: struct parent
                parent = strType.Declaration.InheritedFrom.FirstOrDefault(x => x.OutType is ClassType clss && !clss.Declaration.IsInterface)?.OutType as ClassType;
                typeNameString = strType.Declaration.Name.Name;
            }
            else
            {
                // TODO: compiler error - could not generate type info 
                return default;
            }

            LLVMTypeRef virtualTableType = GetVirtualTableType();

            // parent param
            var ptrT = LLVMTypeRef.CreatePointer(virtualTableType, 0);
            var nullPtr = LLVMValueRef.CreateConstPointerNull(ptrT);
            LLVMValueRef parentRef = parent == null ? nullPtr : GenerateVirtualTableConst(parent);
            // virtual methods
            var virtualMethods = GetVtableArray(type, out int virtualMethodsCount);
            LLVMValueRef virtualMethodsCountRef = LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)virtualMethodsCount);
            // interfaces
            var (interfaces, interfaceOffsets) = GetVtableInterfacesArray(type, out int interfacesCount);
            LLVMValueRef interfacesCountRef = LLVMValueRef.CreateConstInt(_context.Int8Type, (ulong)interfacesCount);

            var globConst = _module.AddGlobal(virtualTableType, $"VirtualTable::{typeNameString}");
            globConst.Initializer = LLVMValueRef.CreateConstNamedStruct(virtualTableType, new LLVMValueRef[] { parentRef, virtualMethods, virtualMethodsCountRef, interfaces, interfaceOffsets, interfacesCountRef });
            globConst.Linkage = (LLVMLinkage.LLVMInternalLinkage);
            globConst.IsGlobalConstant = true;

            _virtualTableDictionary.Add(type, globConst);

            return globConst;
        }

        private LLVMTypeRef _virtualTableType;
        private LLVMTypeRef GetVirtualTableType()
        {
            if (_virtualTableType != null)
                return _virtualTableType;

            // WARN: hard cock
            var virtualTableUnsafeDecl = _currentSourceFile.NamespaceScope.GetSymbolInNamespace("System.Runtime", "VirtualTableUnsafe");
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
                typeNameString = classType.Declaration.Name.Name;
            }
            else if (type is StructType structType)
            {
                allVirtualMethods = structType.Declaration.AllVirtualMethods;
                typeNameString = structType.Declaration.Name.Name;
            }
            else
            {
                amount = 0; // TODO: compiler error
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
                    methPtrs.Add(_valueMap[mtd.GetSymbol]);
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
                typeNameString = clsType.Declaration.Name.Name;
            }
            else if (type is StructType strType)
            {
                typeNameString = strType.Declaration.Name.Name;
            }
            else
            {
                // TODO: compiler error - could not generate type info 
                amount = 0;
                return (default, default);
            }

            LLVMValueRef interfacesArray = _module.AddGlobal(LLVMTypeRef.CreateArray(arrayElementType, (uint)(interfaces.Count)), $"VirtualTableInterfacesArray::{typeNameString}");
            LLVMValueRef interfaceOffsetsArray = _module.AddGlobal(LLVMTypeRef.CreateArray(LLVMTypeRef.CreatePointer(_context.Int32Type, 0), (uint)(interfaces.Count)), $"VirtualTableInterfaceOffsetsArray::{typeNameString}");

            List<LLVMValueRef> intPtrs = new List<LLVMValueRef>(interfaces.Count);
            List<LLVMValueRef> offsetArrays = new List<LLVMValueRef>(interfaces.Count);
            foreach (var intf in interfaces)
            {
                intPtrs.Add(GenerateVirtualTableConst(intf.Item1));

                LLVMValueRef interfaceOffsetsCurr = _module.AddGlobal(LLVMTypeRef.CreateArray(_context.Int32Type, (uint)(intf.Item2.Length)), $"VirtualTableInterfaceOffsets{offsetArrays.Count}::{typeNameString}");

                interfaceOffsetsCurr.Initializer = LLVMValueRef.CreateConstArray(_context.Int32Type, intf.Item2.Select(x => LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)x)).ToArray());
                interfaceOffsetsCurr.IsGlobalConstant = true;
                offsetArrays.Add(interfaceOffsetsCurr);
            }

            interfacesArray.Initializer = LLVMValueRef.CreateConstArray(arrayElementType, intPtrs.ToArray());
            interfacesArray.IsGlobalConstant = true;

            interfaceOffsetsArray.Initializer = LLVMValueRef.CreateConstArray(LLVMTypeRef.CreatePointer(_context.Int32Type, 0), offsetArrays.ToArray());
            interfaceOffsetsArray.IsGlobalConstant = true;

            amount = interfaces.Count;
            return (interfacesArray, interfaceOffsetsArray);
        }

        private static List<(ClassType, int[])> GetAllInterfacesWithOffsetsForVtable(HapetType type)
        {
            List<(ClassType, int[])> allInterfacesWithOffsets = new List<(ClassType, int[])>();

            List<AstFuncDecl> allClassVirtuals;
            if (type is ClassType clsType)
                allClassVirtuals = clsType.Declaration.AllVirtualMethods;
            else if (type is StructType strType)
                allClassVirtuals = strType.Declaration.AllVirtualMethods;
            else
                return new List<(ClassType, int[])>(); // TODO: compiler error

            var allInterfaces = GetAllInterfaces(type, true);

            foreach (var intrf in allInterfaces)
            {
                List<int> offsets = new List<int>();
                for (int i = 0; i < intrf.Declaration.AllVirtualMethods.Count; ++i)
                {
                    var iM = intrf.Declaration.AllVirtualMethods[i];
                    var m = allClassVirtuals.GetSameByNameAndTypes(iM, out int index);
                    // TODO: check m for not null and error if null (compiler error)

                    offsets.Add(index);
                }
                allInterfacesWithOffsets.Add((intrf, offsets.ToArray()));
            }

            return allInterfacesWithOffsets;
        }
        #endregion
    }
}
