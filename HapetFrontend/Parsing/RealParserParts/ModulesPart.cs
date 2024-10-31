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

			beg ??= Consume(TokenType.KwUsing, ErrMsg("keyword 'using'", "at beginning of 'using' statement")).Location;
			SkipNewlines();
			var expr = ParseIdentifierExpression(ErrMsg("expression", "after keyword 'using'"));

			if (expr is not AstNestedExpr)
			{
				ReportMessage(expr.Location, "Namespace name/path expected after 'using' keyword");
				return ParseEmptyExpression();
			}

			return new AstUsingStmt(expr, Location: new Location(beg));
		}

		private AstStatement ParseNamespaceStatement()
		{
			TokenLocation beg = null;

			beg ??= Consume(TokenType.KwNamespace, ErrMsg("keyword 'namespace'", "at beginning of 'namespace' statement")).Location;
			SkipNewlines();
			var expr = ParseIdentifierExpression(ErrMsg("expression", "after keyword 'namespace'"));

			if (expr is not AstNestedExpr)
			{
				ReportMessage(expr.Location, "Namespace name/path expected after 'namespace' keyword");
				return ParseEmptyExpression();
			}

			return new AstNamespaceStmt(expr, Location: new Location(beg));
		}
	}
}
