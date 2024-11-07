using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		private AstDeclaration ParseEnumDeclaration()
		{
			TokenLocation beg = null, end = null;
			var declarations = new List<AstVarDecl>();
			AstIdExpr enumName = null;

			beg = Consume(TokenType.KwEnum, ErrMsg("keyword 'enum'", "at beginning of enum type")).Location;

			// enum name
			if (!CheckToken(TokenType.Identifier))
			{
				// better error location
				ReportMessage(PeekToken().Location, $"Expected enum name after 'enum' keyword");
			}
			else
			{
				var nest = ParseIdentifierExpression(allowDots: false);
				if (nest.RightPart is not AstIdExpr idExpr)
				{
					ReportMessage(nest.Location, $"Enum name expected to be an identifier");
					return new AstEnumDecl(new AstIdExpr("unknown"), declarations, "", beg);
				}
				enumName = idExpr;
			}
			SkipNewlines();

			ConsumeUntil(TokenType.OpenBrace, ErrMsg("{", "at beginning of enum body"), true);

			SkipNewlines();
			while (true)
			{
				var next = PeekToken();
				if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
					break;

				// all enum fields are just identifiers
				if (!CheckToken(TokenType.Identifier))
				{
					NextToken();
					ReportMessage(CurrentToken.Location, $"Identified expected to be here");
					continue;
				}

				// getting decl parts
				AstExpression ini = null;
				var id = ParseIdentifierExpression(allowDots: false);
				TokenLocation fieldEnd = id.Ending;
				if (CheckToken(TokenType.Equal))
				{
					NextToken();
					var initStmt = ParseExpression(false, false, null, false);
					if (initStmt is not AstExpression)
						ReportMessage(initStmt.Location, $"Enum field initializer expected to be an expression");
					ini = initStmt as AstExpression;
					fieldEnd = ini.Ending;
				}
				// the declaration
				AstVarDecl decl = new AstVarDecl(new AstNestedExpr(new AstIdExpr("int"), null, id), id.RightPart as AstIdExpr, ini, "", new Location(id.Beginning, fieldEnd));

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
				else if (decl is AstVarDecl && next.Type == TokenType.Comma)
				{
					// it is just a ',' at the end of enum field
					NextToken();
					SkipNewlines();
				}
				else if (decl is not AstVarDecl)
				{
					NextToken();
					ReportMessage(decl.Location, $"The declaration type is not allowed in enum type");
				}
			}

			end = Consume(TokenType.CloseBrace, ErrMsg("}", "at end of enum declaration")).Location;

			// TODO: doc string
			return new AstEnumDecl(enumName, declarations, "", new Location(beg, end));
		}
	}
}
