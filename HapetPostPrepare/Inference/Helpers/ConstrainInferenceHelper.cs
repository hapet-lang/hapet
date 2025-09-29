using System.Data;
using System.Runtime;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private readonly Dictionary<AstGenericDecl, List<AstConstrainStmt>> _allGenericTypes = new Dictionary<AstGenericDecl, List<AstConstrainStmt>>();

        /// <summary>
        /// Creates or returns existing genericTyped declaration
        /// </summary>
        /// <returns></returns>
        internal AstGenericDecl GetGenericDeclaration(List<AstConstrainStmt> constrains, AstIdExpr currGeneric, 
            AstDeclaration containingParent, InInfo inInfo, ref OutInfo outInfo)
        {
            PrepareNonGenericConstrains(constrains, inInfo, ref outInfo);
            PrepareGenericConstrains(constrains, inInfo, ref outInfo);

            // need to search for the same constrain
            foreach (var k in _allGenericTypes.Keys)
            {
                // skip not the same names
                //if (k.Name.Name != currGeneric.Name)
                //    continue;

                List<bool> collisions = new List<bool>();
                var existingConstrains = _allGenericTypes[k];
                foreach (var exC in existingConstrains)
                {
                    // if it is a user custom type - need to check that 
                    // expr types are the same
                    if (exC.ConstrainType == GenericConstrainType.CustomType)
                    {
                        bool exists = false;
                        // search for the same type in current constrains
                        foreach (var c in constrains)
                        {
                            if (c.ConstrainType == GenericConstrainType.CustomType)
                            {
                                exists = exC.Expr.OutType == c.Expr.OutType;
                                if (exists)
                                    break;
                            }
                        }
                        collisions.Add(exists);
                    }
                    else if (exC.ConstrainType == GenericConstrainType.NewType)
                    {
                        var cNew = constrains.FirstOrDefault(x => x.ConstrainType == GenericConstrainType.NewType);
                        bool allTheSame = cNew != null && exC.AdditionalExprs.Count == cNew.AdditionalExprs.Count;
                        if (allTheSame)
                            for (int i = 0; i < exC.AdditionalExprs.Count; ++i)
                            {
                                if (exC.AdditionalExprs[i].OutType != cNew.AdditionalExprs[i].OutType)
                                {
                                    allTheSame = false;
                                    break;
                                }
                            }
                        collisions.Add(allTheSame);
                    }
                    else
                    {
                        collisions.Add(constrains.Any(x => x.ConstrainType == exC.ConstrainType));
                    }
                }

                // if all true and amount of constrains are the same - it is the same generic type
                if (collisions.All(x => x) && collisions.Count == constrains.Count && constrains.Count == existingConstrains.Count)
                    return k; // just return the constrain
            }

            AddObjectClassConstainIfNeeded(constrains, containingParent, inInfo, ref outInfo, true);
            // check that constrains are ok
            CheckIfConstrainsAreOk(constrains);

            // here - if not found the same in existing
            var genericDecl = CreateGenericDeclaration(constrains, currGeneric, containingParent);

            // post prepare
            PostPrepareGenericDeclConstrains(genericDecl, inInfo, ref outInfo);
            // add it to preparation pipe
            AllGenericsMetadata.Add(genericDecl);

            // cache it
            _allGenericTypes.Add(genericDecl, constrains);

            return genericDecl;
        }

        private void PrepareNonGenericConstrains(List<AstConstrainStmt> constrains, InInfo inInfo, ref OutInfo outInfo)
        {
            // handle constrains
            foreach (var constrain in constrains)
            {
                // just inferencing 
                if (constrain.ConstrainType == GenericConstrainType.CustomType && constrain.Expr.UnrollToRightPart<AstIdExpr>() is not AstIdGenericExpr)
                    PostPrepareExprInference(constrain.Expr, inInfo, ref outInfo);
                else if (constrain.ConstrainType == GenericConstrainType.NewType)
                    foreach (var t in constrain.AdditionalExprs)
                        PostPrepareExprInference(t, inInfo, ref outInfo);
            }
        }

        private void PrepareGenericConstrains(List<AstConstrainStmt> constrains, InInfo inInfo, ref OutInfo outInfo)
        {
            // handle constrains
            foreach (var constrain in constrains)
            {
                // just inferencing 
                if (constrain.ConstrainType == GenericConstrainType.CustomType && constrain.Expr.UnrollToRightPart<AstIdExpr>() is AstIdGenericExpr)
                    PostPrepareExprInference(constrain.Expr, inInfo, ref outInfo);
            }
        }

        private void AddObjectClassConstainIfNeeded(List<AstConstrainStmt> constrains, AstDeclaration containingParent, InInfo inInfo, ref OutInfo outInfo, bool infer = false)
        {
            // manually adding the constrain of System.Object if required
            if (constrains.FirstOrDefault(x => x.ConstrainType == GenericConstrainType.CustomType &&
                x.Expr.OutType is ClassType clsTT &&
                clsTT.Declaration.NameWithNs == "System.Object") == null)
            {
                var specialC = new AstConstrainStmt(
                new AstNestedExpr(new AstIdExpr("System.Object", containingParent.Location), null, containingParent.Location),
                GenericConstrainType.CustomType,
                containingParent.Location)
                {
                    AdditionalExprs = new List<AstNestedExpr>(),
                };
                constrains.Insert(0, specialC);
                SetScopeAndParent(specialC, containingParent);
                PostPrepareConstrainScoping(specialC);
                if (infer)
                    PostPrepareExprInference(specialC.Expr, inInfo, ref outInfo);
            }
        }

        private AstGenericDecl CreateGenericDeclaration(List<AstConstrainStmt> constrains, AstIdExpr currGeneric, AstDeclaration containingParent)
        {
            // creating the declaration
            var genericDecl = new AstGenericDecl(currGeneric, location: currGeneric.Location)
            {
                Constrains = constrains,
                ParentDecl = containingParent.IsImplOfGeneric ? containingParent.OriginalGenericDecl : containingParent,
                IsNestedDecl = true, // for what? but let it be :)
                SubScope = new Scope($"{currGeneric.Name}_scope", containingParent.Scope) { GlobalScope = _compiler.GlobalScope },
                SourceFile = containingParent.SourceFile,
            };
            return genericDecl;
        }

        internal void PostPrepareGenericDeclConstrains(AstGenericDecl decl, InInfo inInfo, ref OutInfo outInfo)
        {
            List<AstNestedExpr> inheritedFrom = new List<AstNestedExpr>();

            // handle constrains
            foreach (var constrain in decl.Constrains)
            {
                switch (constrain.ConstrainType)
                {
                    case GenericConstrainType.CustomType:
                        HandleCustomConstrainType(decl, constrain, inInfo, ref outInfo);
                        inheritedFrom.Add(constrain.Expr);
                        break;
                    // ...
                }
            }
            decl.InheritedFrom = inheritedFrom;

            // single post prepare
            PostPrepareStatementUpToCurrentStep(decl);
        }

        private void HandleCustomConstrainType(AstGenericDecl decl, AstConstrainStmt constrain, InInfo inInfo, ref OutInfo outInfo)
        {
            // inference the ident
            PostPrepareExprInference(constrain.Expr, inInfo, ref outInfo);
            // custom constains are always interface/class
            var theClass = (constrain.Expr.OutType as ClassType).Declaration;

            // we need to copy all the class decls to our genericDecl
            foreach (var d in theClass.Declarations)
            {
                // do not copy ini/ctor/stor/dtor funcs
                if (d is AstFuncDecl funcDecl && (
                    funcDecl.ClassFunctionType == ClassFunctionType.Initializer ||
                    funcDecl.ClassFunctionType == ClassFunctionType.Ctor ||
                    funcDecl.ClassFunctionType == ClassFunctionType.StaticCtor ||
                    funcDecl.ClassFunctionType == ClassFunctionType.Dtor))
                    continue;

                // create a copy of the decl 
                var copied = d.GetOnlyDeclareCopy();

                // change the parent
                copied.ContainingParent = decl;
                // reset name
                if (copied is AstFuncDecl func1)
                    func1.Name = func1.Name.GetCopy();

                // we should make all the decls abstract
                // do not use SpecialKeysHelper because we need to keep 'static abstract' funcs
                if (!copied.SpecialKeys.Contains(TokenType.KwAbstract))
                    copied.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwAbstract, copied.Location.Beginning));

                // we need to change first param in non static funcs
                if (copied is AstFuncDecl func && !func.SpecialKeys.Contains(TokenType.KwStatic))
                    ReplaceFirstParamOnNonStaticFunc(func, decl);

                // add the copy
                decl.Declarations.Add(copied);
            }
        }

        private void ReplaceFirstParamOnNonStaticFunc(AstFuncDecl func, AstGenericDecl decl)
        {
            /// almost the same as in <see cref="FuncPrepareAfterAll"/>
            var thisParamType = decl.Name.GetCopy();
            // creating the class instance 'this' param
            AstExpression paramType = new AstPointerExpr(thisParamType, false);
            AstIdExpr paramName = new AstIdExpr("this");
            AstParamDecl thisParam = new AstParamDecl(new AstNestedExpr(paramType, null), paramName);
            // replacing
            func.Parameters[0] = thisParam;
        }

        private bool CheckIfConstrainsAreOk(List<AstConstrainStmt> constrains) 
        {
            var allChecks = new List<bool>();
            // 1 - check for duplicates
            List<AstConstrainStmt> bucket = new List<AstConstrainStmt>();
            foreach (var c in constrains)
            {
                bool found = false;
                if (c.ConstrainType == GenericConstrainType.CustomType)
                {
                    found = bucket.FirstOrDefault(x => x.ConstrainType == c.ConstrainType && x.Expr.OutType == c.Expr.OutType) != null;
                }
                else
                {
                    found = bucket.FirstOrDefault(x => x.ConstrainType == c.ConstrainType) != null;
                }
                allChecks.Add(!found);
                // if found duplicate
                if (found)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, c,
                        [], ErrorCode.Get(CTEN.DuplicateConstrain));
                }
                bucket.Add(c);
            }

            // 2 - check for only class/struct/enum/delegate
            var strC = constrains.FirstOrDefault(x => x.ConstrainType == GenericConstrainType.StructType);
            var clsC = constrains.FirstOrDefault(x => x.ConstrainType == GenericConstrainType.ClassType);
            var enmC = constrains.FirstOrDefault(x => x.ConstrainType == GenericConstrainType.EnumType);
            var delC = constrains.FirstOrDefault(x => x.ConstrainType == GenericConstrainType.DelegateType);

            var notNulls = (new AstConstrainStmt[] { strC, clsC, enmC, delC }).Where(x => x != null).ToArray();
            if (notNulls.Length > 1)
            {
                foreach (var c in notNulls)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, c,
                        [], ErrorCode.Get(CTEN.OnlyOneOnTheConstrains));
                }
                allChecks.Add(false);
            }

            return allChecks.All(x => x);
        }
    }
}
