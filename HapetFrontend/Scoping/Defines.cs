using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Scoping
{
    public partial class Scope
    {
        internal void DefineBuiltInTypes()
        {
            //DefineTypeSymbol(new AstIdExpr("sbyte"), IntType.GetIntType(1, true));
            //DefineTypeSymbol(new AstIdExpr("short"), IntType.GetIntType(2, true));
            //DefineTypeSymbol(new AstIdExpr("int"), IntType.GetIntType(4, true));
            //DefineTypeSymbol(new AstIdExpr("long"), IntType.GetIntType(8, true));

            //DefineTypeSymbol(new AstIdExpr("byte"), IntType.GetIntType(1, false));
            //DefineTypeSymbol(new AstIdExpr("ushort"), IntType.GetIntType(2, false));
            //DefineTypeSymbol(new AstIdExpr("uint"), IntType.GetIntType(4, false));
            //DefineTypeSymbol(new AstIdExpr("ulong"), IntType.GetIntType(8, false));

            DefineTypeSymbol(new AstIdExpr("half"), FloatType.GetFloatType(2));
            DefineTypeSymbol(new AstIdExpr("float"), FloatType.GetFloatType(4));
            DefineTypeSymbol(new AstIdExpr("double"), FloatType.GetFloatType(8));

            //DefineTypeSymbol(new AstIdExpr("char"), CharType.DefaultType);
            //DefineTypeSymbol(new AstIdExpr("bool"), boolT);
            //DefineTypeSymbol(new AstIdExpr("void"), VoidType.Instance);

            DefineTypeSymbol(new AstIdExpr("var"), VarType.Instance);
            //DefineTypeSymbol(new AstIdExpr("uintptr"), IntPtrType.Instance);
            //DefineTypeSymbol(new AstIdExpr("ptrdiff"), PtrDiffType.Instance);
        }

        internal void DefineBuiltInOperators()
        {
            DefineLiteralOperators();
        }

        private void DefineLiteralOperators()
        {
            var boolT = HapetType.CurrentTypeContext.BoolTypeInstance;

            // literal types
            DefineUnaryOperator("!", boolT, boolT, b => !(bool)b);
            DefineUnaryOperator("-", IntType.LiteralType, IntType.LiteralType, a => ((NumberData)a).Negate());
            DefineUnaryOperator("++", IntType.LiteralType, IntType.LiteralType, a => ((NumberData)a + 1));
            DefineUnaryOperator("--", IntType.LiteralType, IntType.LiteralType, a => ((NumberData)a - 1));
            DefineUnaryOperator("-", FloatType.LiteralType, FloatType.LiteralType, a => ((NumberData)a).Negate());

            // bool types
            DefineBinaryOperator(new BuiltInBinaryOperator("&&", boolT, boolT, boolT, (a, b) => (bool)a && (bool)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("&", boolT, boolT, boolT, (a, b) => (bool)a & (bool)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("||", boolT, boolT, boolT, (a, b) => (bool)a || (bool)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("|", boolT, boolT, boolT, (a, b) => (bool)a | (bool)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, boolT, boolT, (a, b) => (bool)a == (bool)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, boolT, boolT, (a, b) => (bool)a != (bool)b));

            DefineBinaryOperator(new BuiltInBinaryOperator("+", StringType.LiteralType, StringType.LiteralType, StringType.LiteralType, (a, b) => $"{a}{b}"));

            DefineBinaryOperator(new BuiltInBinaryOperator("+", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a + (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("-", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a - (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("*", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a * (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("/", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a / (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("%", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a % (NumberData)b));

            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a == (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a != (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("<", boolT, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a < (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("<=", boolT, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a <= (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator(">", boolT, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a > (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator(">=", boolT, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a >= (NumberData)b));

            // special cases
            DefineBinaryOperator(new BuiltInBinaryOperator("+", FloatType.LiteralType, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a + (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("-", FloatType.LiteralType, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a - (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("*", FloatType.LiteralType, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a * (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("/", FloatType.LiteralType, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a / (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("+", FloatType.LiteralType, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a + (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("-", FloatType.LiteralType, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a - (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("*", FloatType.LiteralType, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a * (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("/", FloatType.LiteralType, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a / (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a == (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a != (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("<", boolT, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a < (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("<=", boolT, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a <= (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator(">", boolT, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a > (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator(">=", boolT, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a >= (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a == (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a != (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("<", boolT, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a < (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("<=", boolT, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a <= (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator(">", boolT, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a > (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator(">=", boolT, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a >= (NumberData)b));
            // end special cases

            // basic types

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
                    FloatType.GetFloatType(2),
                    FloatType.GetFloatType(4),
                    FloatType.GetFloatType(8),
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
                DefineBinaryOperator(new BuiltInBinaryOperator("+", PointerType.VoidLiteralType, type, PointerType.VoidLiteralType));
                DefineBinaryOperator(new BuiltInBinaryOperator("+", PointerType.VoidLiteralType, PointerType.VoidLiteralType, type));
                DefineBinaryOperator(new BuiltInBinaryOperator("-", PointerType.VoidLiteralType, PointerType.VoidLiteralType, type));
            }

            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, PointerType.VoidLiteralType, PointerType.VoidLiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, PointerType.VoidLiteralType, GenericType.LiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, GenericType.LiteralType, PointerType.VoidLiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, PointerType.VoidLiteralType, PointerType.VoidLiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, PointerType.VoidLiteralType, GenericType.LiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, GenericType.LiteralType, PointerType.VoidLiteralType));

            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, GenericType.LiteralType, GenericType.LiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, GenericType.LiteralType, GenericType.LiteralType));

            DefineBinaryOperator(new BuiltInCommonBinaryOperator("as", ClassType.LiteralType, PointerType.VoidLiteralType, ClassType.LiteralType));
            DefineBinaryOperator(new BuiltInCommonBinaryOperator("as", StructType.LiteralType, PointerType.VoidLiteralType, StructType.LiteralType));

            DefineBinaryOperator(new BuiltInCommonBinaryOperator("is", boolT, PointerType.VoidLiteralType, ClassType.LiteralType));
            DefineBinaryOperator(new BuiltInCommonBinaryOperator("is", boolT, PointerType.VoidLiteralType, StructType.LiteralType));
            DefineBinaryOperator(new BuiltInCommonBinaryOperator("is", boolT, PointerType.VoidLiteralType, GenericType.LiteralType));

            // strings and arrays
            // WARN: structs are defined here - array/string types checks are in VarInferenceHelper
            DefineBinaryOperator(new BuiltInBinaryOperator("==", boolT, StructType.LiteralType, PointerType.VoidLiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", boolT, StructType.LiteralType, PointerType.VoidLiteralType));
        }
    }
}
