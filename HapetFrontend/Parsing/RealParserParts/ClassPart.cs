using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		private AstDeclaration ParseClassDeclaration()
		{
			TokenLocation beg = null, end = null;
			var declarations = new List<AstDeclaration>();
			AstIdExpr className = null;

			beg = Consume(TokenType.KwClass, ErrMsg("keyword 'class'", "at beginning of class type")).Location;

			// class name
			if (!CheckToken(TokenType.Identifier))
			{
				// better error location
				ReportError(PeekToken().Location, $"Expected class name after 'class' keyword");
			}
			else
			{
				var nest = ParseIdentifierExpression(allowDots: false);
				if (nest.RightPart is not AstIdExpr idExpr)
				{
					ReportError(nest.Location, $"Class name expected to be an identifier");
					return new AstClassDecl(new AstIdExpr("unknown"), declarations, "", beg);
				}
				className = idExpr;
			}

			ConsumeUntil(TokenType.OpenBrace, ErrMsg("{", "at beginning of class body"), true);

			SkipNewlines();
			while (true)
			{
				var next = PeekToken();
				if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
					break;

				var decl = ParseDeclaration(null, true);
				declarations.Add(decl);				

				next = PeekToken();
				if (next.Type == TokenType.NewLine)
				{
					SkipNewlines();
				}
				else if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
				{
					break;
				}
				else if (decl is AstVarDecl && next.Type == TokenType.Semicolon)
				{
					// it is just a ';' at the end of class field
					NextToken();
					SkipNewlines();
				}
				else
				{
					NextToken();
					ReportError(next.Location, $"Unexpected token {next} at end of class member");
				}
			}

			end = Consume(TokenType.CloseBrace, ErrMsg("}", "at end of class declaration")).Location;

			// TODO: doc string
			return new AstClassDecl(className, declarations, "", new Location(beg, end));
		}
	}
}
