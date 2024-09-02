using Frontend.Ast.Expressions;
using Frontend.Ast;
using Frontend.Ast.Statements;
using Frontend.Parsing.Entities;

namespace Frontend.Parsing
{
	public partial class Parser
	{
		public AstExprStmt ParseBlockStatement()
		{
			var expr = ParseBlockExpr();
			return new AstExprStmt(expr, expr.Location);
		}

		private AstBlockExpr ParseBlockExpr()
		{
			var statements = new List<AstStatement>();
			var beg = Consume(TokenType.OpenBrace, ErrMsg("{", "at beginning of block statement")).Location;

			AstIdExpr label = null;

			if (CheckToken(TokenType.SharpIdentifier))
			{
				var id = NextToken();
				if (id.Data as string == "label")
				{
					label = ParseIdentifierExpr();
				}
				else
				{
					ReportError(id.Location, $"Unexpected token");
				}
			}

			SkipNewlines();
			while (true)
			{
				var next = PeekToken();
				if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
					break;

				var s = ParseStatement(false);
				if (s != null)
				{
					statements.Add(s);

					next = PeekToken();

					if (next.Type == TokenType.CloseBrace || next.Type == TokenType.EOF)
						break;

					switch (s)
					{
						// TODO: wtf???
						case AstExprStmt es when es.Expr is AstBlockExpr || es.Expr is AstIfExpr:
							break;

						default:
							if (!Expect(TokenType.NewLine, ErrMsg("\\n", "after statement")))
								RecoverStatement();
							break;
					}

				}
				SkipNewlines();
			}

			var end = Consume(TokenType.CloseBrace, ErrMsg("}", "at end of block statement")).Location;

			return new AstBlockExpr(statements, label, new Location(beg, end));
		}
	}
}
