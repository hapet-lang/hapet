using HapetFrontend.Types;

namespace HapetFrontend.Scoping
{
    public partial class Scope
    {
        internal void DefineBuiltInTypes()
        {
            DefineTypeSymbol("sbyte", IntType.GetIntType(1, true));
            DefineTypeSymbol("short", IntType.GetIntType(2, true));
            DefineTypeSymbol("int", IntType.GetIntType(4, true));
            DefineTypeSymbol("long", IntType.GetIntType(8, true));

            DefineTypeSymbol("byte", IntType.GetIntType(1, false));
            DefineTypeSymbol("ushort", IntType.GetIntType(2, false));
            DefineTypeSymbol("uint", IntType.GetIntType(4, false));
            DefineTypeSymbol("ulong", IntType.GetIntType(8, false));

            DefineTypeSymbol("half", FloatType.GetFloatType(2));
            DefineTypeSymbol("float", FloatType.GetFloatType(4));
            DefineTypeSymbol("double", FloatType.GetFloatType(8));

            DefineTypeSymbol("char8", CharType.GetCharType(1));
            DefineTypeSymbol("char16", CharType.GetCharType(2));
            DefineTypeSymbol("char32", CharType.GetCharType(4));
            DefineTypeSymbol("char", CharType.GetCharType(4));
            DefineTypeSymbol("bool", BoolType.Instance);
            DefineTypeSymbol("string", StringType.Instance);
            DefineTypeSymbol("void", VoidType.Instance);
            // DefineTypeSymbol("type", CheezType.Type); // TODO: ...
            // DefineTypeSymbol("Code", CheezType.Code); // TODO: do i need it?
        }

        internal void DefineBuiltInOperators()
        {
            DefineLiteralOperators();

            DefineLogicOperators(new HapetType[] { BoolType.Instance },
                ("&&", (a, b) => (bool)a && (bool)b),
                ("||", (a, b) => (bool)a || (bool)b),
                ("==", (a, b) => (bool)a == (bool)b),
                ("!=", (a, b) => (bool)a != (bool)b));

            // TODO: ...
            // DefinePointerOperators();

            // TODO: ...
            //DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, CheezType.Type, CheezType.Type, (a, b) => a == b));
            //DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, CheezType.Type, CheezType.Type, (a, b) => a != b));

            DefineBinaryOperator(new BuiltInClassNullOperator("=="));
            DefineBinaryOperator(new BuiltInClassNullOperator("!="));
            DefineBinaryOperator(new BuiltInFunctionOperator("=="));
            DefineBinaryOperator(new BuiltInFunctionOperator("!="));
            DefineBinaryOperator(new BuiltInEnumCompareOperator("=="));
            DefineBinaryOperator(new BuiltInEnumCompareOperator("!="));
        }

        private void DefineLiteralOperators()
        {
            // literal types
            DefineUnaryOperator("!", BoolType.Instance, b => !(bool)b);
            DefineUnaryOperator("-", IntType.LiteralType, a => ((NumberData)a).Negate());
            DefineUnaryOperator("-", FloatType.LiteralType, a => ((NumberData)a).Negate());

            DefineBinaryOperator(new BuiltInBinaryOperator("+", StringType.LiteralType, StringType.LiteralType, StringType.LiteralType, (a, b) => $"{a}{b}"));

            DefineBinaryOperator(new BuiltInBinaryOperator("+", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a + (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("-", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a - (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("*", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a * (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("/", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a / (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("%", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a % (NumberData)b));

            DefineBinaryOperator(new BuiltInBinaryOperator("+", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a + (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("-", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a - (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("*", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a * (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("/", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a / (NumberData)b));
            DefineBinaryOperator(new BuiltInBinaryOperator("%", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a % (NumberData)b));

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


            // basic types

            foreach (var type in new HapetType[]
                {
                    IntType.GetIntType(1, true),
                    IntType.GetIntType(2, true),
                    IntType.GetIntType(4, true),
                    IntType.GetIntType(8, true),
                    IntType.GetIntType(1, false),
                    IntType.GetIntType(2, false),
                    IntType.GetIntType(4, false),
                    IntType.GetIntType(8, false),
                    CharType.GetCharType(1),
                    CharType.GetCharType(2),
                    CharType.GetCharType(4),
                })
            {
                DefineUnaryOperator("-", type, a => ((NumberData)a).Negate());

                DefineBinaryOperator(new BuiltInBinaryOperator("+", type, type, type, (a, b) => (NumberData)a + (NumberData)b));
                DefineBinaryOperator(new BuiltInBinaryOperator("-", type, type, type, (a, b) => (NumberData)a - (NumberData)b));
                DefineBinaryOperator(new BuiltInBinaryOperator("*", type, type, type, (a, b) => (NumberData)a * (NumberData)b));
                DefineBinaryOperator(new BuiltInBinaryOperator("/", type, type, type, (a, b) => (NumberData)a / (NumberData)b));
                DefineBinaryOperator(new BuiltInBinaryOperator("%", type, type, type, (a, b) => (NumberData)a % (NumberData)b));

                DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, type, type, (a, b) => (NumberData)a == (NumberData)b));
                DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, type, type, (a, b) => (NumberData)a != (NumberData)b));
                DefineBinaryOperator(new BuiltInBinaryOperator("<", BoolType.Instance, type, type, (a, b) => (NumberData)a < (NumberData)b));
                DefineBinaryOperator(new BuiltInBinaryOperator("<=", BoolType.Instance, type, type, (a, b) => (NumberData)a <= (NumberData)b));
                DefineBinaryOperator(new BuiltInBinaryOperator(">", BoolType.Instance, type, type, (a, b) => (NumberData)a > (NumberData)b));
                DefineBinaryOperator(new BuiltInBinaryOperator(">=", BoolType.Instance, type, type, (a, b) => (NumberData)a >= (NumberData)b));
            }

            foreach (var type in new FloatType[]
            {
                FloatType.GetFloatType(2),
                FloatType.GetFloatType(4),
                FloatType.GetFloatType(8)
            })
            {
                DefineUnaryOperator("-", type, a => ((NumberData)a).Negate());

                DefineBinaryOperator(new BuiltInBinaryOperator("+", type, type, type, (a, b) => ((NumberData)a) + ((NumberData)b)));
                DefineBinaryOperator(new BuiltInBinaryOperator("-", type, type, type, (a, b) => ((NumberData)a) - ((NumberData)b)));
                DefineBinaryOperator(new BuiltInBinaryOperator("*", type, type, type, (a, b) => ((NumberData)a) * ((NumberData)b)));
                DefineBinaryOperator(new BuiltInBinaryOperator("/", type, type, type, (a, b) => ((NumberData)a) / ((NumberData)b)));
                DefineBinaryOperator(new BuiltInBinaryOperator("%", type, type, type, (a, b) => ((NumberData)a) % ((NumberData)b)));

                DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, type, type, (a, b) => ((NumberData)a) == ((NumberData)b)));
                DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, type, type, (a, b) => ((NumberData)a) != ((NumberData)b)));
                DefineBinaryOperator(new BuiltInBinaryOperator("<", BoolType.Instance, type, type, (a, b) => ((NumberData)a) < ((NumberData)b)));
                DefineBinaryOperator(new BuiltInBinaryOperator("<=", BoolType.Instance, type, type, (a, b) => ((NumberData)a) <= ((NumberData)b)));
                DefineBinaryOperator(new BuiltInBinaryOperator(">", BoolType.Instance, type, type, (a, b) => ((NumberData)a) > ((NumberData)b)));
                DefineBinaryOperator(new BuiltInBinaryOperator(">=", BoolType.Instance, type, type, (a, b) => ((NumberData)a) >= ((NumberData)b)));
            }
        }
    }
}
