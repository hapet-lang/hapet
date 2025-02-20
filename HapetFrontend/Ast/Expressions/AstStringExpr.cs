using HapetFrontend.Ast.Declarations;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstStringExpr : AstExpression
    {
        public string StringValue => (string)OutValue;
        public string Suffix { get; set; }

        public override string AAAName => nameof(AstStringExpr);

        public AstStringExpr(string value, string suffix = null, ILocation Location = null) : base(Location)
        {
            this.OutValue = value;
            this.Suffix = suffix;
        }

        public static AstStructDecl GetStringStruct(Scope scope)
        {
            return (scope.GetSymbolInNamespace("System", "String") as DeclSymbol).Decl as AstStructDecl;
        }
    }
}
