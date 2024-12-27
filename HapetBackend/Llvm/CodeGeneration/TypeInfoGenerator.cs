using HapetFrontend.Types;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private readonly Dictionary<ClassType, LLVMValueRef> _typeInfoDictionary = new Dictionary<ClassType, LLVMValueRef>();

        #region Type info 
        private unsafe LLVMValueRef GenerateTypeInfoConst(ClassType cls)
        {
            if (_typeInfoDictionary.ContainsKey(cls))
                return _typeInfoDictionary[cls];

            // create if does not exists
            ClassType parent = cls.Declaration.InheritedFrom.FirstOrDefault(x => x.OutType is ClassType clss && !clss.IsInterface)?.OutType as ClassType;
            LLVMTypeRef typeInfoType = GetTypeInfoType();

            // name param
            string typeNameString = cls.Declaration.Name.Name;
            LLVMValueRef typeName = _module.AddGlobal(LLVMTypeRef.CreateArray(_context.Int8Type, (uint)(typeNameString.Length + 1)), $"TypeInfoName::{typeNameString}");
            typeName.Initializer = _context.GetConstString(typeNameString, false);
            typeName.IsGlobalConstant = true;
            // parent param
            var ptrT = LLVMTypeRef.CreatePointer(typeInfoType, 0);
            var nullPtr = LLVMValueRef.CreateConstPointerNull(ptrT);
            LLVMValueRef parentRef = parent == null ? nullPtr : GenerateTypeInfoConst(parent);
            // interfaces
            LLVMValueRef interfaces = GetInterfacesArray(cls, out int interfacesCount);
            LLVMValueRef interfacesCountRef = LLVMValueRef.CreateConstInt(_context.Int8Type, (ulong)interfacesCount);

            var globConst = _module.AddGlobal(typeInfoType, $"TypeInfo::{typeNameString}");
            globConst.Initializer = LLVMValueRef.CreateConstNamedStruct(typeInfoType, new LLVMValueRef[] { typeName, parentRef, interfaces, interfacesCountRef });
            globConst.Linkage = (LLVMLinkage.LLVMInternalLinkage);
            globConst.IsGlobalConstant = true;

            return globConst;
        }

        private LLVMTypeRef _typeInfoType;
        private LLVMTypeRef GetTypeInfoType()
        {
            if (_typeInfoType != null)
                return _typeInfoType;

            _typeInfoType = _context.CreateNamedStruct($"type.info");
            var typeName = LLVMTypeRef.CreatePointer(_context.Int8Type, 0); // name
            var parent = LLVMTypeRef.CreatePointer(_typeInfoType, 0); // parent
            var interfaces = LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(_typeInfoType, 0), 0); // interfaces
            var interfaceCount = _context.Int8Type; // interfaceCount
            _typeInfoType.StructSetBody(new LLVMTypeRef[] { typeName, parent, interfaces, interfaceCount }, false);
            return _typeInfoType;
        }

        private LLVMValueRef GetInterfacesArray(ClassType cls, out int amount)
        {
            LLVMTypeRef arrayElementType = LLVMTypeRef.CreatePointer(GetTypeInfoType(), 0);
            List<ClassType> interfaces = cls.Declaration.InheritedFrom.Where(x => x.OutType is ClassType clss && clss.IsInterface).Select(x => x.OutType as ClassType).ToList();
            if (interfaces.Count == 0)
            {
                amount = 0;
                var ptrT = LLVMTypeRef.CreatePointer(arrayElementType, 0);
                var nullPtr = LLVMValueRef.CreateConstPointerNull(ptrT);
                return nullPtr;
            }

            LLVMValueRef interfacesArray = _module.AddGlobal(LLVMTypeRef.CreateArray(arrayElementType, (uint)(interfaces.Count)), $"TypeInfoInterfacesArray::{cls.Declaration.Name.Name}");
            List<LLVMValueRef> intPtrs = new List<LLVMValueRef>(interfaces.Count);
            foreach (var intf in interfaces)
            {
                intPtrs.Add(GenerateTypeInfoConst(intf));
            }
            interfacesArray.Initializer = LLVMValueRef.CreateConstArray(arrayElementType, intPtrs.ToArray());
            interfacesArray.IsGlobalConstant = true;

            amount = interfaces.Count;
            return interfacesArray;
        }
        #endregion

        #region Loaders
        private LLVMValueRef GetTypeInfoPtr(LLVMTypeRef strType, LLVMValueRef ptrToStr)
        {
            var zeroRef = LLVMValueRef.CreateConstInt(_context.Int32Type, 0);
            var ptrToFullTypeInfo = _builder.BuildGEP2(strType, ptrToStr, new LLVMValueRef[] { zeroRef, zeroRef }, "fullTypeInfo");
            var ptrToFullTypeInfoLoaded = _builder.BuildLoad2(LLVMTypeRef.CreatePointer(HapetTypeToLLVMType(IntPtrType.Instance), 0), ptrToFullTypeInfo, "fullTypeInfoLoaded");
            var ptrToTypeInfo = _builder.BuildGEP2(LLVMTypeRef.CreatePointer(HapetTypeToLLVMType(IntPtrType.Instance), 0), ptrToFullTypeInfoLoaded, new LLVMValueRef[] { zeroRef }, "typeInfo");
            var ptrToTypeInfoLoaded = _builder.BuildLoad2(LLVMTypeRef.CreatePointer(GetTypeInfoType(), 0), ptrToTypeInfo, "typeInfoLoaded");
            return ptrToTypeInfoLoaded;
        }

        private LLVMValueRef GetParentTypeInfoPtr(LLVMValueRef ptrToTypeInfo)
        {
            var zeroRef = LLVMValueRef.CreateConstInt(_context.Int32Type, 0);
            var oneRef = LLVMValueRef.CreateConstInt(_context.Int32Type, 1);
            var ptrToTypeInfoParent = _builder.BuildGEP2(GetTypeInfoType(), ptrToTypeInfo, new LLVMValueRef[] { zeroRef, oneRef }, "typeInfo");
            var ptrToTypeInfoLoadedParent = _builder.BuildLoad2(LLVMTypeRef.CreatePointer(GetTypeInfoType(), 0), ptrToTypeInfoParent, "typeInfoLoaded");
            return ptrToTypeInfoLoadedParent;
        }
        #endregion
    }
}
