using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using System.Collections.Generic;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		private AstStatement ParseUsingStatement()
		{
			TokenLocation beg = null;
			bool isAttach = false;
			AstStatement asWhat = null;

			if (CheckToken(TokenType.KwAttach))
			{
				isAttach = true;
				Consume(TokenType.KwAttach, ErrMsg("keyword 'attach'", "at beginning of 'using' statement"));
			}

			beg ??= Consume(TokenType.KwUsing, ErrMsg("keyword 'using'", "at beginning of 'using' statement")).Location;
			SkipNewlines();
			var expr = ParseIdentifierExpression(ErrMsg("expression", "after keyword 'using'"));

			if (expr is not AstIdExpr)
			{
				ReportError(expr.Location, "Module name/path expected after 'using' keyword");
				return ParseEmptyExpression();
			}

			if (CheckToken(TokenType.KwAs))
			{
				NextToken();
				asWhat = ParseIdentifierExpression(ErrMsg("expression", "after keyword 'as' in 'using' statement"));
				if (asWhat is not AstIdExpr)
				{
					ReportError(asWhat.Location, "Module aliasing expected after 'as' keyword in 'using' keyword");
					return ParseEmptyExpression();
				}
			}

			return new AstUsingStmt(expr, isAttach, asWhat as AstIdExpr, Location: new Location(beg));
		}
	}
}
