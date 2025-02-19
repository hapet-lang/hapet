using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Errors;
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
            // because of implicit/explicit kws
            else if (expr is AstOverloadDecl overDecl)
            {
                overDecl.Attributes.AddRange(_foundAttributes);
                _foundAttributes.Clear();
                return overDecl;
            }

            ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.ExpectedEqualOrNewline));
            return new AstVarDecl(expr as AstIdExpr, null, null, docString, Location: expr);
        }
    }
}
