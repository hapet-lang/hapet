using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        /// <summary>
        /// Handles AstBinaryExpr and AstTernaryExpr to make a decision 
        /// of which of one of the two ops to cast to another one.
        /// Use it when need to handle shite like
        /// var result = someVar + 1;
        /// where someVar could be uint and 1 is an int by default
        /// so the method should make a cast like:
        /// var result = someVar + (uint)1;
        /// </summary>
        /// <param name="expr">The expr to handle</param>
        public void HandleBinExpr(AstExpression expr)
        {
            if (expr is AstBinaryExpr binExpr)
            {
                var result = HandleBinExprInternal(binExpr.Left, binExpr.Right);
                binExpr.Left = result.Item1;
                binExpr.Right = result.Item2;
            }
            else if (expr is AstTernaryExpr terExpr)
            {
                var result = HandleBinExprInternal(terExpr.TrueExpr, terExpr.FalseExpr);
                terExpr.TrueExpr = result.Item1;
                terExpr.FalseExpr = result.Item2;
            }
            throw new NotImplementedException("This method is not implemented for other types of exprs");
        }

        private (AstExpression, AstExpression) HandleBinExprInternal(AstExpression left, AstExpression right)
        {
            AstExpression resultLeft = left;
            AstExpression resultRight = right;

            // we need to handle here only exprs with numbers 
            if (left.OutType is not IntType && left.OutType is not FloatType && left.OutType is not CharType &&
                right.OutType is not IntType && right.OutType is not FloatType && right.OutType is not CharType)
                return (left, right);

            if (left.OutValue != null)
            {
                if (right.OutValue != null)
                {
                    // if both have values - check for preferred and return
                    HapetType _ = HapetType.GetPreferredTypeOf(left.OutType, right.OutType, out bool isFirstSelected);
                    if (isFirstSelected)
                        resultRight = PostPrepareExpressionWithType(GetPreparedAst(left.OutType, left), right);
                    else
                        resultLeft = PostPrepareExpressionWithType(GetPreparedAst(right.OutType, right), left);
                    return (resultLeft, resultRight);
                }

                // here - right with no out value!
            }
            else if (right.OutValue != null)
            {
                // here - left with no out value!
            }
        }
    }
}
