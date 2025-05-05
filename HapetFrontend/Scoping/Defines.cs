using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Scoping
{
    public partial class Scope
    {
        internal void DefineBuiltInTypes()
        {
            DefineTypeSymbol(new AstIdExpr("sbyte"), IntType.GetIntType(1, true));
            DefineTypeSymbol(new AstIdExpr("short"), IntType.GetIntType(2, true));
            DefineTypeSymbol(new AstIdExpr("int"), IntType.GetIntType(4, true));
            DefineTypeSymbol(new AstIdExpr("long"), IntType.GetIntType(8, true));

            DefineTypeSymbol(new AstIdExpr("byte"), IntType.GetIntType(1, false));
            DefineTypeSymbol(new AstIdExpr("ushort"), IntType.GetIntType(2, false));
            DefineTypeSymbol(new AstIdExpr("uint"), IntType.GetIntType(4, false));
            DefineTypeSymbol(new AstIdExpr("ulong"), IntType.GetIntType(8, false));

            DefineTypeSymbol(new AstIdExpr("half"), FloatType.GetFloatType(2));
            DefineTypeSymbol(new AstIdExpr("float"), FloatType.GetFloatType(4));
            DefineTypeSymbol(new AstIdExpr("double"), FloatType.GetFloatType(8));

            DefineTypeSymbol(new AstIdExpr("char16"), CharType.DefaultType);
            DefineTypeSymbol(new AstIdExpr("char"), CharType.DefaultType);
            DefineTypeSymbol(new AstIdExpr("bool"), BoolType.Instance);
            DefineTypeSymbol(new AstIdExpr("void"), VoidType.Instance);

            DefineTypeSymbol(new AstIdExpr("var"), VarType.Instance);
            DefineTypeSymbol(new AstIdExpr("uintptr"), IntPtrType.Instance);
            DefineTypeSymbol(new AstIdExpr("ptrdiff"), PtrDiffType.Instance);
        }

        internal void DefineBuiltInOperators()
        {
            DefineLiteralOperators();
        }

        private void DefineLiteralOperators()
        {
            // literal types
            DefineUnaryOperator("!", BoolType.Instance, BoolType.Instance, b => !(bool)b);
            DefineUnaryOperator("-", IntType.LiteralType, IntType.LiteralType, a => ((NumberData)a).Negate());
            DefineUnaryOperator("++", IntType.LiteralType, IntType.LiteralType, a => ((NumberData)a + 1));
            DefineUnaryOperator("--", IntType.LiteralType, IntType.LiteralType, a => ((NumberData)a - 1));
            DefineUnaryOperator("-", FloatType.LiteralType, FloatType.LiteralType, a => ((NumberData)a).Negate());

            // bool types
            DefineBinaryOperator(new BuiltInBinaryOperator("&&", BoolType.Instance, BoolType.Instance, BoolType.Instance, (a, b) => (bool)a && (bool)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("&", BoolType.Instance, BoolType.Instance, BoolType.Instance, (a, b) => (bool)a & (bool)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("||", BoolType.Instance, BoolType.Instance, BoolType.Instance, (a, b) => (bool)a || (bool)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("|", BoolType.Instance, BoolType.Instance, BoolType.Instance, (a, b) => (bool)a | (bool)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, BoolType.Instance, BoolType.Instance, (a, b) => (bool)a == (bool)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, BoolType.Instance, BoolType.Instance, (a, b) => (bool)a != (bool)b));

            DefineBinaryOperator(new BuiltInBinaryOperator("+", StringType.LiteralType, StringType.LiteralType, StringType.LiteralType, (a, b) => $"{a}{b}"));

            DefineBinaryOperator(new BuiltInBinaryOperator("+", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a + (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("-", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a - (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("*", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a * (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("/", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a / (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("%", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a % (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("&", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a & (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("|", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a | (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("<<", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a << (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator(">>", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a >> (NumberData)b));

            DefineBinaryOperator(new BuiltInBinaryOperator("+", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a + (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("-", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a - (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("*", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a * (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("/", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a / (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("%", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a % (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("&", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a & (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("|", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a | (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("<<", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a << (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator(">>", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a >> (NumberData)b));

            DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a == (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a != (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("<", BoolType.Instance, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a < (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("<=", BoolType.Instance, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a <= (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator(">", BoolType.Instance, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a > (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator(">=", BoolType.Instance, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a >= (NumberData)b));


            DefineBinaryOperator(new BuiltInBinaryOperator("+", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a + (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("-", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a - (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("*", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a * (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("/", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a / (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("%", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a % (NumberData)b));

            DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a == (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a != (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("<", BoolType.Instance, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a < (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("<=", BoolType.Instance, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a <= (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator(">", BoolType.Instance, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a > (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator(">=", BoolType.Instance, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a >= (NumberData)b));

            // special cases
            DefineBinaryOperator(new BuiltInBinaryOperator("+", FloatType.LiteralType, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a + (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("-", FloatType.LiteralType, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a - (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("*", FloatType.LiteralType, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a * (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("/", FloatType.LiteralType, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a / (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("+", FloatType.LiteralType, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a + (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("-", FloatType.LiteralType, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a - (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("*", FloatType.LiteralType, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a * (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("/", FloatType.LiteralType, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a / (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a == (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a != (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("<", BoolType.Instance, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a < (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("<=", BoolType.Instance, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a <= (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator(">", BoolType.Instance, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a > (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator(">=", BoolType.Instance, FloatType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a >= (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a == (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a != (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("<", BoolType.Instance, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a < (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("<=", BoolType.Instance, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a <= (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator(">", BoolType.Instance, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a > (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator(">=", BoolType.Instance, IntType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a >= (NumberData)b));
            // end special cases

            // basic types

            var numTypes = new HapetType[]
                {
                    IntType.GetIntType(1, true),
                    IntType.GetIntType(2, true),
                    IntType.GetIntType(4, true),
                    IntType.GetIntType(8, true),
                    IntType.GetIntType(1, false),
                    IntType.GetIntType(2, false),
                    IntType.GetIntType(4, false),
                    IntType.GetIntType(8, false),
                    CharType.DefaultType,
                    FloatType.GetFloatType(2),
                    FloatType.GetFloatType(4),
                    FloatType.GetFloatType(8),
                    IntPtrType.Instance,
                    PtrDiffType.Instance
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

                    DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, type, secondType, (a, b) => (NumberData)a == (NumberData)b));
                    DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, type, secondType, (a, b) => (NumberData)a != (NumberData)b));
                    DefineBinaryOperator(new BuiltInBinaryOperator("<", BoolType.Instance, type, secondType, (a, b) => (NumberData)a < (NumberData)b));
                    DefineBinaryOperator(new BuiltInBinaryOperator("<=", BoolType.Instance, type, secondType, (a, b) => (NumberData)a <= (NumberData)b));
                    DefineBinaryOperator(new BuiltInBinaryOperator(">", BoolType.Instance, type, secondType, (a, b) => (NumberData)a > (NumberData)b));
                    DefineBinaryOperator(new BuiltInBinaryOperator(">=", BoolType.Instance, type, secondType, (a, b) => (NumberData)a >= (NumberData)b));

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

            DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, PointerType.VoidLiteralType, PointerType.VoidLiteralType));
            DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, PointerType.VoidLiteralType, PointerType.VoidLiteralType));

            DefineBinaryOperator(new BuiltInCommonBinaryOperator("as", ClassType.LiteralType, PointerType.VoidLiteralType, ClassType.LiteralType));
            DefineBinaryOperator(new BuiltInCommonBinaryOperator("is", BoolType.Instance, PointerType.VoidLiteralType, ClassType.LiteralType));
            DefineBinaryOperator(new BuiltInCommonBinaryOperator("is", BoolType.Instance, PointerType.VoidLiteralType, StructType.LiteralType));
        }
    }
}
