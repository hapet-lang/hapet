using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Errors;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        /// <summary>
        /// This function check that all the possible parts of the function
        /// can return a value. The idea is simple - if the current-last BB
        /// does not have any previous BB that are pointing the current-last BB
        /// then the current-last BB is inaccesable so there is enough returns above.
        /// https://softwareengineering.stackexchange.com/questions/386010/creating-a-control-flow-graph-for-a-given-function
        /// </summary>
        /// <param name="func">The function to check</param>
        public void CheckThatThereIsEnoughReturnsInFunc(AstFuncDecl func)
        {
            // skip with empty bodies and void return types
            if (func.Body == null || func.Returns.OutType is VoidType)
                return;
            // if there is already return stmt at the end of func - no need to check anything
            if (AstBlockExpr.IsBlockHasItsOwnBr(func.Body, true))
                return;

            var builder = new PPBasicBlockBuilder();
            builder.AppendBasicBlock(builder.CreateBasicBlock("entry"));
            builder.CurrentBlock.PreviousBlocks.Add(null); // entry block is always "accesable"
            CheckThatThereIsEnoughReturnsInBlock(func.Body, builder);

            // if the last BB has no previous BB - then all ok, else - error
            if (builder.CurrentBlock.PreviousBlocks.Count > 0)
            {
                // error
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, func.Name, 
                    [HapetType.AsString(func.Returns.OutType)], ErrorCode.Get(CTEN.NotEnoughReturns));
            } 
        }

        public void CheckThatThereIsEnoughReturnsInBlock(AstBlockExpr block, PPBasicBlockBuilder builder)
        {
            // go all over the statments and fill up blocks
            bool needBreakForLoop = false;
            foreach (var s in block.Statements)
            {
                if (needBreakForLoop)
                    break;
                switch (s)
                {
                    case AstIfStmt ifStmt:
                        {
                            var endBB = builder.CreateBasicBlock("if.end");
                            var trueBB = builder.CreateBasicBlock("if.true");
                            var falseBB = builder.CreateBasicBlock("if.false");

                            // if else block != null - both are possible
                            if (ifStmt.BodyFalse != null)
                            {
                                builder.BuildCondBr(trueBB, falseBB);
                            }
                            else
                            {
                                builder.BuildCondBr(trueBB, endBB);
                            }

                            // handle true body
                            builder.AppendBasicBlock(trueBB);
                            CheckThatThereIsEnoughReturnsInBlock(ifStmt.BodyTrue, builder);
                            // if no ret-break-cont - make our own
                            if (!(AstBlockExpr.IsBlockHasItsOwnBr(ifStmt.BodyTrue, true) || builder.CurrentBlock.PreviousBlocks.Count == 0))
                                builder.BuildBr(endBB);

                            // do the same if not null
                            if (ifStmt.BodyFalse != null)
                            {
                                // handle false body
                                builder.AppendBasicBlock(falseBB);
                                CheckThatThereIsEnoughReturnsInBlock(ifStmt.BodyFalse, builder);
                                // if no ret-break-cont - make our own
                                if (!(AstBlockExpr.IsBlockHasItsOwnBr(ifStmt.BodyFalse, true) || builder.CurrentBlock.PreviousBlocks.Count == 0))
                                    builder.BuildBr(endBB);
                            }

                            // go to end
                            builder.AppendBasicBlock(endBB);
                            break;
                        }
                    case AstSwitchStmt switchStmt:
                        {
                            // if switch has 'default' case with return - all good
                            var endBB = builder.CreateBasicBlock("switch.end");
                            var defaultBB = builder.CreateBasicBlock("swtich.default.entry");

                            var defaultCase = switchStmt.Cases.FirstOrDefault(x => x.IsDefaultCase);
                            if (defaultCase != null)
                            {
                                builder.AppendBasicBlock(defaultBB);
                                CheckThatThereIsEnoughReturnsInBlock(defaultCase.Body, builder);
                                // if not return stmt - go br end
                                if (!(AstBlockExpr.IsBlockHasItsOwnBr(defaultCase.Body, true) || builder.CurrentBlock.PreviousBlocks.Count == 0))
                                    builder.BuildBr(endBB);
                            }
                            else
                            {
                                // if no default case - go br end
                                builder.BuildBr(endBB);
                            }
                            builder.AppendBasicBlock(endBB);
                            break;
                        }
                    default:
                        {
                            // if the current block is not accessable - then no need to go further
                            if (builder.CurrentBlock.PreviousBlocks.Count == 0)
                            {
                                needBreakForLoop = true;
                                break;
                            }
                            // do not add nested func declarations 
                            if (s is not AstFuncDecl)
                            {
                                // just add a stmt to current block
                                builder.CurrentBlock.BlockStatements.Add(s);
                            }
                            break;
                        }
                }
            }
        }
    }
}
