using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast;
using System.Xml.Linq;
using HapetFrontend.Errors;
using System.Runtime;
using HapetFrontend.Entities;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstDelegateDecl ParseDelegateDeclaration(ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            AstNestedExpr returnType = null;
            AstIdExpr delegateName = null;
            var generics = new List<AstIdExpr>();
            var beg = Consume(TokenType.KwDelegate, ErrMsg("keyword 'delegate'", "at beginning of delegate type")).Location;

            // return type
            if (!CheckToken(TokenType.Identifier))
            {
                // better error location
                ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.NoReturnTypeInDelegDecl));
            }
            else
            {
                returnType = ParseIdentifierExpression(inInfo, allowDots: true);
            }

            // class name
            if (!CheckToken(TokenType.Identifier))
            {
                // better error location
                ReportMessage(PeekToken().Location, [], ErrorCode.Get(CTEN.NoDelegNameAfterReturnType));
            }
            else
            {
                var nest = ParseIdentifierExpression(inInfo, allowDots: false);
                if (nest.RightPart is not AstIdExpr idExpr)
                {
                    ReportMessage(nest.Location, [], ErrorCode.Get(CTEN.DelegNameIsNotIdent));
                    return new AstDelegateDecl(new List<AstParamDecl>(), returnType, new AstIdExpr("unknown"), "", beg);
                }
                delegateName = idExpr;
            }

            // checking generics
            // getting generics from parsed class name
            if (delegateName is AstIdGenericExpr genExpr)
            {
                foreach (var g in genExpr.GenericRealTypes)
                {
                    if (g is AstNestedExpr nest)
                        generics.Add(nest.RightPart as AstIdExpr);
                    else if (g is AstIdExpr id)
                        generics.Add(id);
                    else
                    {
                        ReportMessage(g.Location, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                        generics.Add(null); // ERROR HERE
                    }
                }
            }

            TokenLocation end;
            var parameters = ParseParameterList(TokenType.OpenParen, TokenType.CloseParen, out var pbeg, out end, true);
            SkipNewlines();

            // parsing constrains
            var genericConstrains = ParseGenericConstrains(generics);

            // TODO: probably needed when allowing delegates for non-static funcs
            //// all delegates have ptr to a class object as their first param
            //parameters.Insert(0, new AstParamDecl(new AstNestedExpr(new AstPointerExpr(new AstIdExpr("byte")), null), new AstIdExpr("this")));

            // TODO: doc string
            return new AstDelegateDecl(parameters, returnType, delegateName, "", new Location(beg, end))
            {
                IsImported = inInfo.ExternalMetadata,
                HasGenericTypes = generics.Count > 0,
                GenericNames = generics,
                GenericConstrains = genericConstrains,
        };
        }
    }
}
