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

        /// <summary>
        /// 'true' if the access is performed via indexer
        /// </summary>
        public bool IsIndexerAccess { get; set; }

        public override string AAAName => nameof(AstArrayAccessExpr);

        public AstArrayAccessExpr(AstExpression objectName, AstExpression parameterExpr, ILocation location = null) : base(location)
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
                IsIndexerAccess = IsIndexerAccess,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
