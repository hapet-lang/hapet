using HapetFrontend.Ast.Declarations;
using HapetFrontend.Types;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstStringExpr : AstExpression
    {
        public string StringValue => (string)OutValue;
        public string Suffix { get; set; }

        [DebuggerStepThrough]
        public AstStringExpr(string value, string suffix = null, ILocation Location = null) : base(Location)
        {
            this.OutValue = value;
            this.Suffix = suffix;
            OutType = StringType.Instance;
        }

        private static AstStructDecl GenerateStringStructExpr()
        {
            // creating the struct and its scope
            AstStructDecl strStruct = new AstStructDecl(new AstIdExpr("string.type"), new List<AstDeclaration>(), ""); // TODO: doc string
                                                                                                                       // TODO: doc string
            AstVarDecl sizeField = new AstVarDecl(new AstNestedExpr(new AstIdExpr("int"), null), new AstIdExpr("Length"), new AstNumberExpr((NumberData)0), "");
            AstVarDecl bufField = new AstVarDecl(new AstNestedExpr(new AstPointerExpr(new AstIdExpr("char")), null), new AstIdExpr("Buffer"), new AstNullExpr(StringType.Instance), "");
            strStruct.Declarations.Add(sizeField);
            strStruct.Declarations.Add(bufField);
            strStruct.SpecialKeys.Add(Parsing.TokenType.KwPublic);
            return strStruct;
        }

        // the string struct is always like that
        private static AstStructDecl _stringStruct;
        public static AstStructDecl StringStruct
        {
            get
            {
                _stringStruct ??= GenerateStringStructExpr();
                return _stringStruct;
            }
        }
    }
}
