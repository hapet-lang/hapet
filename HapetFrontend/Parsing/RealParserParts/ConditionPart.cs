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
				ReportMessage(PeekToken().Location, $"Condition of 'if' statement expected");
			var end = Consume(TokenType.CloseParen, ErrMsg("')'", "after the condition"));

			SkipNewlines();

			// TODO: check if there is not only a '{' but could be a ';'
			// because exprs like 'if (false) ;' should also be handled
			// if there is no '{' just create an empty block

			// parsing the block
			if (CheckToken(TokenType.OpenBrace))
			{
				bodyTrue = ParseBlockExpression();
			}
			else
			{
				// getting only one stmt if there are no braces
				var onlyStmt = ParseStatement(false);
				bodyTrue = new AstBlockExpr(new List<AstStatement>() { onlyStmt }, onlyStmt);
			}

			SkipNewlines();

			// if there is an 'else' block
			if (CheckToken(TokenType.KwElse))
			{
				Consume(TokenType.KwElse, ErrMsg("keyword 'else'", "at beginning of 'else' statement"));
				SkipNewlines();

				// parsing the block
				if (CheckToken(TokenType.OpenBrace))
				{
					bodyFalse = ParseBlockExpression();
				}
				else
				{
					// getting only one stmt if there are no braces
					var onlyStmt = ParseStatement(false);
					bodyFalse = new AstBlockExpr(new List<AstStatement>() { onlyStmt }, onlyStmt);
				}
			}

			return new AstIfStmt(condition, bodyTrue, bodyFalse, new Location(beg.Location, end.Location));
		}

		private AstStatement ParseSwitchStatement()
		{
			AstExpression condition = null;

			var beg = Consume(TokenType.KwSwitch, ErrMsg("keyword 'switch'", "at beginning of 'switch' statement"));

			// parse arguments
			if (!CheckToken(TokenType.OpenParen))
			{
				// TODO: error excepted open paren
			}
			Consume(TokenType.OpenParen, ErrMsg("'('", "at the begining of 'switch' statement"));

			// if there is a condition param
			if (!CheckToken(TokenType.CloseParen))
				condition = ParseExpression(true, false) as AstExpression; // TODO: error if it is not an expr
			else
				ReportMessage(PeekToken().Location, $"Condition of 'switch' statement expected");
			var end = Consume(TokenType.CloseParen, ErrMsg("')'", "after the condition"));

			SkipNewlines();

			// parsing the block
			if (CheckToken(TokenType.OpenBrace))
			{
				var theBlock = ParseBlockExpression();
				List<AstCaseStmt> cases = new List<AstCaseStmt>();

				// serching for default
				AstExpression prevWasDefault = null;
				foreach (var s in theBlock.Statements)
				{
					if (s is not AstCaseStmt caseStmt)
					{
						if (s is AstDefaultExpr defExpr)
						{
							prevWasDefault = defExpr;
							continue;
						}

						// this cringe is done to handle default case :((
						if (s is AstBlockExpr block && prevWasDefault != null)
						{
							cases.Add(new AstCaseStmt(null, block, prevWasDefault) { DefaultCase = true });
							prevWasDefault = null;
						}
						else if (s is AstStatement stmt && prevWasDefault != null)
						{
							cases.Add(new AstCaseStmt(null, new AstBlockExpr(new List<AstStatement>() { stmt }, stmt), prevWasDefault) { DefaultCase = true });
							prevWasDefault = null;
						}
						else
						{
							// TODO: error here. all the statements have to be cases
						}
						continue;
					}
					cases.Add(caseStmt);
				}

				return new AstSwitchStmt(condition, cases, new Location(beg.Location, end.Location));
			}
			else
			{
				// TODO: error here. it has to have braces
				return ParseEmptyExpression();
			}
		}

		private AstStatement ParseCaseStatement()
		{
			AstExpression pattern = null;
			AstBlockExpr body = null;
			bool isDefault = false;
			bool isFalling = false;

			Token beg;
			Token end;

			// the case could start with 'default' word
			if (CheckToken(TokenType.KwDefault))
			{
				isDefault = true;
				beg = Consume(TokenType.KwDefault, ErrMsg("keyword 'default'", "at beginning of 'default' case statement"));
			}
			else
			{
				beg = Consume(TokenType.KwCase, ErrMsg("keyword 'case'", "at beginning of 'case' statement"));
			}

			// by default :)
			end = beg;

			// getting an expr after the 'case' word
			if (!isDefault)
			{
				// parse arguments
				if (!CheckToken(TokenType.OpenParen))
				{
					// TODO: error excepted open paren
				}
				Consume(TokenType.OpenParen, ErrMsg("'('", "at the begining of 'case' statement"));

				// if there is a condition param
				if (!CheckToken(TokenType.CloseParen))
					pattern = ParseExpression(true, false) as AstExpression; // TODO: error if it is not an expr
				else
					ReportMessage(PeekToken().Location, $"Condition of 'case' statement expected");
				end = Consume(TokenType.CloseParen, ErrMsg("')'", "after the pattern"));
			}

			SkipNewlines();

			// parsing the block
			if (CheckToken(TokenType.OpenBrace))
			{
				body = ParseBlockExpression();
			}
			else if (CheckToken(TokenType.KwCase))
			{
				isFalling = true;
			}
			else
			{
				// getting only one stmt if there are no braces
				var onlyStmt = ParseStatement(false);
				body = new AstBlockExpr(new List<AstStatement>() { onlyStmt }, onlyStmt);
			}

			var cs = new AstCaseStmt(pattern, body);
			cs.DefaultCase = isDefault;
			cs.FallingCase = isFalling;
			return cs;
		}
	}
}
