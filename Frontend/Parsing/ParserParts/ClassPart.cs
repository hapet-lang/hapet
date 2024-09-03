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
			AstIdExpr className = null;

			beg = Consume(TokenType.KwClass, ErrMsg("keyword 'class'", "at beginning of class type")).Location;

			// class name
			if (!CheckToken(TokenType.Identifier))
			{
				// TODO: better error location
				ReportError(beg, $"Expected class name after 'class' keyword");
			}
			else
			{
				className = ParseIdentifierExpr();
			}

			// TODO: generic parsing
			//if (CheckToken(TokenType.OpenParen))
			//	parameters = ParseParameterList(TokenType.OpenParen, TokenType.ClosingParen, out var _, out var _);

			// TODO: inheritance parse

			while (CheckToken(TokenType.SharpIdentifier))
			{
				var dir = ParseDirective();
				if (dir != null)
					directives.Add(dir);
			}

			ConsumeUntil(TokenType.OpenBrace, ErrMsg("{", "at beginning of class body"), true);

			SkipNewlines();
			while (true)
			{
				var next = PeekToken();
				if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
					break;

				var memberDirectives = ParseDirectives(true);
				declarations.Add(ParseDeclaration(null, true, memberDirectives, false));

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
					ReportError(next.Location, $"Unexpected token {next} at end of class member");
				}
			}

			end = Consume(TokenType.CloseBrace, ErrMsg("}", "at end of class declaration")).Location;

			return new AstClassTypeExpr(parameters, declarations, directives, new Location(beg, end));
		}
	}
}
