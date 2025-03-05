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
        /// <summary>
        /// Creates a pseudo type to handle constrains of a generic type
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="name"></param>
        /// <param name="constrains"></param>
        /// <returns></returns>
        private void CreateTypeDeclarationForGeneric(AstDeclaration parent, AstIdExpr name, List<AstNestedExpr> constrains)
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
        }

        private AstClassDecl GetRealTypeFromGeneric(AstClassDecl clsDecl, List<AstNestedExpr> genericTypes, string realName)
        {
            string origClassPureName = clsDecl.Name.Name.Split('.')[^1];

            var realCls = clsDecl.GetDeepCopy() as AstClassDecl;
            realCls.Name = realCls.Name.GetCopy(realName);
            realCls.HasGenericTypes = false;
            // replaces all T with normal types like int
            ReplaceAllGenericTypesInClass(realCls, genericTypes);
            // replaces all System.Anime::Func(Pivo) with just Func and etc.
            ResetDeclarationNames(realCls);
            // renaming generated funcs like ctor, dtor and etc.
            RenameFromGenericToRealType(realCls, origClassPureName);
            // just a pp
            PostPrepareClassScoping(realCls);
            // pp up to the current metadata step
            PostPrepareStatementUpToCurrentStep(realCls);
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

        private static void ResetDeclarationNames(AstDeclaration decl)
        {
            if (decl is AstClassDecl clsDecl)
            {
                clsDecl.Name = clsDecl.Name.GetCopy(GetName(clsDecl));
                foreach (var dec in clsDecl.Declarations)
                {
                    dec.Name = dec.Name.GetCopy(GetName(dec));
                }
            }

            static string GetName(AstDeclaration d)
            {
                if (d is AstClassDecl c)
                {
                    var elements = c.Name.Name.Split(".");
                    return elements[elements.Length - 1];
                }
                else if (d is AstFuncDecl f)
                {
                    // check if it is really infered
                    if (f.Name.Name.Contains("::"))
                        return string.Concat(f.Name.Name.Split("::")[1].TakeWhile(x => x != '('));
                }
                return d.Name.Name;
            }
        }

        private void RenameFromGenericToRealType(AstDeclaration decl, string origName)
        {
            /// names of some funcs/vars has to be the same as in <see cref="PostPrepareClassMethodsInternal"/>

            // rename ctor, stor, ini, dtor and static var to handle stor ini
            List<AstDeclaration> decls = new List<AstDeclaration>();
            if (decl is AstClassDecl clsDecl)
            {
                decls = clsDecl.Declarations;
            }
            else if (decl is AstStructDecl strDecl)
            {
                decls = strDecl.Declarations;
            }

            foreach (var d in decls)
            {
                // if ctor
                if (d.Name.Name == $"{origName}_ctor")
                {
                    d.Name = d.Name.GetCopy($"{decl.Name.Name}_ctor");
                }
                // if stor
                else if (d.Name.Name == $"{origName}_stor")
                {
                    d.Name = d.Name.GetCopy($"{decl.Name.Name}_stor");
                }
                // if ini
                else if (d.Name.Name == $"{origName}_ini")
                {
                    d.Name = d.Name.GetCopy($"{decl.Name.Name}_ini");
                }
                // if dtor
                else if (d.Name.Name == $"{origName}_dtor")
                {
                    d.Name = d.Name.GetCopy($"{decl.Name.Name}_dtor");
                }
                // if static ctor var handler
                else if (d.Name.Name == $"__is_{_currentSourceFile.Namespace}.{origName}_stor_called")
                {
                    d.Name = d.Name.GetCopy($"__is_{_currentSourceFile.Namespace}.{decl.Name.Name}_stor_called");
                }
            }
        }
    }
}
