using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
using HapetPostPrepare.Entities;
using System;
using System.Text;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private AstDeclaration GetTypeDeclarationForGeneric(AstDeclaration parent, AstIdExpr name, List<AstNestedExpr> constrains)
        {
            // TODO: handle constains
            var cls = new AstClassDecl(name, new List<AstDeclaration>(), "", name)
            {
                IsGenericType = true,
            };
            cls.SpecialKeys.Add(TokenType.KwPrivate);
            cls.Attributes.Add(new AstAttributeStmt(new AstNestedExpr(new AstIdExpr("System.SuppressStaticCtorCallAttribute", name), null, name), [], name));

            PostPrepareClassScoping(cls);
            SetScopeAndParent(cls, parent, parent.SubScope);
            parent.SubScope.DefineDeclSymbol(name.Name, cls);
            return cls;
        }

        private AstClassDecl GetRealTypeFromGeneric(AstClassDecl clsDecl, List<AstNestedExpr> genericTypes, string realName)
        {
            var realCls = clsDecl.GetDeepCopy() as AstClassDecl;
            realCls.Name = realCls.Name.GetCopy(realName);
            ReplaceAllGenericTypesInClass(realCls, genericTypes);
            // TODO: inference this shite
            return realCls;
        }

        private string GetGenericRealName(string name, List<AstNestedExpr> generics)
        {
            InInfo tmpIn = InInfo.Default;
            OutInfo tmpout = OutInfo.Default;

            StringBuilder sb = new StringBuilder(name);
            sb.Append("_GB_");
            foreach (var g in generics)
            {
                // cringe :)
                PostPrepareExprInference(g, tmpIn, ref tmpout);
                if (g.RightPart is AstIdExpr idExpr)
                {
                    sb.Append(idExpr.FindSymbol.Name);
                }
            }
            sb.Append("_GE_");
            return sb.ToString();
        }
    }
}
