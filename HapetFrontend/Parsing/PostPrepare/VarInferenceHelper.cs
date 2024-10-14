using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Types;

namespace HapetFrontend.Parsing.PostPrepare
{
    public partial class PostPrepare
    {
        /// <summary>
        /// The method is used to prepare correct assignment.
        /// Like 'float a = 6;' should be allowed and 'int a = 6.0;' should not be allowed
        /// Also class/interface shite should be checked here
        /// </summary>
        /// <param name="varDecl">The var decl</param>
        private void PostPrepareVariableAssign(AstVarDecl varDecl)
        {
            var newExpr = PostPrepareExpressionWithType(varDecl.Type.OutType, varDecl.Initializer);
            varDecl.Initializer = newExpr;
        }

        /// <summary>
        /// The same as <see cref="PostPrepareVariableAssign"/> but for <see cref="AstAssignStmt"/>
        /// </summary>
        /// <param name="assignStmt">The var assignment</param>
        private void PostPrepareVariableAssign(AstAssignStmt assignStmt)
        {
            var newExpr = PostPrepareExpressionWithType(assignStmt.Target.OutType, assignStmt.Value);
            assignStmt.Value = newExpr;
        }

        /// <summary>
        /// The method tries to cast the <paramref name="expr"/> to <paramref name="neededType"/> type implicitly
        /// If it cannot the converted an error would appear
        /// </summary>
        /// <param name="neededType">The type that should be outed</param>
        /// <param name="expr">The expr to be casted</param>
        /// <returns>Casted expr</returns>
        private AstExpression PostPrepareExpressionWithType(HapetType neededType, AstExpression expr)
        {
            HapetType exprType = expr.OutType;
            AstExpression outExpr = null;

            var tpName = new AstIdExpr(neededType.TypeName);
            tpName.OutType = neededType;
            tpName.Scope = expr.Scope;

            var cst = new AstCastExpr(tpName, expr, expr);
            cst.OutType = neededType;
            cst.Scope = expr.Scope;

            switch (neededType)
            {
                case FloatType when exprType is IntType:
                case FloatType when exprType is CharType:
                case IntType when exprType is CharType:
                    outExpr = cst;
                    break;
                    // TODO: other checks also. warn: class and class should be checked properly!!!
            }

            // if there is no way to cast
            if (neededType != exprType && outExpr == null)
            {
                if (!(neededType is PointerType ptr1 && ptr1.TargetType is ClassType && exprType is ClassType) && // usually when 'Anime a = new Anime();'
					!(neededType is PointerType ptr2 && ptr2.TargetType is CharType && exprType is StringType) // string is just a char ptr
                    ) // place here other exceptions
                {
                    _compiler.ErrorHandler.ReportError(_currentSourceFile.Text, expr, $"Type {exprType} cannot be implicitly casted into {neededType}");
                }
                outExpr = expr;
            }
            // if the types are equal - no need to cast anything, so return orig
            else if (neededType == exprType && outExpr == null)
            {
                outExpr = expr;
            }
            return outExpr;
        }
    }
}
