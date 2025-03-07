using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Extensions;
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
        private AstClassDecl CreateTypeDeclarationForGeneric(AstDeclaration parent, AstIdExpr name, List<AstNestedExpr> constrains)
        {
            // TODO: handle constains
            string additionalString = string.Empty;
            if (parent is AstFuncDecl funcDecl)
                additionalString = funcDecl.GenerateHashForGenericType(name.Name);

            var specialName = name.GetCopy($"{parent.Name.Name}_g_{name.Name}_{additionalString}");
            var cls = new AstClassDecl(specialName, new List<AstDeclaration>(), "", specialName)
            {
                IsGenericType = true,
            };
            cls.SpecialKeys.Add(TokenType.KwPrivate);
            cls.Attributes.Add(new AstAttributeStmt(new AstNestedExpr(new AstIdExpr("System.SuppressStaticCtorCallAttribute", name), null, name), [], name));

            PostPrepareClassScoping(cls);
            SetScopeAndParent(cls, parent, parent.SubScope);

            // we need to define it in global scope :)))
            _compiler.GlobalScope.DefineDeclSymbol(specialName.Name, cls);

            return cls;
        }

        private AstClassDecl GetRealTypeFromGeneric(AstClassDecl clsDecl, List<AstNestedExpr> genericTypes, string realName)
        {
            // we need to save previous info about current shite and then reload it 
            var savedSourceFile = _currentSourceFile;
            var savedClass = _currentClass;
            var savedFunction = _currentFunction;

            string origClassPureName = clsDecl.Name.Name.GetClassNameWithoutNamespace();

            var realCls = clsDecl.GetDeepCopy() as AstClassDecl;
            realCls.IsImplOfGeneric = true;
            realCls.Name = realCls.Name.GetCopy(realName);
            // no need to reset HasGenericTypes when using generic shite from another generic
            realCls.HasGenericTypes = HasGenericTypesInRealTypes(genericTypes);
            // replaces all T with normal types like int
            MakeGenericMapping(realCls.GenericNames, genericTypes);
            ReplaceAllGenericTypesInClass(realCls);
            // replaces all System.Anime::Func(Pivo) with just Func and etc.
            ResetDeclarationNames(realCls);
            // renaming generated funcs like ctor, dtor and etc.
            RenameFromGenericToRealType(realCls, origClassPureName);
            // just a pp
            PostPrepareClassScoping(realCls);
            // pp up to the current metadata step
            PostPrepareStatementUpToCurrentStep(realCls);

            // reload previously saved shite
            _currentSourceFile = savedSourceFile;
            _currentClass = savedClass;
            _currentFunction = savedFunction;

            return realCls;
        }

        private AstFuncDecl GetRealTypeFromGeneric(AstFuncDecl funcDecl, List<AstNestedExpr> genericTypes, string realName)
        {
            // we need to save previous info about current shite and then reload it 
            var savedSourceFile = _currentSourceFile;
            var savedClass = _currentClass;
            var savedFunction = _currentFunction;

            // cringe
            string origFuncPureName;
            if (funcDecl.Name.Name.Contains("::"))
                origFuncPureName = funcDecl.Name.Name.GetPureFuncName();
            else
                origFuncPureName = funcDecl.Name.Name;

            var realFunc = funcDecl.GetDeepCopy() as AstFuncDecl;
            realFunc.ContainingParent = funcDecl.ContainingParent;
            realFunc.IsImplOfGeneric = true;
            realFunc.Name = realFunc.Name.GetCopy(realName);
            // no need to reset HasGenericTypes when using generic shite from another generic
            realFunc.HasGenericTypes = HasGenericTypesInRealTypes(genericTypes);
            // replaces all T with normal types like int
            MakeGenericMapping(realFunc.GenericNames, genericTypes);
            ReplaceAllGenericTypesInFunction(realFunc);
            // replaces all System.Anime::Func(Pivo) with just Func and etc.
            ResetDeclarationNames(realFunc);
            // renaming generated funcs like ctor, dtor and etc.
            RenameFromGenericToRealType(funcDecl, origFuncPureName);
            // just a pp
            PostPrepareFunctionScoping(realFunc);
            // pp up to the current metadata step
            PostPrepareStatementUpToCurrentStep(realFunc);

            // reload previously saved shite
            _currentSourceFile = savedSourceFile;
            _currentClass = savedClass;
            _currentFunction = savedFunction;

            return realFunc;
        }

        private static bool HasGenericTypesInRealTypes(List<AstNestedExpr> genericTypes)
        {
            bool hasGeneric = false;
            foreach (var g in genericTypes)
            {
                if (g.LeftPart == null && g.RightPart is AstIdExpr id)
                {
                    var smb = id.Scope.GetSymbol(id.Name);
                    if (smb is DeclSymbol dS && dS.Decl is AstClassDecl clsD && clsD.IsGenericType)
                    {
                        hasGeneric = true;
                        break;
                    }
                }
            }
            return hasGeneric;
        }

        private string GetGenericRealName(string name, List<AstNestedExpr> generics)
        {
            InInfo tmpIn = InInfo.Default;
            OutInfo tmpout = OutInfo.Default;

            StringBuilder sb = new StringBuilder(name);
            sb.Append("_GB_");
            for (int i = 0; i < generics.Count; ++i)
            {
                var g = generics[i];
                // cringe :)
                PostPrepareExprInference(g, tmpIn, ref tmpout);
                if (g.RightPart is AstIdExpr idExpr)
                {
                    sb.Append(idExpr.FindSymbol.Name);
                }

                // if not last - append delimeter
                if (i != generics.Count - 1)
                    sb.Append("_GD_");
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
            else if (decl is AstFuncDecl funcDecl)
            {
                funcDecl.Name = funcDecl.Name.GetCopy(GetName(funcDecl));
            }

            static string GetName(AstDeclaration d)
            {
                if (d is AstClassDecl c)
                {
                    return c.Name.Name.GetClassNameWithoutNamespace();
                }
                else if (d is AstFuncDecl f)
                {
                    // check if it is really infered
                    if (f.Name.Name.Contains("::"))
                        return f.Name.Name.GetPureFuncName();
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

                    // we also need to rename _ini func call :)
                    if (d is AstFuncDecl fDecl && fDecl.Body != null && 
                        fDecl.Body.Statements[0] is AstCallExpr callExpr && callExpr.FuncName.Name == $"{origName}_ini")
                    {
                        /// make sure that this shite is the same as in <see cref="PostPrepareGenerateClassConstructor"/>
                        callExpr.FuncName = callExpr.FuncName.GetCopy($"{decl.Name.Name}_ini");
                    }
                }
                // if stor
                else if (d.Name.Name == $"{origName}_stor")
                {
                    d.Name = d.Name.GetCopy($"{decl.Name.Name}_stor");

                    // we also need to rename static var assign :)
                    if (d is AstFuncDecl fDecl && fDecl.Body != null &&
                        fDecl.Body.Statements[0] is AstIfStmt ifStmt && ifStmt.BodyTrue != null &&
                        ifStmt.BodyTrue.Statements.Count > 0 && ifStmt.BodyTrue.Statements[^1] is AstAssignStmt assignStmt &&
                        assignStmt.Target is AstNestedExpr nstE && nstE.RightPart is AstIdExpr idE && 
                        idE.Name == $"__is_{_currentSourceFile.Namespace}.{origName}_stor_called" &&
                        ifStmt.Condition is AstUnaryExpr unE && unE.SubExpr is AstIdExpr idECond &&
                        idECond.Name == $"__is_{_currentSourceFile.Namespace}.{origName}_stor_called")
                    {
                        /// make sure that this shite is the same as in <see cref="PostPrepareGenerateClassStaticConstructor"/>
                        nstE.RightPart = idE.GetCopy($"__is_{_currentSourceFile.Namespace}.{decl.Name.Name}_stor_called");
                        unE.SubExpr = idECond.GetCopy($"__is_{_currentSourceFile.Namespace}.{decl.Name.Name}_stor_called");
                    }
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
