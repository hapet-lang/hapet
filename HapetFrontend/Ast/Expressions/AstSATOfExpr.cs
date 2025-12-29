using HapetFrontend.Parsing;
using System.Xml.Linq;

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
            var copy = new AstSATOfExpr(TargetType.GetDeepCopy() as AstNestedExpr, ExprType, Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
                TupleNameList = TupleNameList,
            };
            return copy;
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            if (TargetType == oldChild)
                TargetType = newChild as AstNestedExpr;
        }
    }
}
