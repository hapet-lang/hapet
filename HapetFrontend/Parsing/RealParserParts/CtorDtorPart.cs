using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using System;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		private AstStatement ParseCtorDtorExpression(TokenType tkn)
		{
			TokenLocation beg = null;

			beg ??= Consume(tkn, ErrMsg($"keyword '{tkn}'", "before the constructor declaration")).Location;
			SkipNewlines();
			var typeName = ParseIdentifierExpression(ErrMsg("expression", $"after keyword '{tkn}'"));

			if (typeName.RightPart is not AstIdExpr idExprName)
			{
				ReportError(PeekToken().Location, $"Ctor/Dtor name expected to be an identifier");
				return ParseEmptyExpression();
			}

			if (!CheckToken(TokenType.OpenParen))
			{
				ReportError(PeekToken().Location, $"Expected '(' after ctor/dtor name");
				return ParseEmptyExpression();
			}

			var tpl = ParseTupleExpression(true, true);
			if (tpl is not AstFuncDecl func)
			{
				ReportError(PeekToken().Location, $"Class ctor/dtor expected to be a func type");
				return ParseEmptyExpression();
			}

			// setting prefix of ctod/dtor names
			func.Name = idExprName.GetCopy(idExprName.Name + (tkn == TokenType.KwCtor ? "_ctor" : "_dtor"));
			func.Returns = new AstIdExpr("void");
			func.ClassFunctionTypes.Add(tkn == TokenType.KwCtor ? Enums.ClassFunctionType.Ctor : Enums.ClassFunctionType.Dtor);

			return func;
		}
	}
}
