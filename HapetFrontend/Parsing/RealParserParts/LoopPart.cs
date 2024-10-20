using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Ast;
using System.Collections.Generic;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		private AstForStmt ParseForStatement()
		{
			AstStatement first = null;
			AstExpression second = null;
			AstStatement third = null;
			AstBlockExpr body;

			var beg = Consume(TokenType.KwFor, ErrMsg("keyword 'for'", "at beginning of 'for' loop"));

			// parse arguments
			if (!CheckToken(TokenType.OpenParen))
			{
				// TODO: error excepted open paren
			}
			Consume(TokenType.OpenParen, ErrMsg("'('", "at the begining of 'for' loop statement"));

			// if there is a first param
			if (!CheckToken(TokenType.Semicolon))
				first = ParseStatement(false);
			// Consume(TokenType.Semicolon, ErrMsg("';'", "after the first argument")); // WARN: semicolon is parsed inside ParseStatement

			// if there is a second param
			if (!CheckToken(TokenType.Semicolon))
				second = ParseExpression(true, false) as AstExpression; // TODO: error if it is not an expr
			Consume(TokenType.Semicolon, ErrMsg("';'", "after the second argument"));

			// if there is a third param
			if (!CheckToken(TokenType.CloseParen))
				third = ParseStatement(false);
			var end = Consume(TokenType.CloseParen, ErrMsg("')'", "after the third argument"));

			SkipNewlines();

			// TODO: check if there is not only a '{' but could be a ';'
			// because exprs like 'for (;;) ;' should also be handled
			// if there is no '{' just create an empty block

			// parsing the block
			body = ParseBlockExpression();

			return new AstForStmt(first, second, third, body, new Location(beg.Location, end.Location));
		}

		private AstWhileStmt ParseWhileStatement()
		{
            AstExpression condition = null;
            AstBlockExpr body;

            var beg = Consume(TokenType.KwWhile, ErrMsg("keyword 'while'", "at beginning of 'while' loop"));

            // parse arguments
            if (!CheckToken(TokenType.OpenParen))
            {
                // TODO: error excepted open paren
            }
            Consume(TokenType.OpenParen, ErrMsg("'('", "at the begining of 'while' loop statement"));

            // if there is a condition param
            if (!CheckToken(TokenType.CloseParen))
                condition = ParseExpression(true, false) as AstExpression; // TODO: error if it is not an expr
			else
                ReportError(PeekToken().Location, $"Condition of 'while' loop expected");
            var end = Consume(TokenType.CloseParen, ErrMsg("')'", "after the condition"));

            SkipNewlines();

            // TODO: check if there is not only a '{' but could be a ';'
            // because exprs like 'while (false) ;' should also be handled
            // if there is no '{' just create an empty block

            // parsing the block
            body = ParseBlockExpression();

            return new AstWhileStmt(condition, body, new Location(beg.Location, end.Location));
        }
    }
}
