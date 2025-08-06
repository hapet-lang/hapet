using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Scoping;

namespace HapetFrontend.Ast.Expressions
{
    public class AstBlockExpr : AstExpression
    {
        /// <summary>
        /// The statements that are in the block
        /// </summary>
        public List<AstStatement> Statements { get; set; }

        /// <summary>
        /// The inner scope of the block. Used to get access to it's content
        /// </summary>
        public Scope SubScope { get; set; }

        public override string AAAName => nameof(AstBlockExpr);

        public AstBlockExpr(List<AstStatement> statements, ILocation location = null) : base(location)
        {
            Statements = statements;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstBlockExpr(
                Statements.Select(x => x.GetDeepCopy() as AstStatement).ToList(),
                Location)
            {
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
                TupleNameList = TupleNameList,
            };
            return copy;
        }

        // helpers

        public static bool IsBlockHasItsOwnBr(AstBlockExpr block, bool returnAndThrowOnly = false)
        {
            // if the last statement of the block is already
            // a return then there is no
            // need to create our own!!!
            return block != null && IsBlockHasItsOwnBr(block.Statements, returnAndThrowOnly);
        }

        public static bool IsBlockHasItsOwnBr(List<AstStatement> stmts, bool returnAndThrowOnly = false)
        {
            // if the last statement of the block is already
            // a return then there is no
            // need to create our own!!!
            if (stmts.Count <= 0)
                return false;
            if (stmts.Last() is AstReturnStmt)
                return true;
            if (stmts.Last() is AstThrowStmt)
                return true;
            if (!returnAndThrowOnly && stmts.Last() is AstBreakContStmt)
                return true;
            return false;
        }
    }
}
