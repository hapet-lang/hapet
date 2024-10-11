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

			beg ??= Consume(TokenType.KwUsing, ErrMsg("keyword 'using'", "at beginning of 'using' statement")).Location;
			SkipNewlines();
			var expr = ParseIdentifierExpression(ErrMsg("expression", "after keyword 'using'"));

			if (expr is not AstNestedExpr)
			{
				ReportError(expr.Location, "Module name/path expected after 'using' keyword");
				return ParseEmptyExpression();
			}

			return new AstUsingStmt(expr, Location: new Location(beg));
		}
	}
}
