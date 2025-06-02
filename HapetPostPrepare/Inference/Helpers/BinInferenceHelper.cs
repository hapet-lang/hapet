using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Scoping;
using HapetFrontend.Types;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        /// <summary>
        /// Handles AstBinaryExpr to make a decision 
        /// of which of one of the two ops to cast to another one.
        /// Use it when need to handle shite like
        /// var result = someVar + 1;
        /// where someVar could be uint and 1 is an int by default
        /// so the method should make a cast like:
        /// var result = someVar + (uint)1;
        /// </summary>
        /// <param name="expr">The expr to handle</param>
        public void HandleBinExpr(AstBinaryExpr binExpr)
        {
            // skip user defined
            if (binExpr.ActualOperator is IUserDefinedOperator)
                return;

            // special case for 
            // structInstance == null
            // this could be possible because of generics generation
            if ((binExpr.Operator == "==" || binExpr.Operator == "!=") &&
                (binExpr.Left.OutType is StructType && binExpr.Right.OutType == PointerType.NullLiteralType ||
                binExpr.Left.OutType == PointerType.NullLiteralType && binExpr.Right.OutType is StructType))
            {
                // just make one expr to be 'true' and another one 'false'
                binExpr.Right = new AstBoolExpr(true, binExpr);
                binExpr.Left = new AstBoolExpr(false, binExpr);
                var operators = binExpr.Scope.GetBinaryOperators(binExpr.Operator, binExpr.Left.OutType, binExpr.Right.OutType);
                binExpr.ActualOperator = operators[0];
                return;
            }

            // only for numerics
            if (binExpr.Left.OutType is not IntType && binExpr.Left.OutType is not FloatType && binExpr.Left.OutType is not CharType &&
                binExpr.Right.OutType is not IntType && binExpr.Right.OutType is not FloatType && binExpr.Right.OutType is not CharType)
                return;

            // just skip these. no need to do anything with them
            if (binExpr.Operator == "<<" || binExpr.Operator == ">>")
                return;

            // check if someone has a value
            // some shite is done to do smth like:
            // we have: where someVar is uint type
            // var result = someVar + 1;
            // it has to make it like:
            // var result = someVar + (uint)1;
            if (binExpr.Left.OutValue != null)
            {
                if (binExpr.Right.OutValue != null)
                {
                    // if both have values - check for preferred and return
                    HapetType preferredType = HapetType.GetPreferredTypeOf(binExpr.Left.OutType, binExpr.Right.OutType, out bool isFirstSelected);
                    if (isFirstSelected)
                        binExpr.Right = PostPrepareExpressionWithType(GetPreparedAst(preferredType, binExpr), binExpr.Right);
                    else
                        binExpr.Left = PostPrepareExpressionWithType(GetPreparedAst(preferredType, binExpr), binExpr.Left);
                    return;
                }

                // here - right with no out value!
                CastResult castResult = new CastResult();
                var casted = PostPrepareExpressionWithType(binExpr.Right, binExpr.Left, castResult);
                if (castResult.CouldBeCasted)
                    binExpr.Left = casted;
            }
            else if (binExpr.Right.OutValue != null)
            {
                // here - left with no out value!
                CastResult castResult = new CastResult();
                var casted = PostPrepareExpressionWithType(binExpr.Left, binExpr.Right, castResult);
                if (castResult.CouldBeCasted)
                    binExpr.Right = casted;
            }

            // creating cast to result type if it is not a bool expr
            if (binExpr.Left.OutType != binExpr.OutType &&
                binExpr.OutType is not BoolType &&
                binExpr.OutType is not PointerType)
            {
                // cast if they are not the same haha
                binExpr.Left = PostPrepareExpressionWithType(GetPreparedAst(binExpr.OutType, binExpr), binExpr.Left);
            }
            // creating cast to result type if it is not a bool expr
            if (binExpr.Right.OutType != binExpr.OutType &&
                binExpr.OutType is not BoolType &&
                binExpr.OutType is not PointerType)
            {
                // cast if they are not the same haha
                binExpr.Right = PostPrepareExpressionWithType(GetPreparedAst(binExpr.OutType, binExpr), binExpr.Right);
            }

            // creating cast to result type if it is a bool expr and left and right are not the same types
            if (binExpr.Right.OutType != binExpr.Left.OutType &&
                binExpr.OutType is BoolType)
            {
                // cast if they are not the same haha
                HapetType castingType = HapetType.GetPreferredTypeOf(binExpr.Left.OutType, binExpr.Right.OutType, out bool tookLeft);
                // if the left type was taken then change the right expr
                if (tookLeft)
                    binExpr.Right = PostPrepareExpressionWithType(GetPreparedAst(castingType, binExpr), binExpr.Right);
                else
                    binExpr.Left = PostPrepareExpressionWithType(GetPreparedAst(castingType, binExpr), binExpr.Left);
            }

        }
    }
}
