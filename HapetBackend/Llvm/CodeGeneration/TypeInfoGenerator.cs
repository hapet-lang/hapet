using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Types;
using LLVMSharp.Interop;
using System.Xml.Linq;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private readonly Dictionary<HapetType, LLVMValueRef> _typeInfoDictionary = new Dictionary<HapetType, LLVMValueRef>();

        #region Type info 
        private unsafe LLVMValueRef GenerateTypeInfoConst(HapetType type)
        {
            if (_typeInfoDictionary.TryGetValue(type, out LLVMValueRef value))
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
                parent = strType.Declaration.InheritedFrom.FirstOrDefault(x => x.OutType is ClassType clss && !clss.Declaration.IsInterface)?.OutType as ClassType;
                typeNameString = strType.Declaration.Name.Name;
            }
            else
            {
                // compiler error - could not generate type info 
                _messageHandler.ReportMessage(_currentSourceFile.Text, null, [HapetType.AsString(type)], ErrorCode.Get(CTEN.CouldNotGenerateTypeInfo));
                return default;
            }

            LLVMTypeRef typeInfoType = GetTypeInfoType();

            // name param
            LLVMValueRef typeName = _module.AddGlobal(LLVMTypeRef.CreateArray(_context.Int8Type, (uint)(typeNameString.Length + 1)), $"TypeInfoName::{typeNameString}");
            typeName.Initializer = _context.GetConstString(typeNameString, false);
            typeName.IsGlobalConstant = true;
            // parent param
            var ptrT = LLVMTypeRef.CreatePointer(typeInfoType, 0);
            var nullPtr = LLVMValueRef.CreateConstPointerNull(ptrT);
            LLVMValueRef parentRef = parent == null ? nullPtr : GenerateTypeInfoConst(parent);
            // interfaces
            var (interfaces, interfaceOffsets) = GetInterfacesArray(type, out int interfacesCount);
            LLVMValueRef interfacesCountRef = LLVMValueRef.CreateConstInt(_context.Int8Type, (ulong)interfacesCount);

            var globConst = _module.AddGlobal(typeInfoType, $"TypeInfo::{typeNameString}");
            globConst.Initializer = LLVMValueRef.CreateConstNamedStruct(typeInfoType, new LLVMValueRef[] { typeName, parentRef, interfaces, interfaceOffsets, interfacesCountRef });
            globConst.Linkage = (LLVMLinkage.LLVMInternalLinkage);
            globConst.IsGlobalConstant = true;

            _typeInfoDictionary.Add(type, globConst);

            return globConst;
        }

        private LLVMTypeRef _typeInfoType;
        private LLVMTypeRef GetTypeInfoType()
        {
            if (_typeInfoType != null)
                return _typeInfoType;

            // WARN: hard cock
            var typeInfoUnsafeDecl = _currentSourceFile.NamespaceScope.GetSymbolInNamespace("System.Runtime", new AstIdExpr("TypeInfoUnsafe"));
            _typeInfoType = _typeMap[typeInfoUnsafeDecl.Decl.Type.OutType];
            return _typeInfoType;
        }

        private (LLVMValueRef, LLVMValueRef) GetInterfacesArray(HapetType type, out int amount)
        {
            LLVMTypeRef arrayElementType = LLVMTypeRef.CreatePointer(GetTypeInfoType(), 0);
            List<(ClassType, int[])> interfaces = GetAllInterfacesWithOffsets(type);
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
                // compiler error - could not generate type info 
                _messageHandler.ReportMessage(_currentSourceFile.Text, null, [HapetType.AsString(type)], ErrorCode.Get(CTEN.CouldNotGenerateTypeInfo));
                amount = 0;
                return (default, default);
            }

            LLVMValueRef interfacesArray = _module.AddGlobal(LLVMTypeRef.CreateArray(arrayElementType, (uint)(interfaces.Count)), $"TypeInfoInterfacesArray::{typeNameString}");
            LLVMValueRef interfaceOffsetsArray = _module.AddGlobal(LLVMTypeRef.CreateArray(LLVMTypeRef.CreatePointer(_context.Int32Type, 0), (uint)(interfaces.Count)), $"TypeInfoInterfaceOffsetsArray::{typeNameString}");
            
            List<LLVMValueRef> intPtrs = new List<LLVMValueRef>(interfaces.Count);
            List<LLVMValueRef> offsetArrays = new List<LLVMValueRef>(interfaces.Count);
            foreach (var intf in interfaces)
            {
                intPtrs.Add(GenerateTypeInfoConst(intf.Item1));

                LLVMValueRef interfaceOffsetsCurr = _module.AddGlobal(LLVMTypeRef.CreateArray(_context.Int32Type, (uint)(intf.Item2.Length)), $"TypeInfoInterfaceOffsets{offsetArrays.Count}::{typeNameString}");

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

        private List<(ClassType, int[])> GetAllInterfacesWithOffsets(HapetType type)
        {
            // to calc offset up to the required element
            int GetOffsetTo(List<AstVarDecl> allVars, int ind)
            {
                int totalSize = 0;
                // go all over the fields and calc the size
                for (int i = 0; i < ind; ++i)
                {
                    var field = allVars[i];
                    var fieldType = field.Type.OutType;

                    int fieldAlignment = fieldType.GetAlignment() == 0 ? fieldType.GetSize() : fieldType.GetAlignment();
                    int padding = (fieldAlignment - (totalSize % fieldAlignment)) % fieldAlignment;  // Alignment
                    totalSize += padding;  // Add padding for the alignment
                    totalSize += fieldType.GetSize();  // Add field size

                    // if it is the last iteration - add padding
                    if (i + 1 == ind)
                    {
                        var nextElement = allVars[i + 1].Type.OutType;
                        int fieldAlignmentLast = nextElement.GetAlignment() == 0 ? nextElement.GetSize() : nextElement.GetAlignment();
                        int paddingLast = (fieldAlignmentLast - (totalSize % fieldAlignmentLast)) % fieldAlignmentLast;  // Alignment
                        totalSize += paddingLast;  // Add padding for the alignment
                    }
                }
                return totalSize;
            }

            List<(ClassType, int[])> allInterfacesWithOffsets = new List<(ClassType, int[])>();

            List<AstVarDecl> allClassFields;
            if (type is ClassType clsType)
                allClassFields = clsType.Declaration.GetAllRawFields();
            else if (type is StructType strType)
                allClassFields = strType.Declaration.GetAllRawFields();
            else
            {
                _messageHandler.ReportMessage(_currentSourceFile.Text, null, [HapetType.AsString(type)], ErrorCode.Get(CTEN.CouldNotGenerateTypeInfo));
                return new List<(ClassType, int[])>(); // compiler error
            }

            var allInterfaces = GetAllInterfaces(type, true);
            foreach (var intrf in allInterfaces)
            {
                List<int> offsets = new List<int>();
                foreach (var iF in intrf.Declaration.GetAllRawFields())
                {
                    allClassFields.GetSameDeclByTypeAndName(iF, out var fIndex);
                    int offset = GetOffsetTo(allClassFields, fIndex);
                    offsets.Add(offset);
                }
                allInterfacesWithOffsets.Add((intrf, offsets.ToArray()));
            }

            return allInterfacesWithOffsets;
        }

        private static List<ClassType> GetAllInterfaces(HapetType type, bool includeParent = true)
        {
            List<ClassType> inheritedInterfaces;
            if (type is ClassType clsType)
                inheritedInterfaces = clsType.Declaration.InheritedFrom.Where(x => x.OutType is ClassType).Select(x => x.OutType as ClassType).ToList();
            else if (type is StructType strType)
                inheritedInterfaces = strType.Declaration.InheritedFrom.Where(x => x.OutType is ClassType).Select(x => x.OutType as ClassType).ToList();
            else
                return new List<ClassType>();

            List<ClassType> allInterfaces = new List<ClassType>();

            // get parent class interfaces if the parent exists :)
            List<ClassType> parentClassInterfaces = new List<ClassType>();
            if (inheritedInterfaces.Count > 0 && !inheritedInterfaces[0].Declaration.IsInterface)
            {
                // if include parent interfaces 
                if (includeParent)
                    parentClassInterfaces.AddRange(GetAllInterfaces(inheritedInterfaces[0], true));
            }
            allInterfaces.AddRange(parentClassInterfaces);

            // go all over the curr class interfaces
            for (int i = 0; i < inheritedInterfaces.Count; ++i) 
            {
                var inh = inheritedInterfaces[i];

                // skip the parent class - we need only interfaces :)
                if (!inh.Declaration.IsInterface)
                    continue;

                // skip interfaces that we already implemented
                if (allInterfaces.Any(x => x == inh))
                    continue;

                // get all interfaces of the curr one
                var parentParents = GetAllInterfaces(inh, true);
                foreach (var pp in parentParents)
                {
                    // skip interfaces that we already implemented
                    if (allInterfaces.Any(x => x == pp))
                        continue;
                    allInterfaces.Add(pp);
                }
                // add it
                allInterfaces.Add(inh);
            }

            return allInterfaces;
        }
        #endregion

        #region Loaders
        private LLVMValueRef GetTypeInfoPtr(LLVMTypeRef strType, LLVMValueRef ptrToStr)
        {
            var intPtrT = HapetType.CurrentTypeContext.IntPtrTypeInstance;
            var zeroRef = LLVMValueRef.CreateConstInt(_context.Int32Type, 0);
            var ptrToFullTypeInfo = _builder.BuildGEP2(strType, ptrToStr, new LLVMValueRef[] { zeroRef, zeroRef }, "fullTypeInfo");
            var ptrToFullTypeInfoLoaded = _builder.BuildLoad2(LLVMTypeRef.CreatePointer(HapetTypeToLLVMType(intPtrT), 0), ptrToFullTypeInfo, "fullTypeInfoLoaded");
            var ptrToTypeInfo = _builder.BuildGEP2(LLVMTypeRef.CreatePointer(HapetTypeToLLVMType(intPtrT), 0), ptrToFullTypeInfoLoaded, new LLVMValueRef[] { zeroRef }, "typeInfo");
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
