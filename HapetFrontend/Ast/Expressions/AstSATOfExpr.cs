using HapetFrontend.Parsing;

namespace HapetFrontend.Ast.Expressions
{
    /// <summary>
    /// Handle for sizeof, alignof, typeof intrinsics
    /// </summary>
    public class AstSATOfExpr : AstExpression
    {
        /// <summary>
        /// One of <see cref="TokenType.KwSizeof"/>, <see cref="TokenType.KwAlignof"/>, <see cref="TokenType.KwTypeof"/>, <see cref="TokenType.KwNameof"/>
        /// </summary>
        public TokenType ExprType { get; set; }

        /// <summary>
        /// Type on which the SAT is applied
        /// </summary>
        public AstNestedExpr TargetType { get; set; }

        public override string AAAName => nameof(AstSATOfExpr);

        public AstSATOfExpr(AstNestedExpr targetType, TokenType exprType, ILocation location = null) : base(location)
        {
            TargetType = targetType;
            ExprType = exprType;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstSATOfExpr(TargetType, ExprType, Location)
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
