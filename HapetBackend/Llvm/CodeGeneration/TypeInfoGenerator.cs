using HapetFrontend.Ast.Declarations;
using HapetFrontend.Helpers;
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
            ClassType parent = cls.Declaration.InheritedFrom.FirstOrDefault(x => x.OutType is ClassType clss && !clss.Declaration.IsInterface)?.OutType as ClassType;
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
            var (interfaces, interfaceOffsets) = GetInterfacesArray(cls, out int interfacesCount);
            LLVMValueRef interfacesCountRef = LLVMValueRef.CreateConstInt(_context.Int8Type, (ulong)interfacesCount);

            var globConst = _module.AddGlobal(typeInfoType, $"TypeInfo::{typeNameString}");
            globConst.Initializer = LLVMValueRef.CreateConstNamedStruct(typeInfoType, new LLVMValueRef[] { typeName, parentRef, interfaces, interfaceOffsets, interfacesCountRef });
            globConst.Linkage = (LLVMLinkage.LLVMInternalLinkage);
            globConst.IsGlobalConstant = true;

            return globConst;
        }

        private LLVMTypeRef _typeInfoType;
        private LLVMTypeRef GetTypeInfoType()
        {
            if (_typeInfoType != null)
                return _typeInfoType;

            // WARN: hard cock
            var typeInfoUnsafeDecl = _currentSourceFile.NamespaceScope.GetSymbolInNamespace("System.Runtime", "TypeInfoUnsafe");
            _typeInfoType = _typeMap[typeInfoUnsafeDecl.Decl.Type.OutType];
            return _typeInfoType;
        }

        private (LLVMValueRef, LLVMValueRef) GetInterfacesArray(ClassType cls, out int amount)
        {
            LLVMTypeRef arrayElementType = LLVMTypeRef.CreatePointer(GetTypeInfoType(), 0);
            List<(ClassType, int)> interfaces = GetAllInterfaces(cls);
            if (interfaces.Count == 0)
            {
                amount = 0;
                var ptrT = LLVMTypeRef.CreatePointer(arrayElementType, 0);
                var nullPtr = LLVMValueRef.CreateConstPointerNull(ptrT);

                var ptrTInt = LLVMTypeRef.CreatePointer(_context.Int32Type, 0);
                var nullPtrInt = LLVMValueRef.CreateConstPointerNull(ptrTInt);
                return (nullPtr, nullPtrInt);
            }

            LLVMValueRef interfacesArray = _module.AddGlobal(LLVMTypeRef.CreateArray(arrayElementType, (uint)(interfaces.Count)), $"TypeInfoInterfacesArray::{cls.Declaration.Name.Name}");
            LLVMValueRef interfaceOffsetsArray = _module.AddGlobal(LLVMTypeRef.CreateArray(_context.Int32Type, (uint)(interfaces.Count)), $"TypeInfoInterfaceOffsetsArray::{cls.Declaration.Name.Name}");
            
            List<LLVMValueRef> intPtrs = new List<LLVMValueRef>(interfaces.Count);
            List<LLVMValueRef> offsets = new List<LLVMValueRef>(interfaces.Count);
            foreach (var intf in interfaces)
            {
                intPtrs.Add(GenerateTypeInfoConst(intf.Item1));
                offsets.Add(LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)intf.Item2));
            }

            interfacesArray.Initializer = LLVMValueRef.CreateConstArray(arrayElementType, intPtrs.ToArray());
            interfacesArray.IsGlobalConstant = true;

            interfaceOffsetsArray.Initializer = LLVMValueRef.CreateConstArray(_context.Int32Type, offsets.ToArray());
            interfaceOffsetsArray.IsGlobalConstant = true;

            amount = interfaces.Count;
            return (interfacesArray, interfaceOffsetsArray);
        }

        // TODO: refactor this cringe
        private List<(ClassType, int)> GetAllInterfaces(ClassType cls, bool includeParent = true)
        {
            List<ClassType> inheritedInterfaces = cls.Declaration.InheritedFrom.Where(x => x.OutType is ClassType).Select(x => x.OutType as ClassType).ToList();

            List<(ClassType, int)> allInterfaces = new List<(ClassType, int)>();

            // get parent class interfaces if the parent exists :)
            int parentStructSize = 0;
            List<(ClassType, int)> parentClassInterfaces = new List<(ClassType, int)>();
            if (cls.Declaration.InheritedFrom.Count > 0 && !(cls.Declaration.InheritedFrom[0].OutType as ClassType).Declaration.IsInterface)
            {
                // if not include parent interfaces - just return its own
                if (includeParent)
                    parentClassInterfaces.AddRange(GetAllInterfaces(cls.Declaration.InheritedFrom[0].OutType as ClassType, true));

                // we need to know the next field after base class fields for proper padding
                HapetType nextElementType = null;
                foreach (var intf in inheritedInterfaces)
                {
                    var interfaceFields = intf.Declaration.Declarations.GetStructFields();
                    if (interfaceFields.Count > 0)
                    {
                        nextElementType = interfaceFields[0].Type.OutType;
                        break;
                    }
                }
                // if there were no interfaces or they were without fields - try get our own field
                if (nextElementType == null)
                {
                    var outFields = cls.Declaration.Declarations.GetStructFields();
                    if (outFields.Count > 0)
                    {
                        nextElementType = outFields[0].Type.OutType;
                    }
                }

                parentStructSize = (cls.Declaration.InheritedFrom[0].OutType as ClassType).GetStructSizeForInterfaceOffset(nextElementType);
            }
            allInterfaces.AddRange(parentClassInterfaces);

            // to store current offset to the interface fields
            int currentTypeOffset = parentStructSize;

            // go all over the curr class interfaces
            for (int i = 0; i < inheritedInterfaces.Count; ++i) 
            {
                var inh = inheritedInterfaces[i];

                // skip the parent class - we need only interfaces :)
                if (!inh.Declaration.IsInterface)
                    continue;

                // skip interfaces that we already implemented in parent classes
                if (parentClassInterfaces.Any(x => x.Item1 == inh))
                    continue;

                allInterfaces.Add((inh, currentTypeOffset));

                // we need to know the next field after base class fields for proper padding
                HapetType nextElementType = null;
                foreach (var intf in inheritedInterfaces.Skip(i + 1))
                {
                    var interfaceFields = intf.Declaration.Declarations.GetStructFields();
                    if (interfaceFields.Count > 0)
                    {
                        nextElementType = interfaceFields[0].Type.OutType;
                        break;
                    }
                }
                // if there were no interfaces or they were without fields - try get our own field
                if (nextElementType == null)
                {
                    var outFields = cls.Declaration.Declarations.GetStructFields();
                    if (outFields.Count > 0)
                    {
                        nextElementType = outFields[0].Type.OutType;
                    }
                }

                // update offset
                currentTypeOffset += inh.GetStructSizeForInterfaceOffset(nextElementType);
            }

            return allInterfaces;
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
