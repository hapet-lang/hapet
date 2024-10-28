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
				expr = ParseExpression(allowCommaTuple, true, null, true);

			if (expr is UnknownDecl udecl)
			{
				return PrepareUnknownDecl(udecl, docString, allowCommaTuple);
			}
			//else if (expr is AstFuncDecl funcDecl)
			//{
			//	// already prepared func (probably ctor or dtor)
			//	return funcDecl;
			//}
			// TODO: upper shite is probably not possible

			ReportError(PeekToken().Location, $"Unexpected token. Expected '=' or '\\n'");
			return new AstVarDecl(expr as AstIdExpr, null, null, docString, Location: expr);
		}
	}
}
