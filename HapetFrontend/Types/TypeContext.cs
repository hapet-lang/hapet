using System;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Parsing;

namespace HapetFrontend.Types
{
    public class TypeContext
    {
        public int PointerSize { get; set; }
        public StringType StringTypeInstance { get; private set; } = new StringType(null);
        public IntPtrType IntPtrTypeInstance { get; private set; } = new IntPtrType(null);
        public PtrDiffType PtrDiffTypeInstance { get; private set; } = new PtrDiffType(null);
        public Dictionary<AstDelegateDecl, DelegateType> DelegateTypeInstances { get; private set; } = new Dictionary<AstDelegateDecl, DelegateType>();
        public Dictionary<HapetType, ArrayType> ArrayTypeInstances { get; private set; } = new Dictionary<HapetType, ArrayType>();
        public ArrayType GetArrayType(HapetType targetType)
        {
            var key = targetType;
            if (ArrayTypeInstances.TryGetValue(key, out ArrayType value))
            {
                return value;
            }
            ArrayType empty = new ArrayType(targetType, null);
            ArrayTypeInstances[targetType] = empty;
            return empty;
        }
        public Dictionary<(int, bool), IntType> IntTypeInstances { get; private set; } = new Dictionary<(int, bool), IntType>();
        public IntType GetIntType(int sizeInBytes, bool signed)
        {
            var key = (sizeInBytes, signed);
            if (IntTypeInstances.TryGetValue(key, out IntType value))
            {
                return value;
            }
            IntType empty = new IntType(null, signed);
            IntTypeInstances[key] = empty;
            return empty;
        }
    }
}
