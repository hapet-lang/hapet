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

        public AstStringExpr(string value, string suffix = null, ILocation location = null) : base(location)
        {
            this.OutValue = value;
            this.Suffix = suffix;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstStringExpr(
                StringValue, Suffix,
                Location)
            {
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
                TupleNameList = TupleNameList,
            };
            return copy;
        }
    }
}
