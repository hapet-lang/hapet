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
using HapetFrontend.Helpers;

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
            string parentString = parent.Name.Name;
            if (parent is AstPropertyDecl propDecl)
                parentString = $"{parent.ContainingParent.Name.Name}.{parentString}";
            string additionalString = string.Empty;
            if (parent is AstFuncDecl funcDecl)
                additionalString = funcDecl.GenerateHashForGenericType(name.Name);

            string typeName = $"{parentString}_{parent.GenericNames.Count}_{GenericsHelper.GENERIC_TYPE_BEGIN}{name.Name}{GenericsHelper.GENERIC_TYPE_END}{additionalString}";
            var specialName = name.GetCopy(typeName);
            var cls = new AstClassDecl(specialName, new List<AstDeclaration>(), "", specialName)
            {
                IsGenericType = true,
            };
            cls.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwPrivate, name.Location.Beginning));
            cls.Attributes.Add(new AstAttributeStmt(new AstNestedExpr(new AstIdExpr("System.SuppressStaticCtorCallAttribute", name), null, name), [], name));

            SetScopeAndParent(cls, parent, _compiler.GlobalScope);
            PostPrepareClassScoping(cls);

            // we need to define it in global scope :)))
            _compiler.GlobalScope.DefineDeclSymbol(specialName.Name, cls);

            return cls;
        }

        private AstDeclaration GetRealTypeFromGeneric(AstDeclaration decl, List<AstNestedExpr> genericTypes, string realName)
        {
            // we need to save previous info about current shite and then reload it 
            var savedSourceFile = _currentSourceFile;
            var savedClass = _currentClass;
            var savedFunction = _currentFunction;

            // set the decl source file
            _currentSourceFile = decl.SourceFile;

            // cringe
            string origDeclPureName = decl.Name.Name;
            if (decl is AstFuncDecl funcDecl)
            {
                if (funcDecl.Name.Name.Contains("::"))
                    origDeclPureName = funcDecl.Name.Name.GetPureFuncName();
                else
                    origDeclPureName = funcDecl.Name.Name;
            }
            else if (decl is AstClassDecl || decl is AstStructDecl)
            {
                origDeclPureName = decl.Name.Name.GetClassNameWithoutNamespace();
            }

            var realDecl = decl.GetDeepCopy() as AstDeclaration;
            realDecl.ContainingParent = decl.ContainingParent;
            realDecl.IsImplOfGeneric = true;
            realDecl.OriginalGenericDecl = decl;
            realDecl.Name = realDecl.Name.GetCopy(realName).GetPureIdExpr();
            // no need to reset HasGenericTypes when using generic shite from another generic
            realDecl.HasGenericTypes = GenericsHelper.HasGenericTypesInRealTypes(genericTypes);

            // replaces all T with normal types like int
            MakeGenericMapping(realDecl.GenericNames, genericTypes);
            ReplaceAllGenericTypesInDecl(realDecl);
            // replaces all System.Anime::Func(Pivo) with just Func and etc.
            GenericsHelper.ResetDeclarationNames(realDecl);
            // renaming generated funcs like ctor, dtor and etc.
            RenameFromGenericToRealType(realDecl, origDeclPureName);
            // just a pp
            PostPrepareDeclScoping(realDecl);
            // pp up to the current metadata step
            PostPrepareStatementUpToCurrentStep(realDecl);

            // if it is a property - we need to create and inference its field/get/set
            if (realDecl is AstPropertyDecl propDecl)
            {
                List<AstDeclaration> parentDecls = new List<AstDeclaration>();
                if (propDecl.ContainingParent is AstClassDecl clsDecl)
                    parentDecls = clsDecl.Declarations;
                else if (propDecl.ContainingParent is AstStructDecl strDecl)
                    parentDecls = strDecl.Declarations;

                var newDecls = AddPropertyShiteToDecl(propDecl.ContainingParent, propDecl, true);
                foreach (var newD in newDecls)
                {
                    newD.IsImplOfGeneric = true;
                    newD.HasGenericTypes = realDecl.HasGenericTypes;
                    ReplaceAllGenericTypesInDecl(newD);
                    // replaces all System.Anime::Func(Pivo) with just Func and etc.
                    GenericsHelper.ResetDeclarationNames(newD);
                    // just a pp
                    SetScopeAndParent(newD, propDecl);
                    PostPrepareDeclScoping(newD);
                    // pp up to the current metadata step
                    PostPrepareStatementUpToCurrentStep(newD);
                }

                // we really need to add them :)  
                parentDecls.AddRange(newDecls);
                parentDecls.Add(realDecl);
            }

            // reload previously saved shite
            _currentSourceFile = savedSourceFile;
            _currentClass = savedClass;
            _currentFunction = savedFunction;

            return realDecl;
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

                    // cringe for metadata - because parsed in another way :)
                    if (d is AstFuncDecl fDecl2 && fDecl2.Body != null &&
                        fDecl2.Body.Statements[0] is AstNestedExpr nest && nest.RightPart is AstCallExpr callExpr2 && 
                        callExpr2.FuncName.Name == $"{origName}_ini")
                    {
                        /// make sure that this shite is the same as in <see cref="PostPrepareGenerateClassConstructor"/>
                        callExpr2.FuncName = callExpr2.FuncName.GetCopy($"{decl.Name.Name}_ini");
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
                        idE.Name == $"__is_{_currentSourceFile.Namespace.Replace('.', '_')}_{origName}_stor_called" &&
                        ifStmt.Condition is AstUnaryExpr unE && unE.SubExpr is AstIdExpr idECond &&
                        idECond.Name == $"__is_{_currentSourceFile.Namespace.Replace('.', '_')}_{origName}_stor_called")
                    {
                        /// make sure that this shite is the same as in <see cref="PostPrepareGenerateClassStaticConstructor"/>
                        nstE.RightPart = idE.GetCopy($"__is_{_currentSourceFile.Namespace.Replace('.', '_')}_{decl.Name.Name}_stor_called");
                        unE.SubExpr = idECond.GetCopy($"__is_{_currentSourceFile.Namespace.Replace('.', '_')}_{decl.Name.Name}_stor_called");
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
                else if (d.Name.Name == $"__is_{_currentSourceFile.Namespace.Replace('.', '_')}_{origName}_stor_called")
                {
                    d.Name = d.Name.GetCopy($"__is_{_currentSourceFile.Namespace.Replace('.', '_')}_{decl.Name.Name}_stor_called");
                }
            }
        }
    }
}
