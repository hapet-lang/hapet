using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;
using System.Xml.Linq;

namespace HapetFrontend.Ast.Statements
{
    public class AstForStmt : AstStatement
    {
        /// <summary>
        /// The first arg of for loop. Could be <see cref="AstVarDecl"/>, pure <see cref="AstExpression"/> or just a <see cref="null"/>
        /// </summary>
        public AstStatement FirstArgument { get; set; }

        /// <summary>
        /// The second arg of for loop. Could be pure <see cref="AstExpression"/> or just a <see cref="null"/>.
        /// Has to return <see cref="BoolType"/>
        /// </summary>
        public AstExpression SecondArgument { get; set; }

        /// <summary>
        /// The third arg of for loop. Usually just a <see cref="AstAssignStmt"/> so <see cref="AstStatement"/> or just a <see cref="null"/>.
        /// </summary>
        public AstStatement ThirdArgument { get; set; }

        /// <summary>
        /// The body of for loop
        /// </summary>
        public AstBlockExpr Body { get; set; }

        /// <summary>
        /// 'true' if the for is actually a foreach
        /// </summary>
        public bool IsForeach { get; set; }

        /// <summary>
        /// Argument to loop over
        /// </summary>
        public AstExpression ForeachArgument { get; set; }

        #region foreach helper props
        public AstVarDecl ForeachGetEnumeratorVar { get; set; }
        public AstCallExpr ForeachMoveNextCall { get; set; }
        public AstAssignStmt ForeachCurrentAssign { get; set; }
        #endregion

        /// <summary>
        /// Used in LSP
        /// </summary>
        public ILocation ForeachInWord { get; set; }

        public override string AAAName => nameof(AstForStmt);

        public AstForStmt(AstStatement first, AstExpression second, AstStatement third, AstBlockExpr body, ILocation location = null) : base(location)
        {
            FirstArgument = first;
            SecondArgument = second;
            ThirdArgument = third;
            Body = body;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstForStmt(
                FirstArgument.GetDeepCopy() as AstStatement,
                SecondArgument.GetDeepCopy() as AstExpression,
                ThirdArgument.GetDeepCopy() as AstStatement,
                Body.GetDeepCopy() as AstBlockExpr,
                Location)
            {
                ForeachArgument = ForeachArgument?.GetDeepCopy() as AstExpression,
                IsForeach = IsForeach,
                ForeachInWord = ForeachInWord,
                IsSyntheticStatement = IsSyntheticStatement,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            if (FirstArgument == oldChild)
                FirstArgument = newChild as AstStatement;
            else if (SecondArgument == oldChild)
                SecondArgument = newChild as AstExpression;
            else if (ThirdArgument == oldChild)
                ThirdArgument = newChild as AstStatement;
            else if (Body == oldChild)
                Body = newChild as AstBlockExpr;
            else if (ForeachArgument == oldChild)
                ForeachArgument = newChild as AstExpression;
        }
    }
}
