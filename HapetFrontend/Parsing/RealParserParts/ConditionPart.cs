using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Statements;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		private AstIfStmt ParseIfStatement()
		{
			AstExpression condition = null;
			AstBlockExpr bodyTrue;
			AstBlockExpr bodyFalse = null;

			var beg = Consume(TokenType.KwIf, ErrMsg("keyword 'if'", "at beginning of 'if' statement"));

			// parse arguments
			if (!CheckToken(TokenType.OpenParen))
			{
				// TODO: error excepted open paren
			}
			Consume(TokenType.OpenParen, ErrMsg("'('", "at the begining of 'if' statement"));

			// if there is a condition param
			if (!CheckToken(TokenType.CloseParen))
				condition = ParseExpression(true, false) as AstExpression; // TODO: error if it is not an expr
			else
				ReportError(PeekToken().Location, $"Condition of 'if' statement expected");
			var end = Consume(TokenType.CloseParen, ErrMsg("')'", "after the condition"));

			SkipNewlines();

			// TODO: check if there is not only a '{' but could be a ';'
			// because exprs like 'if (false) ;' should also be handled
			// if there is no '{' just create an empty block

			// parsing the block
			bodyTrue = ParseBlockExpression();

			// if there is an 'else' block
			if (CheckToken(TokenType.KwElse))
			{
				Consume(TokenType.KwElse, ErrMsg("keyword 'else'", "at beginning of 'else' statement"));
				// parsing the block
				bodyFalse = ParseBlockExpression();
			}

			return new AstIfStmt(condition, bodyTrue, bodyFalse, new Location(beg.Location, end.Location));
		}
	}
}
