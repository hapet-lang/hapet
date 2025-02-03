using HapetFrontend.Types;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private readonly Dictionary<ClassType, LLVMValueRef> _virtualTableDictionary = new Dictionary<ClassType, LLVMValueRef>();

        #region Type info 
        private unsafe LLVMValueRef GenerateVirtualTableConst(ClassType cls)
        {
            if (_virtualTableDictionary.ContainsKey(cls))
                return _virtualTableDictionary[cls];

            // create if does not exists
            ClassType parent = cls.Declaration.InheritedFrom.FirstOrDefault(x => x.OutType is ClassType clss && !clss.Declaration.IsInterface)?.OutType as ClassType;
            LLVMTypeRef virtualTableType = GetVirtualTableType();

            // parent param
            string typeNameString = cls.Declaration.Name.Name;
            var ptrT = LLVMTypeRef.CreatePointer(virtualTableType, 0);
            var nullPtr = LLVMValueRef.CreateConstPointerNull(ptrT);
            LLVMValueRef parentRef = parent == null ? nullPtr : GenerateVirtualTableConst(parent);
            // virtual methods
            var virtualMethods = GetVtableArray(cls, out int virtualMethodsCount);
            LLVMValueRef virtualMethodsCountRef = LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)virtualMethodsCount);

            var globConst = _module.AddGlobal(virtualTableType, $"VirtualTable::{typeNameString}");
            globConst.Initializer = LLVMValueRef.CreateConstNamedStruct(virtualTableType, new LLVMValueRef[] { parentRef, virtualMethods, virtualMethodsCountRef, nullPtr, nullPtr, virtualMethodsCountRef });
            globConst.Linkage = (LLVMLinkage.LLVMInternalLinkage);
            globConst.IsGlobalConstant = true;

            _virtualTableDictionary.Add(cls, globConst);

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

        private LLVMValueRef GetVtableArray(ClassType cls, out int amount)
        {
            var allVirtualMethods = cls.Declaration.AllVirtualMethods;
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

            LLVMValueRef methodsArray = _module.AddGlobal(LLVMTypeRef.CreateArray(arrayElementType, (uint)(allVirtualMethods.Count)), $"VirtualTableMethodsArray::{cls.Declaration.Name.Name}");
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
        #endregion
    }
}
