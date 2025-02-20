using HapetFrontend.Types;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstNumberExpr : AstExpression
    {
        public NumberData Data { get; private set; }
        public string Suffix { get; set; }

        public override string AAAName => nameof(AstNumberExpr);

        public AstNumberExpr(NumberData data, string suffix = null, HapetType numberType = null, ILocation Location = null) : base(Location)
        {
            Data = data;
            OutValue = data;
            this.Suffix = suffix;

            if (numberType == null)
            {
                if (data.Type == Enums.NumberType.Float)
                {
                    OutType = FloatType.DefaultType;
                }
                else
                {
                    OutType = IntType.DefaultType;
                }
            }
            else
                OutType = numberType;
        }
    }
}
