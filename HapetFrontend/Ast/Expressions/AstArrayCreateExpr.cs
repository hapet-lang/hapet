using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstArrayCreateExpr : AstExpression, ICloneable
    {
        /// <summary>
        /// The type of which array is created
        /// </summary>
        public AstExpression TypeName { get; set; }
        /// <summary>
        /// The size expressions 
        /// This is a list because of ndim arrays
        /// </summary>
        public List<AstExpression> SizeExprs { get; set; }
        /// <summary>
        /// Init values of the array
        /// </summary>
        public List<AstExpression> Elements { get; set; }

        public override string AAAName => nameof(AstArrayCreateExpr);

        public AstArrayCreateExpr(AstExpression type, List<AstExpression> sizeExprs, List<AstExpression> elements, ILocation Location = null) : base(Location)
        {
            TypeName = type;
            SizeExprs = sizeExprs;
            Elements = elements;
        }

        public object Clone()
        {
            return new AstArrayCreateExpr(TypeName, new List<AstExpression>(SizeExprs), new List<AstExpression>(Elements), Location)
            {
                OutType = OutType,
                Parent = Parent,
                Scope = Scope,
                SourceFile = SourceFile,
                OutValue = OutValue,
                IsCompileTimeValue = IsCompileTimeValue
            };
        }
    }
}
