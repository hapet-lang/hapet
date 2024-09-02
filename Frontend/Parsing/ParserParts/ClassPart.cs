using Frontend.Ast;
using Frontend.Ast.Expressions;
using Frontend.Parsing.Entities;

namespace Frontend.Parsing
{
	public partial class Parser
	{
		private AstExpression ParseClassTypeExpression()
		{
			TokenLocation beg = null, end = null;
			var declarations = new List<AstDeclaration>();
			var directives = new List<AstDirective>();
			List<AstParameter> parameters = null;

			var tkn = Consume(TokenType.KwClass, ErrMsg("keyword 'class'", "at beginning of class type"));
			beg = tkn.Location;

			// TODO: generic parsing
			//if (CheckToken(TokenType.OpenParen))
			//	parameters = ParseParameterList(TokenType.OpenParen, TokenType.ClosingParen, out var _, out var _);

			while (CheckToken(TokenType.SharpIdentifier))
			{
				var dir = ParseDirective();
				if (dir != null)
					directives.Add(dir);
			}

			ConsumeUntil(TokenType.OpenBrace, ErrMsg("{", "at beginning of class body"));

			SkipNewlines();
			while (true)
			{
				var next = PeekToken();
				if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
					break;

				var memberDirectives = ParseDirectives(true);
				declarations.Add(ParseDeclaration(null, false, true, memberDirectives, false));

				next = PeekToken();
				if (next.Type == TokenType.NewLine)
				{
					SkipNewlines();
				}
				else if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
				{
					break;
				}
				else
				{
					NextToken();
					ReportError(next.Location, $"Unexpected token {next} at end of trait member");
				}
			}

			end = Consume(TokenType.CloseBrace, ErrMsg("}", "at end of trait declaration")).Location;

			return new AstClassTypeExpr(parameters, declarations, directives, new Location(beg, end));
		}
	}
}
