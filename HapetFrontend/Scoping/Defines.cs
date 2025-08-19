using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Scoping
{
    public partial class Scope
    {
        internal void DefineBuiltInTypes()
        {
            DefineTypeSymbol(new AstIdExpr("var"), VarType.Instance);
        }

        internal void DefineBuiltInOperators()
        {
            DefineLiteralOperators();
        }

        private void DefineLiteralOperators()
        {
            var boolT = HapetType.CurrentTypeContext.BoolTypeInstance;
            var ptrToVoid = HapetType.CurrentTypeContext.PtrToVoidType;

            // literal types
            DefineUnaryOperator("!", boolT, boolT, b => !(bool)b);

            // bool types
            DefineBinaryOperator(new BuiltInBinaryOperator("&&", boolT, boolT, boolT, (a, b) => (bool)a && (bool)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("&", boolT, boolT, boolT, (a, b) => (bool)a & (bool)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("||", boolT, boolT, boolT, (a, b) => (bool)a || (bool)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("|", boolT, boolT, boolT, (a, b) => (bool)a | (bool)b));
            // these are already defined in structs
            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, boolT, boolT, (a, b) => (bool)a == (bool)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, boolT, boolT, (a, b) => (bool)a != (bool)b));

            var stringType = HapetType.CurrentTypeContext.StringTypeInstance;
            DefineBinaryOperator(new BuiltInBinaryOperator("+", stringType, stringType, stringType, (a, b) => $"{a}{b}"));

            var numTypes = new HapetType[]
                {
                    HapetType.CurrentTypeContext.GetIntType(1, true),
                    HapetType.CurrentTypeContext.GetIntType(2, true),
                    HapetType.CurrentTypeContext.GetIntType(4, true),
                    HapetType.CurrentTypeContext.GetIntType(8, true),
                    HapetType.CurrentTypeContext.GetIntType(1, false),
                    HapetType.CurrentTypeContext.GetIntType(2, false),
                    HapetType.CurrentTypeContext.GetIntType(4, false),
                    HapetType.CurrentTypeContext.GetIntType(8, false),
                    HapetType.CurrentTypeContext.CharTypeInstance,
                    HapetType.CurrentTypeContext.GetFloatType(4),
                    HapetType.CurrentTypeContext.GetFloatType(8),
                    HapetType.CurrentTypeContext.IntPtrTypeInstance,
                    HapetType.CurrentTypeContext.PtrDiffTypeInstance
                };
            foreach (var type in numTypes)
            {
                // TODO: replace resultType here with normally calced type
                // for example if we have 
                // byte a = 5;
                // int b = -a;
                // the result type has to be at least sbyte or even short
                DefineUnaryOperator("-", type, type, a => ((NumberData)a).Negate());

                // define this shite for ints
                if (type is not FloatType)
                {
                    DefineUnaryOperator("++", type, type, a => ((NumberData)a + 1));
                    DefineUnaryOperator("--", type, type, a => ((NumberData)a - 1));
                }

                foreach (var secondType in numTypes)
                {
                    // calc out type of these shite
                    HapetType outType = HapetType.GetPreferredTypeOf(type, secondType, out bool _);

                    DefineBinaryOperator(new BuiltInBinaryOperator("+", outType, type, secondType, (a, b) => (NumberData)a + (NumberData)b));
                    DefineBinaryOperator(new BuiltInBinaryOperator("-", outType, type, secondType, (a, b) => (NumberData)a - (NumberData)b));
                    DefineBinaryOperator(new BuiltInBinaryOperator("*", outType, type, secondType, (a, b) => (NumberData)a * (NumberData)b));
                    DefineBinaryOperator(new BuiltInBinaryOperator("/", outType, type, secondType, (a, b) => (NumberData)a / (NumberData)b));
                    DefineBinaryOperator(new BuiltInBinaryOperator("%", outType, type, secondType, (a, b) => (NumberData)a % (NumberData)b));

                    // these are already defined in structs
                    DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, type, secondType, (a, b) => (NumberData)a == (NumberData)b));
                    DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, type, secondType, (a, b) => (NumberData)a != (NumberData)b));

                    DefineBinaryOperator(new BuiltInBinaryOperator("<", boolT, type, secondType, (a, b) => (NumberData)a < (NumberData)b));
                    DefineBinaryOperator(new BuiltInBinaryOperator("<=", boolT, type, secondType, (a, b) => (NumberData)a <= (NumberData)b));
                    DefineBinaryOperator(new BuiltInBinaryOperator(">", boolT, type, secondType, (a, b) => (NumberData)a > (NumberData)b));
                    DefineBinaryOperator(new BuiltInBinaryOperator(">=", boolT, type, secondType, (a, b) => (NumberData)a >= (NumberData)b));

                    if ((type is IntType && secondType is IntType) ||
                        (type is IntType && secondType is CharType) ||
                        (type is CharType && secondType is IntType) ||
                        (type is CharType && secondType is CharType))
                    {
                        DefineBinaryOperator(new BuiltInBinaryOperator("&", outType, type, secondType, (a, b) => (NumberData)a & (NumberData)b));
                        DefineBinaryOperator(new BuiltInBinaryOperator("|", outType, type, secondType, (a, b) => (NumberData)a | (NumberData)b));
                        // there is no need to set output as the biggest type because the second param is only additional param
                        DefineBinaryOperator(new BuiltInBinaryOperator(">>", type, type, secondType, (a, b) => (NumberData)a >> (NumberData)b));
                        DefineBinaryOperator(new BuiltInBinaryOperator("<<", type, type, secondType, (a, b) => (NumberData)a << (NumberData)b));
                        DefineBinaryOperator(new BuiltInBinaryOperator("^", type, type, secondType, (a, b) => (NumberData)a ^ (NumberData)b));
                    }
                }
            }

            // for ptr arithmetics
            foreach (var type in numTypes)
            {
                // skip float types
                if (type is FloatType)
                    continue;

                DefineBinaryOperator(new BuiltInBinaryOperator("+", ptrToVoid, type, ptrToVoid));
                DefineBinaryOperator(new BuiltInBinaryOperator("+", ptrToVoid, ptrToVoid, type));
                DefineBinaryOperator(new BuiltInBinaryOperator("-", ptrToVoid, ptrToVoid, type));
            }
            DefineUnaryOperator(new BuiltInUnaryOperator("++", ptrToVoid, ptrToVoid));
            DefineUnaryOperator(new BuiltInUnaryOperator("--", ptrToVoid, ptrToVoid));

            DefineBinaryOperator(new BuiltInBinaryOperator("-", HapetType.CurrentTypeContext.PtrDiffTypeInstance, ptrToVoid, ptrToVoid));
            DefineBinaryOperator(new BuiltInBinaryOperator("+", HapetType.CurrentTypeContext.PtrDiffTypeInstance, ptrToVoid, ptrToVoid));
            DefineBinaryOperator(new BuiltInBinaryOperator("<", boolT, ptrToVoid, ptrToVoid));
            DefineBinaryOperator(new BuiltInBinaryOperator("<=", boolT, ptrToVoid, ptrToVoid));
            DefineBinaryOperator(new BuiltInBinaryOperator(">", boolT, ptrToVoid, ptrToVoid));
            DefineBinaryOperator(new BuiltInBinaryOperator(">=", boolT, ptrToVoid, ptrToVoid));

            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, ptrToVoid, ptrToVoid));
            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, NullType.LiteralType, GenericType.LiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, GenericType.LiteralType, NullType.LiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, ptrToVoid, ptrToVoid));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, NullType.LiteralType, GenericType.LiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, GenericType.LiteralType, NullType.LiteralType));

            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, GenericType.LiteralType, GenericType.LiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, GenericType.LiteralType, GenericType.LiteralType));

            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, ClassType.LiteralType, ClassType.LiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, ClassType.LiteralType, ClassType.LiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, NullType.LiteralType, ClassType.LiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, NullType.LiteralType, ClassType.LiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, ClassType.LiteralType, NullType.LiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, ClassType.LiteralType, NullType.LiteralType));

            DefineBinaryOperator(new BuiltInCommonBinaryOperator("as", ClassType.LiteralType, ClassType.LiteralType, ClassType.LiteralType));
            DefineBinaryOperator(new BuiltInCommonBinaryOperator("as", StructType.LiteralType, ClassType.LiteralType, StructType.LiteralType));

            DefineBinaryOperator(new BuiltInCommonBinaryOperator("is", boolT, ClassType.LiteralType, ClassType.LiteralType));
            DefineBinaryOperator(new BuiltInCommonBinaryOperator("is", boolT, ClassType.LiteralType, StructType.LiteralType));
            DefineBinaryOperator(new BuiltInCommonBinaryOperator("is", boolT, ClassType.LiteralType, GenericType.LiteralType));
            DefineBinaryOperator(new BuiltInCommonBinaryOperator("is", boolT, StructType.LiteralType, ClassType.LiteralType)); // need to warn when doing it
            // generics 'is' checks
            DefineBinaryOperator(new BuiltInCommonBinaryOperator("is", boolT, GenericType.LiteralType, ClassType.LiteralType));
            DefineBinaryOperator(new BuiltInCommonBinaryOperator("is", boolT, GenericType.LiteralType, StructType.LiteralType));

            // WARN: structs are defined here - array/string types checks are in VarInferenceHelper
            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, StructType.LiteralType, NullType.LiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, StructType.LiteralType, NullType.LiteralType));

            // just struct equal checking
            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, StructType.LiteralType, StructType.LiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, StructType.LiteralType, StructType.LiteralType));
        }
    }
}
