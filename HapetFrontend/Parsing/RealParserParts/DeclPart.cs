using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using System.Diagnostics;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private readonly List<AstAttributeStmt> _foundAttributes = new List<AstAttributeStmt>();

        private AstDeclaration ParseDeclaration(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            var docString = GetCurrentDocString();

            var saved1 = inInfo.AllowFunctionDeclaration;
            var saved4 = inInfo.AllowGeneric;
            var saved2 = inInfo.Message;
            var saved3 = inInfo.AllowPointerExpression;
            inInfo.AllowFunctionDeclaration = true;
            inInfo.AllowGeneric = true;
            inInfo.Message = null;
            inInfo.AllowPointerExpression = true;
            var expr = ParseExpression(inInfo, ref outInfo);
            inInfo.AllowFunctionDeclaration = saved1;
            inInfo.AllowGeneric = saved4;
            inInfo.Message = saved2;
            inInfo.AllowPointerExpression = saved3;

            if (expr is UnknownDecl udecl)
            {
                SkipNewlines();

                udecl.Documentation = docString;
                var result = PrepareUnknownDecl(udecl, _foundAttributes, inInfo, ref outInfo);
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
            // because of indexer overloading
            else if (expr is AstIndexerDecl indexerDecl)
            {
                indexerDecl.Attributes.AddRange(_foundAttributes);
                _foundAttributes.Clear();
                return indexerDecl;
            }

            ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.ExpectedEqualOrNewline));
            return new AstVarDecl(expr as AstIdExpr, null, null, docString, Location: expr);
        }
    }
}
