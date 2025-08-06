using HapetFrontend.Ast;

namespace HapetPostPrepare.Entities
{
    public class PPBasicBlock
    {
        public string Name { get; set; }
        public List<PPBasicBlock> PreviousBlocks { get; private set; } = new List<PPBasicBlock>();
        public List<PPBasicBlock> NextBlocks { get; private set; } = new List<PPBasicBlock>();

        public List<AstStatement> BlockStatements { get; private set; } = new List<AstStatement>();

        public override string ToString()
        {
            return Name;
        }
    }

    public class PPBasicBlockBuilder
    {
        public List<PPBasicBlock> Blocks { get; private set; } = new List<PPBasicBlock>();
        public PPBasicBlock CurrentBlock { get; private set; }

        public PPBasicBlock CreateBasicBlock(string name)
        {
            var block = new PPBasicBlock() { Name = name };
            return block;
        }

        public void AppendBasicBlock(PPBasicBlock block)
        {
            Blocks.Add(block);
            CurrentBlock = block;
        }

        public void AppendStatement(AstStatement stmt)
        {
            CurrentBlock.BlockStatements.Add(stmt);
        }

        public void BuildBr(PPBasicBlock nextBlock)
        {
            CurrentBlock.NextBlocks.Add(nextBlock);
            nextBlock.PreviousBlocks.Add(CurrentBlock);
        }

        public void BuildCondBr(PPBasicBlock trueBlock, PPBasicBlock falseBlock)
        {
            CurrentBlock.NextBlocks.Add(trueBlock);
            CurrentBlock.NextBlocks.Add(falseBlock);
            trueBlock.PreviousBlocks.Add(CurrentBlock);
            falseBlock.PreviousBlocks.Add(CurrentBlock);
        }
    }
}
