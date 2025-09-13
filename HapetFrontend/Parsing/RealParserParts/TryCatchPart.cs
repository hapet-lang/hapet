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

                // parse param
                Consume(inInfo, TokenType.OpenParen, ErrMsg("'('", "at the begining of 'catch' statement"));
                // if there is a condition param
                if (!CheckToken(inInfo, TokenType.CloseParen))
                    par = ParseParameter(inInfo, ref outInfo, allowDefaultValue: false);
                else
                    ReportMessage(PeekToken(inInfo).Location, [], ErrorCode.Get(CTEN.CaseParamExpected)); // TODO: catch param - create error
                var catchEnd = Consume(inInfo, TokenType.CloseParen, ErrMsg("')'", "after the param")).Location;
                SkipNewlines(inInfo);

                // parse the block of catch stmt
                var catchBlock = GetLoopOrCondBlock(inInfo, ref outInfo);

                // create the catch stmt
                var catchStmt = new AstCatchStmt(catchBlock, par, new Location(catchBeg.Location, catchEnd));
                catchBlocks.Add(catchStmt);
                SkipNewlines(inInfo);
            }

            // get finally block
            if (CheckToken(inInfo, TokenType.KwFinally))
            {
                Consume(inInfo, TokenType.KwFinally, ErrMsg("keyword 'finally'", "at beginning of 'finally' statement"));
                SkipNewlines(inInfo);
                finallyBlock = GetLoopOrCondBlock(inInfo, ref outInfo);
                SkipNewlines(inInfo);
            }

            var tryCatchStmt = new AstTryCatchStmt(tryBlock, catchBlocks, finallyBlock, beg.Location);
            return tryCatchStmt;
        }
    }
}
