using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;
using System;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareIdentifierInference(AstIdExpr idExpr, InInfo inInfo, ref OutInfo outInfo, AstDeclaration declToSearch = null)
        {
            string name = idExpr.Name;

            // at first - check that the id could be a generic type
            var genericEntity = _currentParentStack.CurrentGenericIdMappings.FirstOrDefault(x => x.Value.Item1 == name);
            if (genericEntity.HasValue)
            {
                idExpr.OutType = genericEntity.Value.Item2;
                return;
            }

            // infer generic names
            if (idExpr is AstIdGenericExpr genId)
            {
                for (int i = 0; i < genId.GenericRealTypes.Count; ++i)
                {
                    var g = genId.GenericRealTypes[i];
                    PostPrepareExprInference(g, inInfo, ref outInfo);
                }
            }

            // infer explicit shite
            if (idExpr.AdditionalData != null)
            {
                PostPrepareExprInference(idExpr.AdditionalData, inInfo, ref outInfo);
            }

            if (Step1_IdentifierFullNamespace(idExpr, inInfo, ref outInfo, declToSearch)) return;
            if (Step2_IdentifierLocalScope(idExpr, inInfo, ref outInfo, declToSearch)) return;
            if (Step3_IdentifierCurrNamespace(idExpr, inInfo, ref outInfo, declToSearch)) return;
            if (Step4_IdentifierUsings(idExpr, inInfo, ref outInfo, declToSearch)) return;
            if (Step5_IdentifierFuncs(idExpr, inInfo, ref outInfo, declToSearch)) return;

            if (!inInfo.MuteErrors && !inInfo.FromCallExpr)
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [], ErrorCode.Get(CTEN.TypeCouldNotBeInfered));
        }

        private bool Step1_IdentifierFullNamespace(AstIdExpr idExpr, InInfo inInfo, ref OutInfo outInfo, AstDeclaration scopeToSearch = null)
        {
            // skip if search in specific scope
            if (scopeToSearch != null)
                return false;

            string name = idExpr.Name;
            string ns = name.GetNamespaceWithoutClassName();
            // check if it is smth like 'System.Attribute' where 'System' is ns and 'Attribute' is a class
            if (!string.IsNullOrWhiteSpace(ns))
            {
                // the only type name
                var rightPart = idExpr.GetCopy(name.GetClassNameWithoutNamespace());

                // search for the symbol in concrete namespace
                var smbl = idExpr.Scope.GetSymbolInNamespace(ns, rightPart, handleGenerics: true);
                if (smbl is DeclSymbol typed)
                {
                    IdentifierOnFoundSymbol(idExpr, typed, string.Empty, inInfo, ref outInfo);
                    return true;
                }
            }
            return false;
        }

        private bool Step2_IdentifierLocalScope(AstIdExpr idExpr, InInfo inInfo, ref OutInfo outInfo, AstDeclaration declToSearch = null)
        {
            string name = idExpr.Name;

            // at first try to search in decl subscopes
            if (declToSearch != null)
            {
                var resultCurrent = SubSearcher(declToSearch.SubScope, ref outInfo);
                if (resultCurrent) return true; // handle good result

                // go all over the inherited types
                foreach (var inh in declToSearch.GetInheritedTypes())
                {
                    // try to search in them
                    var result = Step2_IdentifierLocalScope(idExpr, inInfo, ref outInfo, (inh.OutType as ClassType).Declaration);
                    if (result) return true; // handle good result
                }
            }

            // skip if search in specific scope
            if (declToSearch != null)
                return false;

            // then try id's scope
            var res = SubSearcher(idExpr.Scope, ref outInfo);
            if (res) return true; // handle good result

            // kostyl to handle 'base.Anime()' calls
            if (name == "base")
            {
                // getting the current's class inherited shite
                List<AstNestedExpr> inherited;
                var currentParent = _currentParentStack.GetNearestParentClassOrStruct();
                if (currentParent is AstClassDecl clsDeclCurr)
                    inherited = clsDeclCurr.InheritedFrom;
                else
                    inherited = (currentParent as AstStructDecl).InheritedFrom;

                idExpr.OutType = inherited[0].OutType;
                var smbl2 = idExpr.Scope.GetSymbol(idExpr.GetCopy("this"));
                idExpr.FindSymbol = smbl2;
                return true;
            }

            return false;

            bool SubSearcher(Scope scope, ref OutInfo outInfo) 
            {
                var smbl = scope.GetSymbol(idExpr, handleGenerics: true);
                if (smbl is DeclSymbol typed)
                {
                    IdentifierOnFoundSymbol(idExpr, typed, string.Empty, inInfo, ref outInfo);
                    return true;
                }

                // check for explicit shite
                if (idExpr.AdditionalData != null)
                {
                    string typeName = (idExpr.AdditionalData.OutType as ClassType).Declaration.Name.Name;
                    smbl = scope.GetSymbol(idExpr.GetCopy($"{typeName}.{name}"), handleGenerics: true);
                    if (smbl is DeclSymbol typed2)
                    {
                        IdentifierOnFoundSymbol(idExpr, typed2, string.Empty, inInfo, ref outInfo);
                        return true;
                    }
                }
                return false;
            }
        }

        private bool Step3_IdentifierCurrNamespace(AstIdExpr idExpr, InInfo inInfo, ref OutInfo outInfo, AstDeclaration declToSearch = null)
        {
            // skip if search in specific scope
            if (declToSearch != null)
                return false;

            string name = idExpr.Name;
            // searching for the name with namespace
            // works only for types/objects
            string nameWithNamespace = $"{idExpr.SourceFile.Namespace}.{name}";
            var smblInLocalFile = idExpr.Scope.GetSymbol(idExpr.GetCopy(nameWithNamespace), handleGenerics: true);
            if (smblInLocalFile is DeclSymbol typed3)
            {
                IdentifierOnFoundSymbol(idExpr, typed3, nameWithNamespace, inInfo, ref outInfo);
                return true;
            }
            return false;
        }

        private bool Step4_IdentifierUsings(AstIdExpr idExpr, InInfo inInfo, ref OutInfo outInfo, AstDeclaration declToSearch = null)
        {
            // skip if search in specific scope
            if (declToSearch != null)
                return false;

            string name = idExpr.Name;
            // go all over the usings
            foreach (var usng in idExpr.SourceFile.Usings)
            {
                // getting ns string
                var ns = usng.FlattenNamespace;

                // try just get the name from using namespace
                string fullNameWithNs = $"{ns}.{name}";
                var usedSmbl = idExpr.Scope.GetSymbolInNamespace(ns, idExpr, handleGenerics: true);
                if (usedSmbl is DeclSymbol typed5)
                {
                    IdentifierOnFoundSymbol(idExpr, typed5, typed5.Name.Name, inInfo, ref outInfo);
                    return true;
                }

                string currNs = name.GetNamespaceWithoutClassName();
                // check if it is smth like 'Runtime.InteropServices.DllImportAttribute'
                // where 'Runtime.InteropServices' is PART! of ns and 'DllImportAttribute' is a class
                if (!string.IsNullOrWhiteSpace(currNs))
                {
                    // getting a symbol from namespace
                    var includedSmbl = idExpr.Scope.GetSymbolInNamespace($"{ns}.{currNs}", idExpr, handleGenerics: true);
                    if (includedSmbl is DeclSymbol typed4)
                    {
                        IdentifierOnFoundSymbol(idExpr, typed4, typed4.Name.Name, inInfo, ref outInfo);
                        return true;
                    }
                }                
            }
            return false;
        }

        private bool Step5_IdentifierFuncs(AstIdExpr idExpr, InInfo inInfo, ref OutInfo outInfo, AstDeclaration declToSearch = null)
        {
            string name = idExpr.Name;
            // at first try to search in decl subscopes
            if (declToSearch != null)
            {
                var resultCurrent = SubSearcher(declToSearch.SubScope, ref outInfo);
                if (resultCurrent) return true; // handle good result

                // go all over the inherited types
                foreach (var inh in declToSearch.GetInheritedTypes())
                {
                    // try to search in them
                    var result = Step5_IdentifierFuncs(idExpr, inInfo, ref outInfo, (inh.OutType as ClassType).Declaration);
                    if (result) return true; // handle good result
                }
            }

            // skip if search in specific scope
            if (declToSearch != null)
                return false;

            // then try id's scope
            var res = SubSearcher(idExpr.Scope, ref outInfo);
            if (res) return true; // handle good result

            return false;

            bool SubSearcher(Scope scope, ref OutInfo outInfo)
            {
                // searching for the name with current class name
                // works only for functions
                var currentParent = _currentParentStack.GetNearestParentClassOrStruct();
                string nameWithClass = $"{currentParent.Name.Name}::{name}";
                var smblInLocalClass = scope.GetSymbol(idExpr.GetCopy(nameWithClass), handleGenerics: true);
                if (smblInLocalClass is DeclSymbol typed2)
                {
                    IdentifierOnFoundSymbol(idExpr, typed2, typed2.Name.Name, inInfo, ref outInfo);
                    return true;
                }

                // it is a func
                if (name.Contains("::"))
                {
                    // for example 'System.Attribute::Attrbute_ctor(...)'
                    string[] nameAndFunc = name.Split("::");
                    if (nameAndFunc.Length != 2)
                    {
                        // error 
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [name], ErrorCode.Get(CTEN.OnlyOneDoubleColonInFunc));
                        return false;
                    }

                    // recursively infer left part of func call
                    // WARN: need to get PURE id expr - because
                    // idExpr could be AstIdGenericExpr and then everything is fucked up
                    AstIdExpr leftPartId = idExpr.GetPureIdExpr().GetCopy(nameAndFunc[0]);
                    PostPrepareIdentifierInference(leftPartId, inInfo, ref outInfo);

                    // it has to be a class (or mb struct)
                    string fullFuncName;
                    ISymbol funcInAnotherClass;
                    if (leftPartId.OutType is ClassType clsTp)
                    {
                        fullFuncName = $"{clsTp}::{nameAndFunc[1]}";
                        scope = declToSearch?.SubScope ?? clsTp.Declaration.SubScope;
                        funcInAnotherClass = scope.GetSymbol(idExpr.GetCopy(fullFuncName));
                    }
                    else if (leftPartId.OutType is StructType strTp)
                    {
                        fullFuncName = $"{strTp}::{nameAndFunc[1]}";
                        scope = declToSearch?.SubScope ?? strTp.Declaration.SubScope;
                        funcInAnotherClass = scope.GetSymbol(idExpr.GetCopy(fullFuncName));
                    }
                    else if (leftPartId.OutType is GenericType genTp)
                    {
                        fullFuncName = $"{genTp}::{nameAndFunc[1]}";
                        scope = declToSearch?.SubScope ?? genTp.Declaration.SubScope;
                        funcInAnotherClass = scope.GetSymbol(idExpr.GetCopy(fullFuncName));
                    }
                    else
                    {
                        // error 
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, leftPartId,
                            [HapetType.AsString(leftPartId.OutType)],
                            ErrorCode.Get(CTEN.FuncCallNotOnType));
                        return false;
                    }

                    if (funcInAnotherClass is DeclSymbol typed4)
                    {
                        IdentifierOnFoundSymbol(idExpr, typed4, typed4.Name.Name, inInfo, ref outInfo);
                        return true;
                    }
                }
                return false;
            }
        }

        private void IdentifierOnFoundSymbol(AstIdExpr idExpr, DeclSymbol typed, string name, InInfo inInfo, ref OutInfo outInfo2)
        {
            if (!CheckIfCouldBeAccessed(idExpr, typed.Decl) && !inInfo.FromCallExpr && !inInfo.MuteErrors)
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [], ErrorCode.Get(CTEN.DeclCouldNotBeAccessed));
            typed = CheckForGenericType(typed, idExpr);
            if (!string.IsNullOrWhiteSpace(name))
            {
                idExpr.Name = name;
            }
            idExpr.OutType = typed.Decl.Type.OutType;

            HandleBasicTypes(typed.Decl, idExpr);
            TryAssignConstValueToExpr(idExpr, typed.Decl, inInfo, ref outInfo2);
            TrySaveClassAndStructUsage(typed.Decl);
            idExpr.FindSymbol = typed;
        }

        /// <summary>
        /// This shite helps us to move OutValue from one to another
        /// </summary>
        /// <param name="expr">The main expr</param>
        /// <param name="decl">The decl that could have OutValue</param>
        private void TryAssignConstValueToExpr(AstExpression expr, AstDeclaration decl, InInfo inInfo, ref OutInfo outInfo)
        {
            // assign out value only from consts
            if (decl is AstVarDecl varDecl && varDecl.SpecialKeys.Contains(TokenType.KwConst))
            {
                // skip this shite - inferer will error it somewhere
                if (varDecl.Initializer == null)
                    return;

                // check that the initializer is not yet infered - infer it
                if (varDecl.Initializer.OutType == null)
                {
                    PostPrepareExprInference(varDecl.Initializer, inInfo, ref outInfo);
                }
                expr.OutValue = varDecl.Initializer.OutValue;
            }
        }

        /// <summary>
        /// Saves class usage to know which were used by the program. 
        /// This would be used to call static ctors :)
        /// </summary>
        /// <param name="decl">The decl to check and mark</param>
        private void TrySaveClassAndStructUsage(AstDeclaration decl)
        {
            if (decl is AstClassDecl || decl is AstStructDecl)
            {
                _allUsedClassesAndStructsInProgram.Add(decl);
            }
        }

        private DeclSymbol CheckForGenericType(DeclSymbol decl, AstIdExpr idExpr)
        {
            if (idExpr is not AstIdGenericExpr genId)
                return decl;

            var theDecl = decl.Decl;
            var realDecl = CreateRealTypeFromGeneric(theDecl, genId, out var realName, true);

            DeclSymbol realDclDecl;
            // func is defined by itself
            if (theDecl is not AstFuncDecl)
            {
                // define the real decl in the same scope where generic one exists
                realDclDecl = new DeclSymbol(realName, realDecl);
                theDecl.Scope.DefineSymbol(realDclDecl);
            }
            else
            {
                realDclDecl = theDecl.Scope.GetSymbol(realDecl.Name) as DeclSymbol;
            }
            return realDclDecl;
        }

        public AstDeclaration CreateRealTypeFromGeneric(AstDeclaration genDecl, AstIdGenericExpr realId, out AstIdGenericExpr realName, bool allowRealGeneric = false)
        {
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            if (!genDecl.HasGenericTypes)
            {
                realName = genDecl.Name as AstIdGenericExpr;
                return genDecl;
            }

            // return if it is already an impl
            if (genDecl.IsImplOfGeneric)
            {
                realName = genDecl.Name as AstIdGenericExpr;
                return genDecl;
            }

            // infer generic names
            for (int i = 0; i < realId.GenericRealTypes.Count; ++i)
            {
                var g = realId.GenericRealTypes[i];
                PostPrepareExprInference(g, inInfo, ref outInfo);
            }

            // if instantiating with genericTypes are not allowed - 
            // check that the types are not generic
            if (!allowRealGeneric)
            {
                // no need to create new decls with non-real-generics
                if (HasAnyGenericTypes(realId.GenericRealTypes))
                {
                    // TODO: check for compatibility (? tf i mean by it)
                    realName = genDecl.Name as AstIdGenericExpr;
                    return genDecl;
                }
            }

            // generating generic shite name
            realName = realId.GetCopy(genDecl.Name.Name);
            var realDcl = genDecl.Scope.GetSymbol(realName);
            if (realDcl is DeclSymbol realDclDecl)
            {
                // return if exists
                return realDclDecl.Decl;
            }

            // create a new shite with real types
            var realDecl = GetRealTypeFromGeneric(genDecl, realId.GenericRealTypes.GetNestedList(_compiler.MessageHandler), 
                realName, HasAnyGenericTypes(realId.GenericRealTypes));
            return realDecl;
        }
    }
}
