using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
    public class AstSwitchStmt : AstStatement
    {
        /// <summary>
        /// The expression that is going to be matched
        /// </summary>
        public AstExpression SubExpression { get; set; }

        /// <summary>
        /// The cases that are in the switch
        /// </summary>
        public List<AstCaseStmt> Cases { get; set; }

        public override string AAAName => nameof(AstSwitchStmt);

        public AstSwitchStmt(AstExpression sub, List<AstCaseStmt> cases, ILocation location = null) : base(location)
        {
            SubExpression = sub;
            Cases = cases;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstSwitchStmt(
                SubExpression.GetDeepCopy() as AstExpression,
                Cases.Select(x => x.GetDeepCopy() as AstCaseStmt).ToList(),
                Location)
            {
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }

    public class AstCaseStmt : AstStatement
    {
        /// <summary>
        /// A const value that should match the switch' SubExpression
        /// </summary>
        public AstExpression Pattern { get; set; }

        /// <summary>
        /// The body of the 'case'
        /// </summary>
        public AstBlockExpr Body { get; set; }

        /// <summary>
        /// 'true' if the case is default case
        /// </summary>
        public bool IsDefaultCase { get; set; }

        /// <summary>
        /// The case that is just falling into lower one like: <br/>
        /// case (0) <br/>
        /// case (1) <br/>
        /// { <br/>
        ///		... <br/>
        ///	} <br/>
        ///	Where the 'case (0)' would be Falling
        /// </summary>
        public bool IsFallingCase { get; set; }

        public AstCaseStmt(AstExpression pattern, AstBlockExpr body, ILocation location = null) : base(location)
        {
            Pattern = pattern;
            Body = body;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstCaseStmt(
                Pattern.GetDeepCopy() as AstExpression,
                Body.GetDeepCopy() as AstBlockExpr,
                Location)
            {
                IsDefaultCase = IsDefaultCase,
                IsFallingCase = IsFallingCase,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
