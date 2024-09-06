using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using System.Diagnostics;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		private AstDeclaration ParseDeclaration(AstStatement expr, bool allowCommaTuple)
		{
			var docString = GetCurrentDocString();
			if (expr == null)
				expr = ParseExpression(allowCommaTuple);

			AstStatement initializer = null;
			TokenLocation end = expr.Ending;

			if (expr is UnknownDecl udecl)
			{
				// variable declaration with initializer
				if (CheckToken(TokenType.Equal))
				{
					NextToken();
					initializer = ParseExpression(allowCommaTuple);
					end = initializer.Ending;

					if (initializer is not AstExpression)
					{
						ReportError(initializer.Location, $"Variable initializer has to be an expresssion");
					}

					return new AstVarDecl(udecl.Type, udecl.Name, initializer as AstExpression, docString, Location: new Location(expr.Beginning, end));
				}
				// variable declaration without initializer
				else if (CheckToken(TokenType.Semicolon))
				{
					// do not get the next token
					return new AstVarDecl(udecl.Type, udecl.Name, null, docString, Location: new Location(expr.Beginning, end));
				}
				// func declaration 
				else if (CheckToken(TokenType.OpenParen))
				{
					var tpl = ParseTupleExpression(true, true);
					if (tpl is AstFuncDecl func)
					{
						func.Name = udecl.Name;
						func.Returns = udecl.Type;
						return func;
					}
					// TODO: could there be a lambda???
				}
				// TODO: properties
			}

			ReportError(PeekToken().Location, $"Unexpected token. Expected '=' or '\\n'");
			return new AstVarDecl(expr as AstIdExpr, null, null, docString, Location: expr);
		}
	}
}
