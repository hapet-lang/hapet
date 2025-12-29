using HapetFrontend.Ast.Expressions;
using System.Xml.Linq;

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
                int index = Cases.IndexOf(oldChild as AstCaseStmt);
                Cases[index] = newChild as AstCaseStmt;
            }
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

        /// <summary>
        /// The label used by goto
        /// </summary>
        public string LabelForGoto { get; set; }

        /// <summary>
        /// Used in LSP
        /// </summary>
        public ILocation GotoLabelLocation { get; set; }

        public AstCaseStmt(AstExpression pattern, AstBlockExpr body, ILocation location = null) : base(location)
        {
            Pattern = pattern;
            Body = body;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstCaseStmt(
                Pattern?.GetDeepCopy() as AstExpression,
                Body?.GetDeepCopy() as AstBlockExpr,
                Location)
            {
                LabelForGoto = LabelForGoto,
                IsDefaultCase = IsDefaultCase,
                IsFallingCase = IsFallingCase,
                Scope = Scope,
                SourceFile = SourceFile,
                GotoLabelLocation = GotoLabelLocation,
            };
            return copy;
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            if (Pattern == oldChild)
                Pattern = newChild as AstExpression;
            else if (Body == oldChild)
                Body = newChild as AstBlockExpr;
        }
    }
}
