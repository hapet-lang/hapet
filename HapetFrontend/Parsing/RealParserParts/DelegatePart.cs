using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast;
using System.Xml.Linq;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstDelegateDecl ParseDelegateDeclaration()
        {
            AstNestedExpr returnType = null;
            AstIdExpr delegateName = null;
            var beg = Consume(TokenType.KwDelegate, ErrMsg("keyword 'delegate'", "at beginning of delegate type")).Location;

            // return type
            if (!CheckToken(TokenType.Identifier))
            {
                // better error location
                ReportMessage(PeekToken().Location, $"Expected return type after 'delegate' keyword");
            }
            else
            {
                returnType = ParseIdentifierExpression(allowDots: true);
            }

            // class name
            if (!CheckToken(TokenType.Identifier))
            {
                // better error location
                ReportMessage(PeekToken().Location, $"Expected delegate name after delegate return type");
            }
            else
            {
                var nest = ParseIdentifierExpression(allowDots: false);
                if (nest.RightPart is not AstIdExpr idExpr)
                {
                    ReportMessage(nest.Location, $"Delegate name expected to be an identifier");
                    return new AstDelegateDecl(new List<AstParamDecl>(), returnType, new AstIdExpr("unknown"), "", beg);
                }
                delegateName = idExpr;
            }

            var parameters = ParseParameterList(TokenType.OpenParen, TokenType.CloseParen, out var pbeg, out var pend, true);
            var end = Consume(TokenType.Semicolon, ErrMsg("token ';'", "at the end of delegate declaration")).Location;

            // TODO: probably needed when allowing delegates for non-static funcs
            //// all delegates have ptr to a class object as their first param
            //parameters.Insert(0, new AstParamDecl(new AstNestedExpr(new AstPointerExpr(new AstIdExpr("byte")), null), new AstIdExpr("this")));

            // TODO: doc string
            return new AstDelegateDecl(parameters, returnType, delegateName, Location: new Location(beg, end));
        }
    }
}
