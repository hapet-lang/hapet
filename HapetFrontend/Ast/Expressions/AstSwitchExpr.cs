using HapetFrontend.Ast.Statements;

namespace HapetFrontend.Ast.Expressions
{
    public class AstSwitchExpr : AstExpression
    {
        /// <summary>
        /// The expression that is going to be matched
        /// </summary>
        public AstExpression SubExpression { get; set; }

        /// <summary>
        /// The cases that are in the switch
        /// </summary>
        public List<AstCaseExpr> Cases { get; set; }

        public override string AAAName => nameof(AstSwitchExpr);

        public AstSwitchExpr(AstExpression sub, List<AstCaseExpr> cases, ILocation location = null) : base(location)
        {
            SubExpression = sub;
            Cases = cases;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstSwitchExpr(
                SubExpression.GetDeepCopy() as AstExpression,
                Cases.Select(x => x.GetDeepCopy() as AstCaseExpr).ToList(),
                Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            if (SubExpression == oldChild)
                SubExpression = newChild as AstExpression;
            else if (Cases.Contains(oldChild))
            {
                int index = Cases.IndexOf(oldChild as AstCaseExpr);
                Cases[index] = newChild as AstCaseExpr;
            }
        }
    }

    public class AstCaseExpr : AstExpression
    {
        /// <summary>
        /// A const value that should match the switch' SubExpression
        /// </summary>
        public AstExpression Pattern { get; set; }

        /// <summary>
        /// The return value of case
        /// </summary>
        public AstExpression ReturnExpr { get; set; }

        /// <summary>
        /// 'true' if the case is default case
        /// </summary>
        public bool IsDefaultCase { get; set; }

        public override string AAAName => nameof(AstCaseExpr);

        public AstCaseExpr(AstExpression pattern, AstExpression returnExpr, ILocation location = null) : base(location)
        {
            Pattern = pattern;
            ReturnExpr = returnExpr;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstCaseExpr(
                Pattern?.GetDeepCopy() as AstExpression,
                ReturnExpr?.GetDeepCopy() as AstExpression,
                Location)
            {
                IsDefaultCase = IsDefaultCase,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            if (Pattern == oldChild)
                Pattern = newChild as AstExpression;
            else if (ReturnExpr == oldChild)
                ReturnExpr = newChild as AstExpression;
        }
    }
}
