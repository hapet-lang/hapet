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
using System.Text;
using System.Runtime;
using System.Xml.Linq;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareIdentifierInference(AstIdExpr idExpr, InInfo inInfo, ref OutInfo outInfo, Scope scopeToSearch = null)
        {
            string name = idExpr.Name;

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

            if (Step1_IdentifierFullNamespace(idExpr, inInfo, ref outInfo, scopeToSearch)) return;
            if (Step2_IdentifierLocalScope(idExpr, inInfo, ref outInfo, scopeToSearch)) return;
            if (Step3_IdentifierCurrNamespace(idExpr, inInfo, ref outInfo, scopeToSearch)) return;
            if (Step4_IdentifierUsings(idExpr, inInfo, ref outInfo, scopeToSearch)) return;
            if (Step5_IdentifierFuncs(idExpr, inInfo, ref outInfo, scopeToSearch)) return;

            if (!inInfo.MuteErrors)
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [], ErrorCode.Get(CTEN.TypeCouldNotBeInfered));
        }

        private bool Step1_IdentifierFullNamespace(AstIdExpr idExpr, InInfo inInfo, ref OutInfo outInfo, Scope scopeToSearch = null)
        {
            // skip if search in specific scope
            if (scopeToSearch != null)
                return false;

            string name = idExpr.Name;
            // check if it is smth like 'System.Attribute' where 'System' is ns and 'Attribute' is a class
            if (!string.IsNullOrWhiteSpace(name.GetNamespaceWithoutClassName()))
            {
                var leftPart = name.GetNamespaceWithoutClassName();
                var rightPart = name.GetClassNameWithoutNamespace();

                // search for the symbol in concrete namespace
                var smbl = idExpr.Scope.GetSymbolInNamespace(leftPart, rightPart);
                if (smbl is DeclSymbol typed)
                {
                    IdentifierOnFoundSymbol(idExpr, typed, string.Empty, inInfo, ref outInfo);
                    return true;
                }
            }
            return false;
        }

        private bool Step2_IdentifierLocalScope(AstIdExpr idExpr, InInfo inInfo, ref OutInfo outInfo, Scope scopeToSearch = null)
        {
            string name = idExpr.Name;
            var scope = scopeToSearch ?? idExpr.Scope;

            var smbl = scope.GetSymbol(name);
            if (smbl is DeclSymbol typed)
            {
                IdentifierOnFoundSymbol(idExpr, typed, string.Empty, inInfo, ref outInfo);
                return true;
            }

            // check for explicit shite
            if (idExpr.AdditionalData != null)
            {
                string typeName = (idExpr.AdditionalData.OutType as ClassType).Declaration.Name.Name;
                smbl = scope.GetSymbol($"{typeName}.{name}");
                if (smbl is DeclSymbol typed2)
                {
                    IdentifierOnFoundSymbol(idExpr, typed2, string.Empty, inInfo, ref outInfo);
                    return true;
                }
            }

            // skip if search in specific scope
            if (scopeToSearch != null)
                return false;

            // kostyl to handle 'base.Anime()' calls
            if (name == "base")
            {
                idExpr.OutType = PointerType.GetPointerType(_currentClass.InheritedFrom[0].OutType);
                var smbl2 = idExpr.Scope.GetSymbol("this");
                idExpr.FindSymbol = smbl2;
                return true;
            }

            return false;
        }

        private bool Step3_IdentifierCurrNamespace(AstIdExpr idExpr, InInfo inInfo, ref OutInfo outInfo, Scope scopeToSearch = null)
        {
            // skip if search in specific scope
            if (scopeToSearch != null)
                return false;

            string name = idExpr.Name;
            // searching for the name with namespace
            // works only for types/objects
            string nameWithNamespace = $"{idExpr.SourceFile.Namespace}.{name}";
            var smblInLocalFile = idExpr.Scope.GetSymbol(nameWithNamespace);
            if (smblInLocalFile is DeclSymbol typed3)
            {
                IdentifierOnFoundSymbol(idExpr, typed3, nameWithNamespace, inInfo, ref outInfo);
                return true;
            }
            return false;
        }

        private bool Step4_IdentifierUsings(AstIdExpr idExpr, InInfo inInfo, ref OutInfo outInfo, Scope scopeToSearch = null)
        {
            // skip if search in specific scope
            if (scopeToSearch != null)
                return false;

            string name = idExpr.Name;
            // go all over the usings
            foreach (var usng in idExpr.SourceFile.Usings)
            {
                // getting ns string
                var ns = usng.FlattenNamespace;

                // check if it is smth like 'Runtime.InteropServices.DllImportAttribute'
                // where 'Runtime.InteropServices' is PART! of ns and 'DllImportAttribute' is a class
                if (!string.IsNullOrWhiteSpace(name.GetNamespaceWithoutClassName()))
                {
                    var leftPart = name.GetNamespaceWithoutClassName();
                    var rightPart = name.GetClassNameWithoutNamespace();

                    // getting a symbol from namespace
                    var includedSmbl = idExpr.Scope.GetSymbolInNamespace($"{ns}.{leftPart}", rightPart);
                    if (includedSmbl is DeclSymbol typed4)
                    {
                        IdentifierOnFoundSymbol(idExpr, typed4, string.Empty, inInfo, ref outInfo);
                        return true;
                    }
                }

                // try just get the name from using namespace
                string fullNameWithNs = $"{ns}.{name}";
                var usedSmbl = idExpr.Scope.GetSymbolInNamespace(ns, name);
                if (usedSmbl is DeclSymbol typed5)
                {
                    IdentifierOnFoundSymbol(idExpr, typed5, fullNameWithNs, inInfo, ref outInfo);
                    return true;
                }
            }
            return false;
        }

        private bool Step5_IdentifierFuncs(AstIdExpr idExpr, InInfo inInfo, ref OutInfo outInfo, Scope scopeToSearch = null)
        {
            Scope scope = scopeToSearch ?? idExpr.Scope;
            string name = idExpr.Name;
            // searching for the name with current class name
            // works only for functions
            string nameWithClass = $"{_currentClass.Name.Name}::{name}";
            var smblInLocalClass = scope.GetSymbol(nameWithClass);
            if (smblInLocalClass is DeclSymbol typed2)
            {
                IdentifierOnFoundSymbol(idExpr, typed2, nameWithClass, inInfo, ref outInfo);
                return true;
            }

            // it is a func
            if (name.Contains("::"))
            {
                // for example 'System.Attribute::Attrbute_ctor(...)'
                string[] nameAndFunc = name.Split("::");
                if (nameAndFunc.Length != 2)
                {
                    // TODO: error 
                    return false;
                }

                // recursively infer left part of func call
                AstIdExpr leftPartId = idExpr.GetCopy(nameAndFunc[0]);
                PostPrepareIdentifierInference(leftPartId, inInfo, ref outInfo);

                // it has to be a class (or mb struct)
                string fullFuncName;
                ISymbol funcInAnotherClass;
                if (leftPartId.OutType is ClassType clsTp)
                {
                    fullFuncName = $"{clsTp}::{nameAndFunc[1]}";
                    scope = scopeToSearch ?? clsTp.Declaration.SubScope;
                    funcInAnotherClass = scope.GetSymbol(fullFuncName);
                }
                else if (leftPartId.OutType is StructType strTp)
                {
                    fullFuncName = $"{strTp}::{nameAndFunc[1]}";
                    scope = scopeToSearch ?? strTp.Declaration.SubScope;
                    funcInAnotherClass = scope.GetSymbol(fullFuncName);
                }
                else
                {
                    // TODO: error 
                    return false;
                }

                if (funcInAnotherClass is DeclSymbol typed4)
                {
                    IdentifierOnFoundSymbol(idExpr, typed4, fullFuncName, inInfo, ref outInfo);
                    return true;
                }
            }
            return false;
        }

        private void IdentifierOnFoundSymbol(AstIdExpr idExpr, DeclSymbol typed, string name, InInfo inInfo, ref OutInfo outInfo2)
        {
            if (!CheckIfCouldBeAccessed(idExpr, typed.Decl) && !inInfo.FromCallExpr && !inInfo.MuteErrors)
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [], ErrorCode.Get(CTEN.DeclCouldNotBeAccessed));
            typed = CheckForGenericType(typed, idExpr);
            if (!string.IsNullOrWhiteSpace(name))
                idExpr.Name = name;
            idExpr.OutType = typed.Decl.Type.OutType;
            TryAssignConstValueToExpr(idExpr, typed.Decl, inInfo, ref outInfo2);
            TrySaveClassUsage(typed.Decl);
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
                // TODO: possible circular access!!!
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
        private void TrySaveClassUsage(AstDeclaration decl)
        {
            if (decl is AstClassDecl clsDecl)
            {
                _allUsedClassesInProgram.Add(clsDecl);
            }
        }

        private DeclSymbol CheckForGenericType(DeclSymbol decl, AstIdExpr idExpr)
        {
            if (idExpr is not AstIdGenericExpr genId)
                return decl;

            if (!decl.Decl.HasGenericTypes)
                return decl;

            var theDecl = decl.Decl;

            // this is to get REAL PURE GENERIC. not the fcking T-like
            if (theDecl.IsImplOfGeneric)
            {
                theDecl = theDecl.OriginalGenericDecl;
            }

            // generating generic shite name
            string realName = GenericsHelper.GetRealFromGenericName(theDecl, genId.GenericRealTypes.GetNestedList());
            var realDcl = theDecl.Scope.GetSymbol(realName);
            if (realDcl is DeclSymbol realDclDecl)
            {
                // return if exists
                return realDclDecl;
            }

            // create a new shite with real types
            var realCls = GetRealTypeFromGeneric(theDecl, genId.GenericRealTypes.GetNestedList(), realName);

            // define the real decl in the same scope where generic one exists
            realDclDecl = new DeclSymbol(realName, realCls);
            theDecl.Scope.DefineSymbol(realDclDecl);
            return realDclDecl;
        }
    }
}
