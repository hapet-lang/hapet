using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Statements
{
    public class AstTryCatchStmt : AstStatement
    {
        /// <summary>
        /// Block of try stmt
        /// </summary>
        public AstBlockExpr TryBlock { get; set; }

        /// <summary>
        /// Blocks of catches
        /// </summary>
        public List<AstCatchStmt> CatchBlocks { get; set; }

        /// <summary>
        /// Block of finally stmt
        /// </summary>
        public AstBlockExpr FinallyBlock { get; set; }

        public override string AAAName => nameof(AstTryCatchStmt);

        public AstTryCatchStmt(AstBlockExpr tryBlock, List<AstCatchStmt> catchBlocks, AstBlockExpr finallyBlock, ILocation location = null) : base(location)
        {
            TryBlock = tryBlock;
            CatchBlocks = catchBlocks;
            FinallyBlock = finallyBlock;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstTryCatchStmt(
                TryBlock.GetDeepCopy() as AstBlockExpr,
                CatchBlocks.Select(x => x.GetDeepCopy() as AstCatchStmt).ToList(),
                FinallyBlock.GetDeepCopy() as AstBlockExpr,
                Location)
            {
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }

    public class AstCatchStmt : AstStatement
    {
        /// <summary>
        /// Block of catch stmt
        /// </summary>
        public AstBlockExpr CatchBlock { get; set; }

        /// <summary>
        /// Param of catch stmt
        /// </summary>
        public AstParamDecl CatchParam { get; set; }

        public override string AAAName => nameof(AstCatchStmt);

        public AstCatchStmt(AstBlockExpr catchBlock, AstParamDecl catchParam, ILocation location = null) : base(location)
        {
            CatchBlock = catchBlock;
            CatchParam = catchParam;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstCatchStmt(
                CatchBlock.GetDeepCopy() as AstBlockExpr,
                CatchParam.GetDeepCopy() as AstParamDecl,
                Location)
            {
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
