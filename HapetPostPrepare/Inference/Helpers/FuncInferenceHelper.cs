using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast;
using HapetFrontend.Entities;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Errors;
using HapetFrontend.Ast.Expressions;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public DeclSymbol GetFuncFromCandidates(string name, AstCallExpr callExpr, List<AstExpression> args, Scope scopeToSearch, 
            AstDeclaration declToSearch, out List<AstExpression> castsToBeDone)
        {
            // getting only the Class::AndFuncName without params
            var splitted = name.Split('(');
            string classWithFuncName = splitted[0];

            int bestScore = int.MaxValue;
            DeclSymbol bestMatch = null;
            castsToBeDone = new List<AstExpression>();

            // skip non funcs
            // skip if different amount of params/args
            // also get parent funcs
            List<DeclSymbol> candidates = new List<DeclSymbol>();
            if (declToSearch is AstClassDecl clsToSearch)
            {
                var currCls = clsToSearch;
                while (currCls != null)
                {
                    candidates.AddRange(GetCandidatesInScope(classWithFuncName, currCls.SubScope)
                        .Where(x => (x.Decl is AstFuncDecl funcDecl && funcDecl.Parameters.Count == args.Count)).ToList());

                    // cringe check for parent :)
                    currCls = currCls.InheritedFrom.Count > 0 ?
                        ((currCls.InheritedFrom[0].OutType as ClassType).Declaration.IsInterface ? null :
                        (currCls.InheritedFrom[0].OutType as ClassType).Declaration) : null;
                }

                // go all over interfaces
                currCls = clsToSearch;
                while (currCls != null)
                {
                    foreach (var inh in currCls.InheritedFrom)
                    {
                        var inhDecl = (inh.OutType as ClassType).Declaration;
                        if (!inhDecl.IsInterface)
                            continue;

                        candidates.AddRange(GetCandidatesInScope(classWithFuncName, inhDecl.SubScope)
                            .Where(x => (x.Decl is AstFuncDecl funcDecl && funcDecl.Parameters.Count == args.Count)).ToList());
                    }

                    // cringe check for parent :)
                    currCls = currCls.InheritedFrom.Count > 0 ?
                        ((currCls.InheritedFrom[0].OutType as ClassType).Declaration.IsInterface ? null :
                        (currCls.InheritedFrom[0].OutType as ClassType).Declaration) : null;
                }
            }
            else if (declToSearch is AstStructDecl strToSearch)
            {
                AstDeclaration currStr = strToSearch;
                while (currStr != null)
                {
                    candidates.AddRange(GetCandidatesInScope(classWithFuncName, currStr.SubScope)
                        .Where(x => (x.Decl is AstFuncDecl funcDecl && funcDecl.Parameters.Count == args.Count)).ToList());

                    // cringe check for parent :)
                    if (currStr is AstStructDecl currStrStruct)
                    {
                        // TODO: also could inherit structs
                        currStr = currStrStruct.InheritedFrom.Count > 0 ?
                            ((currStrStruct.InheritedFrom[0].OutType as ClassType).Declaration.IsInterface ? null :
                            (currStrStruct.InheritedFrom[0].OutType as ClassType).Declaration) : null;
                    }
                    else if (currStr is AstClassDecl currStrClass)
                    {
                        currStr = currStrClass.InheritedFrom.Count > 0 ?
                            ((currStrClass.InheritedFrom[0].OutType as ClassType).Declaration.IsInterface ? null :
                            (currStrClass.InheritedFrom[0].OutType as ClassType).Declaration) : null;
                    }
                }

                // go all over interfaces
                currStr = strToSearch;
                while (currStr != null)
                {
                    if (currStr is AstStructDecl currStrStruct)
                    {
                        foreach (var inh in currStrStruct.InheritedFrom)
                        {
                            // TODO: could be a struct inh
                            var inhDecl = (inh.OutType as ClassType).Declaration;
                            if (!inhDecl.IsInterface)
                                continue;

                            candidates.AddRange(GetCandidatesInScope(classWithFuncName, inhDecl.SubScope)
                                .Where(x => (x.Decl is AstFuncDecl funcDecl && funcDecl.Parameters.Count == args.Count)).ToList());
                        }

                        // cringe check for parent :)
                        currStr = currStrStruct.InheritedFrom.Count > 0 ?
                            ((currStrStruct.InheritedFrom[0].OutType as ClassType).Declaration.IsInterface ? null :
                            (currStrStruct.InheritedFrom[0].OutType as ClassType).Declaration) : null;
                    }
                    else if (currStr is AstClassDecl currStrClass)
                    {
                        foreach (var inh in currStrClass.InheritedFrom)
                        {
                            var inhDecl = (inh.OutType as ClassType).Declaration;
                            if (!inhDecl.IsInterface)
                                continue;

                            candidates.AddRange(GetCandidatesInScope(classWithFuncName, inhDecl.SubScope)
                                .Where(x => (x.Decl is AstFuncDecl funcDecl && funcDecl.Parameters.Count == args.Count)).ToList());
                        }

                        // cringe check for parent :)
                        currStr = currStrClass.InheritedFrom.Count > 0 ?
                            ((currStrClass.InheritedFrom[0].OutType as ClassType).Declaration.IsInterface ? null :
                            (currStrClass.InheritedFrom[0].OutType as ClassType).Declaration) : null;
                    }
                }
            }
            candidates.AddRange(GetCandidates(classWithFuncName, scopeToSearch)
                    .Where(x => (x.Decl is AstFuncDecl funcDecl && funcDecl.Parameters.Count == args.Count)).ToList());
            candidates = candidates.Distinct().ToList();

            // handle explicit shite
            CheckAndPrepareExplicitFuncs(candidates, callExpr);

            foreach (var cand in candidates)
            {
                var funcDecl = cand.Decl as AstFuncDecl;
                int score = 0;
                List<AstExpression> casts = new List<AstExpression>();
                for (int i = 0; i < args.Count; ++i)
                {
                    var arg = args[i];
                    var par = funcDecl.Parameters[i];

                    if (arg.OutType == par.Type.OutType)
                    {
                        score += 0;
                        casts.Add(arg);
                        continue;
                    }

                    CastResult castResult = new CastResult();
                    var cst = PostPrepareExpressionWithType(par.Type, arg, castResult);
                    if (castResult.CouldBeCasted)
                    {
                        score += 1;
                        casts.Add(cst);
                        continue;
                    }
                    else if (castResult.CouldBeNarrowed)
                    {
                        score += 2;
                        casts.Add(arg);
                        continue;
                    }
                    // check if it is a generic parameter
                    else if (par.Type.OutType is PointerType ptrT && 
                        ptrT.TargetType is ClassType clsT &&
                        clsT.Declaration.IsGenericType)
                    {
                        score += 3;
                        casts.Add(arg);
                        continue;
                    }

                    // if nothing - set max val and break
                    score = int.MaxValue;
                    casts.Add(null);
                    break;
                }

                // getting the best candidate
                if (score < bestScore)
                {
                    bestScore = score;
                    bestMatch = cand;
                    castsToBeDone = casts;
                }
                else if (score == bestScore && score != int.MaxValue)
                {
                    // TODO: ambiguous error here that there are two func and we dk which one to call
                }
            }

            return bestMatch;
        }

        private static List<DeclSymbol> GetCandidates(string classWithFuncName, Scope scopeToSearch, List<DeclSymbol> cands = null)
        {
            List<DeclSymbol> candidates = cands ?? new List<DeclSymbol>();
            // search for the func in the scope
            foreach (var (k, d) in GetDecls(scopeToSearch))
            {
                if (k.StartsWith(classWithFuncName) && d != null)
                    candidates.Add(d);
            }
            // search in parent scope
            if (scopeToSearch.Parent != null)
                GetCandidates(classWithFuncName, scopeToSearch.Parent, candidates);
            return candidates;
        }

        private static List<DeclSymbol> GetCandidatesInScope(string classWithFuncName, Scope scopeToSearch, List<DeclSymbol> cands = null)
        {
            List<DeclSymbol> candidates = cands ?? new List<DeclSymbol>();
            // search for the func in the scope
            foreach (var (k, d) in GetDecls(scopeToSearch))
            {
                if (d != null && d.Decl is AstFuncDecl)
                {
                    var onlyFuncName = classWithFuncName.GetPureFuncName();
                    var firstKeyPart = k.GetPureFuncName();
                    if ((k.StartsWith(classWithFuncName) || firstKeyPart == onlyFuncName))
                        candidates.Add(d);

                    // generics check
                    if (onlyFuncName.Contains(GenericsHelper.GENERIC_BEGIN) && firstKeyPart.Contains(GenericsHelper.GENERIC_BEGIN))
                    {
                        // if names without generics are the same and the same amount of parameters
                        string pureFuncName = firstKeyPart.Substring(0, firstKeyPart.IndexOf(GenericsHelper.GENERIC_BEGIN));
                        string pureCallName = onlyFuncName.Substring(0, firstKeyPart.IndexOf(GenericsHelper.GENERIC_BEGIN));

                        int gAmountFunc = firstKeyPart.GetGenericsAmount();
                        int gAmountCall = onlyFuncName.GetGenericsAmount();

                        if (pureFuncName == pureCallName && gAmountFunc == gAmountCall)
                            candidates.Add(d);
                    }

                    // explicit interface impl check
                    /// the same as in <see cref="OtherExtensions.GetSameByNameAndTypes(List{AstFuncDecl}, AstFuncDecl, out int, bool)"/>
                    if (d.Decl.Name.AdditionalData != null)
                    {
                        string pureSearchName = onlyFuncName.GetClassNameWithoutNamespace();
                        string pureName = firstKeyPart.GetClassNameWithoutNamespace();

                        if (pureName == pureSearchName)
                            candidates.Add(d);
                    }
                }
            }
            return candidates;
        }

        private static IEnumerable<(string, DeclSymbol)> GetDecls(Scope scope)
        {
            // search for the func in the shadow
            foreach (var k in scope.ShadowSymbolTable.Keys)
            {
                yield return (k, scope.ShadowSymbolTable[k] as DeclSymbol);
            }
            // search for the func in the scope
            foreach (var k in scope.SymbolTable.Keys)
            {
                yield return (k, scope.SymbolTable[k] as DeclSymbol);
            }
        }

        private void CheckAndPrepareExplicitFuncs(List<DeclSymbol> decls, AstCallExpr callExpr)
        {
            // return when less than 1 - not explicit
            if (decls.Count < 1)
                return;

            var declsCopied = decls.ToList();
            bool thereWasExplicitShite = false;
            foreach (var d in declsCopied)
            {
                var funcDecl = d.Decl as AstFuncDecl;
                var onlyFuncName = funcDecl.Name.Name.GetPureFuncName();

                // skip non explicit
                if (funcDecl.Name.AdditionalData == null)
                    continue;

                // to handle then
                thereWasExplicitShite = true;

                // remove it from candidates
                decls.Remove(d);

                // search if there funcs from interfaces - remove them also
                string pureSearchParent = (funcDecl.Name.AdditionalData.OutType as ClassType).Declaration.Name.Name;
                string pureSearchName = onlyFuncName.GetClassNameWithoutNamespace();
                foreach (var dIn in declsCopied)
                {
                    var funcDeclIn = dIn.Decl as AstFuncDecl;
                    if (funcDeclIn.Name.Name.StartsWith($"{pureSearchParent}::{pureSearchName}("))
                        decls.Remove(dIn);
                }
            }

            if (thereWasExplicitShite && decls.Count == 0)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [callExpr.FuncName.Name], ErrorCode.Get(CTEN.ExplicitMethodCall));
            }
        }
    }
}
