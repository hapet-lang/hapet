using System;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Parsing;

namespace HapetFrontend.Types
{
    public class TypeContext
    {
        public int PointerSize { get; set; }
        public ClassType ObjectTypeInstance { get; private set; } = new ClassType(null);
        public VoidType VoidTypeInstance { get; private set; } = new VoidType(null);
        public PointerType PtrToVoidType { get; private set; }
        public BoolType BoolTypeInstance { get; private set; } = new BoolType(null);
        public CharType CharTypeInstance { get; private set; } = new CharType(null, 2); // default is 2 bytes long char
        public StringType StringTypeInstance { get; private set; } = new StringType(null);
        public IntPtrType IntPtrTypeInstance { get; private set; } = new IntPtrType(null);
        public PtrDiffType PtrDiffTypeInstance { get; private set; } = new PtrDiffType(null);
        public ClassType DelegateTypeInstance { get; private set; } = new ClassType(null);
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
            IntType empty = new IntType(null, sizeInBytes, signed);
            IntTypeInstances[key] = empty;
            return empty;
        }
        public Dictionary<int, FloatType> FloatTypeInstances { get; private set; } = new Dictionary<int, FloatType>();
        public FloatType GetFloatType(int sizeInBytes)
        {
            var key = sizeInBytes;
            if (FloatTypeInstances.TryGetValue(key, out FloatType value))
            {
                return value;
            }
            FloatType empty = new FloatType(null, sizeInBytes);
            FloatTypeInstances[key] = empty;
            return empty;
        }

        public void Init()
        {
            PtrToVoidType = PointerType.GetPointerType(VoidTypeInstance);
        }
    }
}
