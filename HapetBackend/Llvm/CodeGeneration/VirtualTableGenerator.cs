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
            var ptrT = LLVMTypeRef.CreatePointer(virtualTableType, 0);
            var nullPtr = LLVMValueRef.CreateConstPointerNull(ptrT);
            LLVMValueRef parentRef = parent == null ? nullPtr : GenerateVirtualTableConst(parent);
            // virtual methods
            var virtualMethods = GetInterfacesArray(cls, out int virtualMethodsCount);
            LLVMValueRef virtualMethodsCountRef = LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)virtualMethodsCount);
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
        #endregion
    }
}
