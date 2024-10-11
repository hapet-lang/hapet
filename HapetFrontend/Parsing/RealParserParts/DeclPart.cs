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
				expr = ParseExpression(allowCommaTuple, true);

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

					var varDecl = new AstVarDecl(udecl.Type, udecl.Name, initializer as AstExpression, docString, Location: new Location(expr.Beginning, end));
					varDecl.SpecialKeys.AddRange(udecl.SpecialKeys);
					return varDecl;
				}
				// variable declaration without initializer
				else if (CheckToken(TokenType.Semicolon))
				{
					// do not get the next token
					var varDecl = new AstVarDecl(udecl.Type, udecl.Name, null, docString, Location: new Location(expr.Beginning, end));
					varDecl.SpecialKeys.AddRange(udecl.SpecialKeys);
					return varDecl;
				}
				// func declaration 
				else if (CheckToken(TokenType.OpenParen))
				{
					var tpl = ParseTupleExpression(true, true);
					if (tpl is AstFuncDecl func)
					{
						if (udecl.Type == null)
						{
							// it is ctor/dtor
							func.Name = udecl.Name.GetCopy(udecl.Name.Name + (udecl.Name.Suffix != "~" ? "_ctor" : "_dtor"));
							func.Returns = new AstIdExpr("void");
							func.ClassFunctionTypes.Add(udecl.Name.Suffix != "~" ? Enums.ClassFunctionType.Ctor : Enums.ClassFunctionType.Dtor);
						}
						else
						{
							// it is normal func
							func.Name = udecl.Name;
							func.Returns = udecl.Type;
						}

						func.SpecialKeys.AddRange(udecl.SpecialKeys);
						return func;
					}
					// TODO: could there be a lambda???
				}
				// TODO: properties with { get; set; }
			}
			else if (expr is AstFuncDecl funcDecl)
			{
				// already prepared func (probably ctor or dtor)
				return funcDecl;
			}

			ReportError(PeekToken().Location, $"Unexpected token. Expected '=' or '\\n'");
			return new AstVarDecl(expr as AstIdExpr, null, null, docString, Location: expr);
		}
	}
}
