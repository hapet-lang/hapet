using HapetFrontend.Ast.Declarations;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstArrayAccessExpr : AstExpression
    {
        /// <summary>
        /// The expression on which indexing is done
        /// </summary>
        public AstExpression ObjectName { get; set; }
        /// <summary>
        /// It could be not only an Int. but also a String (for dicts) and other shite
        /// For ndim arrays use nested of this
        /// </summary>
        public AstExpression ParameterExpr { get; set; }

        public override string AAAName => nameof(AstArrayAccessExpr);

        public AstArrayAccessExpr(AstExpression objectName, AstExpression parameterExpr, ILocation Location = null) : base(Location)
        {
            ObjectName = objectName;
            ParameterExpr = parameterExpr;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstArrayAccessExpr(
                ObjectName.GetDeepCopy() as AstExpression,
                ParameterExpr.GetDeepCopy() as AstExpression,
                Location)
            {
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
