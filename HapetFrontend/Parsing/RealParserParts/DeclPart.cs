using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using System.Diagnostics;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private readonly List<AstAttributeStmt> _foundAttributes = new List<AstAttributeStmt>();

        private AstDeclaration ParseDeclaration(AstStatement expr, bool allowCommaTuple)
        {
            var docString = GetCurrentDocString();
            if (expr == null)
                expr = ParseExpression(allowCommaTuple, true, null, true);

            if (expr is UnknownDecl udecl)
            {
                SkipNewlines();
                var result = PrepareUnknownDecl(udecl, docString, allowCommaTuple, _foundAttributes);
                // clearing found attr because they were applied to the decl
                _foundAttributes.Clear();
                return result;
            }
            else if (expr is AstAttributeStmt attrStmt)
            {
                _foundAttributes.Add(attrStmt);
                return new AstEmptyDecl(new AstIdExpr("attr"));
            }
            //else if (expr is AstFuncDecl funcDecl)
            //{
            //	// already prepared func (probably ctor or dtor)
            //	return funcDecl;
            //}
            // TODO: upper shite is probably not possible

            ReportMessage(PeekToken().Location, $"Unexpected token. Expected '=' or '\\n'");
            return new AstVarDecl(expr as AstIdExpr, null, null, docString, Location: expr);
        }
    }
}
