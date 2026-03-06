using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstTryCatchStmt ParseTryCatchStatement(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var beg = Consume(inInfo, TokenType.KwTry, ErrMsg("keyword 'try'", "at beginning of 'try' statement"));
            SkipNewlines(inInfo);

            // parsing the block
            var tryBlock = GetLoopOrCondBlock(inInfo, ref outInfo);
            SkipNewlines(inInfo);
            var catchBlocks = new List<AstCatchStmt>();
            AstBlockExpr finallyBlock = null;
            
            // get all catch blocks
            while (CheckToken(inInfo, TokenType.KwCatch))
            {
                var catchBeg = Consume(inInfo, TokenType.KwCatch, ErrMsg("keyword 'catch'", "at beginning of 'catch' statement"));
                SkipNewlines(inInfo);
                AstParamDecl par = null;
                bool isCommonCatch = false;
                TokenLocation catchEnd = null;

                // parse param
                if (CheckToken(inInfo, TokenType.OpenParen))
                {
                    Consume(inInfo, TokenType.OpenParen, ErrMsg("'('", "at the begining of 'catch' statement"));
                    // if there is a condition param
                    if (!CheckToken(inInfo, TokenType.CloseParen))
                        par = ParseParameter(inInfo, ref outInfo, allowDefaultValue: false);
                    else
                        ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.CaseParamExpected)); // TODO: catch param - create error
                    catchEnd = Consume(inInfo, TokenType.CloseParen, ErrMsg("')'", "after the param")).Location;
                }
                else
                {
                    isCommonCatch = true;
                }

                SkipNewlines(inInfo);

                // parse the block of catch stmt
                var catchBlock = GetLoopOrCondBlock(inInfo, ref outInfo);

                // create the catch stmt
                var catchStmt = new AstCatchStmt(catchBlock, par, new Location(catchBeg.Location, catchEnd ?? catchBeg.Location))
                {
                    IsCommonCatch = isCommonCatch,
                };
                catchBlocks.Add(catchStmt);
                SkipNewlines(inInfo);
            }

            Token finallyTkn = null;
            // get finally block
            if (CheckToken(inInfo, TokenType.KwFinally))
            {
                finallyTkn = Consume(inInfo, TokenType.KwFinally, ErrMsg("keyword 'finally'", "at beginning of 'finally' statement"));
                SkipNewlines(inInfo);
                finallyBlock = GetLoopOrCondBlock(inInfo, ref outInfo);
                SkipNewlines(inInfo);
            }

            // error if there are parametrized catch blocks after IsCommonCatch block
            bool gotCommonCatch = false;
            foreach (var catchBlock in catchBlocks)
            {
                if (catchBlock.IsCommonCatch && !gotCommonCatch)
                {
                    gotCommonCatch = true;
                    continue;
                }
                // if there was already common catch block - error
                if (gotCommonCatch)
                    ReportMessage(catchBlock.Location.Beginning, [], ErrorCode.Get(CTEN.CatchAfterCommonCatch));
            }

            // error if no catch or finally blocks
            if (catchBlocks.Count == 0 && finallyBlock == null)
                ReportMessage(beg.Location, [], ErrorCode.Get(CTEN.NoFinallyOrCatchBlockFound));

            var tryCatchStmt = new AstTryCatchStmt(tryBlock, catchBlocks, finallyBlock, beg.Location)
            {
                FinallyTokenLocation = finallyTkn?.Location,
            };
            return tryCatchStmt;
        }
    }
}
