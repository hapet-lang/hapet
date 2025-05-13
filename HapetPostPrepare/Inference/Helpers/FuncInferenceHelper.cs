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
        public DeclSymbol GetFuncFromCandidates(AstIdExpr name, AstCallExpr callExpr, List<AstArgumentExpr> args, 
            AstDeclaration declToSearch, bool callFromObject, out List<AstExpression> castsToBeDone)
        {
            int bestScore = int.MaxValue;
            DeclSymbol bestMatch = null;
            castsToBeDone = new List<AstExpression>();

            // getting all the candidates
            List<DeclSymbol> candidates = GetAllCandidates(name, callExpr, declToSearch, args, callFromObject);

            // handle explicit shite
            CheckAndPrepareExplicitFuncs(candidates, callExpr);

            // there has to be only one func when no args is set
            if (args == null)
            {
                if (candidates.Count > 1)
                {
                    // error
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr, 
                        [candidates[0].Name.Name, candidates[1].Name.Name], 
                        ErrorCode.Get(CTEN.AmbiguousFunctionCall));
                }
                return candidates.FirstOrDefault();
            }

            // filter the candidates
            foreach (var cand in candidates)
            {
                // if set to non-null - should be checked in every frame of arg check :)
                AstParamDecl paramsParamDecl = null;

                var funcDecl = cand.Decl as AstFuncDecl;
                int score = 0;
                List<AstExpression> casts = new List<AstExpression>();
                for (int i = 0; i < args.Count; ++i)
                {
                    var arg = args[i];
                    var argExpr = arg.Expr;

                    var par = paramsParamDecl ?? GetParameterByIndexOrName(funcDecl.Parameters, arg, i, out var _);
                    // break loop if there is no param with specified name
                    if (par == null)
                        break;

                    // cringe to handle 'arglist' kw
                    if (par.IsArglist)
                    {
                        paramsParamDecl = par;
                        score += 4;
                        casts.Add(argExpr);
                        continue;
                    }

                    // cringe to handle 'params' kw
                    var parType = par.Type;
                    if (par.IsParams) 
                    {
                        paramsParamDecl = par;
                        // because usually like 'params object[] pivo'
                        // but we need to compare pure 'object' type then :)
                        var tmp = (par.Type as AstNestedExpr).RightPart as AstArrayExpr;
                        parType = tmp.SubExpression; 
                    }

                    if (argExpr.OutType == parType.OutType)
                    {
                        score += 0;
                        casts.Add(argExpr);
                        continue;
                    }
                    // if putting 'null' as an arg
                    else if (argExpr is AstNullExpr && parType.OutType is PointerType)
                    {
                        score += 0;
                        casts.Add(argExpr);
                        continue;
                    }
                    CastResult castResult = new CastResult();
                    var cst = PostPrepareExpressionWithType(parType, argExpr, castResult);
                    if (castResult.CouldBeCasted)
                    {
                        score += 1;
                        casts.Add(cst);
                        continue;
                    }
                    else if (castResult.CouldBeNarrowed)
                    {
                        score += 3;
                        casts.Add(argExpr);
                        continue;
                    }
                    // check if it is a generic parameter 
                    // WARN: better generic check? like T[] or T* ?
                    else if (parType.OutType is GenericType)
                    {
                        score += 2;
                        casts.Add(argExpr);
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
                    // ambiguous error here that there are two func and we dk which one to call
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr,
                        [bestMatch.Name.Name, cand.Name.Name],
                        ErrorCode.Get(CTEN.AmbiguousFunctionCall));
                }
            }

            return bestMatch;
        }

        public static AstParamDecl GetParameterByIndexOrName(List<AstParamDecl> pars, AstArgumentExpr arg, int fallbackIndex, out int realIndex)
        {
            // if there is no name in arg - return by index
            if (arg.Name == null)
            {
                realIndex = fallbackIndex;
                return pars.Count > fallbackIndex ? pars[fallbackIndex] : null;
            }

            for (int i = 0; i < pars.Count; ++i)
            {
                if (pars[i].Name.Name == arg.Name.Name)
                {
                    realIndex = i;
                    return pars[i];
                }
            }
            realIndex = -1;
            return null;
        }

        public List<AstArgumentExpr> GenerateNormalArguments(List<AstParamDecl> pars, List<AstArgumentExpr> args, AstStatement caller)
        {
            List<AstArgumentExpr> normalArgs = Enumerable.Repeat<AstArgumentExpr>(null, pars.Count).ToList();
            for (int i = 0; i < args.Count; ++i)
            {
                var currArg = args[i];
                var currPar = GetParameterByIndexOrName(pars, currArg, i, out int realIndex);

                // special case for 'params' cringe
                if (currPar.IsParams)
                {
                    // pizdec
                    var exprs = args.Select(x => x.Expr).Skip(i).ToList();
                    var arrCreate = new AstArrayCreateExpr(
                        GetPreparedAst(currArg.Expr.OutType, currArg),
                        new List<AstExpression>() { new AstNumberExpr(NumberData.FromInt(exprs.Count)) },
                        exprs,
                        new Location(exprs.First().Beginning, exprs.Last().Ending)
                    )
                    {
                        Scope = currArg.Scope
                    };
                    normalArgs[realIndex] = new AstArgumentExpr(arrCreate); // set and go out
                    break;
                }

                // special case for 'arglist' cringe
                if (currPar.IsArglist)
                {
                    // pizdec
                    var exprs = args.Select(x => x.Expr).Skip(i).ToList();
                    normalArgs[realIndex] = new AstArgumentExpr(exprs[0]); // set the first one
                    // we really need to add them :)
                    foreach (var tmpA in exprs.Skip(1))
                        normalArgs.Add(new AstArgumentExpr(tmpA));
                    break;
                }

                normalArgs[realIndex] = currArg;
            }

            // check for unset shite - try use default values
            for (int i = 0; i < pars.Count; ++i)
            {
                var currPar = pars[i];

                // skip params that do not have default values - they are already set
                if (currPar.DefaultValue == null)
                    continue;

                // search for the arg - skip if the was an arg for the param
                var theArg = args.FirstOrDefault(x => x.Name != null && x.Name.Name == currPar.Name.Name);
                if (theArg != null)
                    continue;

                normalArgs[i] = new AstArgumentExpr(currPar.DefaultValue);
            }

            // we need to create an empty array for 'params' if there was no args
            var paramsParam = pars.FirstOrDefault(x => x.IsParams);
            if (paramsParam != null)
            {
                int indexx = pars.IndexOf(paramsParam);
                if (normalArgs[indexx] == null)
                {
                    var arrCreate = new AstArrayCreateExpr(
                        GetPreparedAst(paramsParam.Type.OutType, paramsParam),
                        new List<AstExpression>() { new AstNumberExpr(NumberData.FromInt(0)) },
                        new List<AstExpression>(),
                        new Location(caller.Beginning, caller.Ending)
                    )
                    {
                        Scope = caller.Scope
                    };
                    normalArgs[indexx] = new AstArgumentExpr(arrCreate);
                }
            }

            // we need to remove arg of 'arglist' if there was no args
            var arglistParam = pars.FirstOrDefault(x => x.IsArglist);
            if (arglistParam != null)
            {
                int indexx = pars.IndexOf(arglistParam);
                if (normalArgs[indexx] == null)
                {
                    normalArgs.RemoveAt(indexx);
                }
            }

            return normalArgs;
        }

        private static List<DeclSymbol> GetAllCandidates(AstIdExpr name, AstCallExpr callExpr, AstDeclaration declToSearch, 
            List<AstArgumentExpr> args, bool callFromObject)
        {
            List<DeclSymbol> candidates = new List<DeclSymbol>();

            candidates.AddRange(Candidates_Step1_InheritedAndCurrent(name, declToSearch));                  // step 1
            candidates.AddRange(Candidates_Step2_CurrentScopeAndParents(name, callExpr));                   // step 2
            var argsAmount = args == null ? -1 : args.Count;
            candidates = Candidates_Step3_MinAmountParams(candidates, argsAmount).ToList();                 // step 3
            candidates = Candidates_Step4_OnlyOneGeneric(name, candidates).ToList();                        // step 4
            candidates = Candidates_Step5_StaticOrNon(candidates, callFromObject).ToList();                 // step 5

            return candidates.Distinct().ToList();
        }

        private static List<DeclSymbol> Candidates_Step1_InheritedAndCurrent(AstIdExpr name, AstDeclaration declToSearch)
        {
            List<DeclSymbol> candidates = new List<DeclSymbol>();

            // go all over inherited types - skip interfaces

            // add current decl subscope' decls
            candidates.AddRange(GetCandidatesInScope(name, declToSearch.SubScope));

            // get only inherited parents - that has implementations
            foreach (var inh in declToSearch.GetInheritedTypes())
            {
                var inhDecl = (inh.OutType as ClassType).Declaration;
                // get parent class decls
                candidates.AddRange(Candidates_Step1_InheritedAndCurrent(name, inhDecl));
            }
            return candidates;
        }

        private static List<DeclSymbol> Candidates_Step2_CurrentScopeAndParents(AstIdExpr name, AstCallExpr callExpr)
        {
            if (callExpr == null || callExpr.Scope == null)
                return new List<DeclSymbol>();
            return GetCandidatesInScope(name, callExpr.Scope, searchParent: true); // also search parents
        }

        private static IEnumerable<DeclSymbol> Candidates_Step3_MinAmountParams(IEnumerable<DeclSymbol> decls, int argsAmount)
        {
            foreach (var d in decls)
            {
                var funcDecl = d.Decl as AstFuncDecl;

                // if args amount is -1 - then any func is ok
                if (argsAmount == -1)
                    yield return d;

                // allow if func has equal amount of params and args
                if (funcDecl.Parameters.Count == argsAmount)
                    yield return d;

                // check if func has bigger amount of params than args
                if (funcDecl.Parameters.Count > argsAmount)
                {
                    // we need to be sure that the last params has default values
                    // if not - no yield probably
                    bool isOk = true;
                    var unsetParsAmount = funcDecl.Parameters.Count - argsAmount;
                    for (int i = 0; i < unsetParsAmount; ++i)
                    {
                        var thePar = funcDecl.Parameters[funcDecl.Parameters.Count - 1 - i];
                        if (thePar.DefaultValue == null)
                        {
                            isOk = false;
                            break;
                        }
                    }
                    if (isOk)
                        yield return d;
                    // if the last param is params or arglist - 
                    // the decl would be also returned correctly below
                }

                // skip if 0 params - because args amount is non zero!!!
                if (funcDecl.Parameters.Count == 0)
                    continue;

                // if not bigger - check if the last param with 'params' or 'arglist' cringe - allow
                if (funcDecl.Parameters.Last().IsParams || funcDecl.Parameters.Last().IsArglist)
                    yield return d;
            }
        }

        private static IEnumerable<DeclSymbol> Candidates_Step4_OnlyOneGeneric(AstIdExpr name, IEnumerable<DeclSymbol> decls)
        {
            // skip if non generic search
            if (name is AstIdGenericExpr genId)
            {
                /// almost the same checks as in <see cref="Scope.GetSymbol"/>
                // we need to store original decl 
                // if we won't find the same decl
                // we need to return original one
                DeclSymbol originalGeneric = null;
                DeclSymbol theSameGeneric = null;
                foreach (var d in decls)
                {
                    if (d.Name is not AstIdGenericExpr currId)
                    {
                        yield return d;
                        continue;
                    }

                    bool allEqual = true;
                    for (int i = 0; i < currId.GenericRealTypes.Count; ++i)
                    {
                        if (currId.GenericRealTypes[i].OutType == genId.GenericRealTypes[i].OutType)
                        {
                            continue;
                        }
                        if (currId.GenericRealTypes[i].OutType is GenericType kType &&
                            genId.GenericRealTypes[i].OutType is GenericType sType &&
                            kType.ParentDeclaration is AstDeclaration fDecl &&
                            sType.ParentDeclaration is AstDeclaration sDecl)
                        {
                            // check that original decls are the same
                            var fComp1 = fDecl.IsImplOfGeneric ? fDecl.OriginalGenericDecl : fDecl;
                            var fComp2 = sDecl.IsImplOfGeneric ? sDecl.OriginalGenericDecl : sDecl;
                            if (fComp1 == fComp2 && kType.Name.Name == sType.Name.Name)
                            {
                                continue;
                            }
                        }
                        allEqual = false;
                        break;
                    }
                    if (d is DeclSymbol ds1 &&
                        ds1.Decl.IsImplOfGeneric &&
                        allEqual)
                    {
                        theSameGeneric = d as DeclSymbol;
                        continue;
                    }

                    // if original generic
                    if (d is DeclSymbol ds &&
                        ds.Decl.HasGenericTypes &&
                        !ds.Decl.IsImplOfGeneric)
                    {
                        originalGeneric = d as DeclSymbol;
                        continue;
                    }
                }
                // try at first to return the same
                if (theSameGeneric != null)
                    yield return theSameGeneric;
                // then try to return original generic
                else if (originalGeneric != null)
                    yield return originalGeneric;
            }
            else
            {
                // return all if non-generic shite
                foreach (var d in decls)
                {
                    yield return d;
                }
            }
        }

        private static IEnumerable<DeclSymbol> Candidates_Step5_StaticOrNon(IEnumerable<DeclSymbol> decls, bool callFromObject)
        {
            if (callFromObject)
            {
                // return all non-static funcs
                foreach (var d in decls)
                {
                    if (!d.Decl.SpecialKeys.Contains(HapetFrontend.Parsing.TokenType.KwStatic))
                        yield return d;
                }
            }
            else
            {
                // return all static funcs
                foreach (var d in decls)
                {
                    if (d.Decl.SpecialKeys.Contains(HapetFrontend.Parsing.TokenType.KwStatic))
                        yield return d;
                }
            }
        }

        #region Helpers
        private static List<DeclSymbol> GetCandidatesInScope(AstIdExpr classWithFuncName, Scope scopeToSearch, bool searchParent = false)
        {
            List<DeclSymbol> candidates = new List<DeclSymbol>();
            // search for the func in the scope
            foreach (var (k, d) in GetDecls(scopeToSearch))
            {
                if (d != null && d.Decl is AstFuncDecl)
                {
                    // 1 - name similarity checks
                    var onlyFuncName = classWithFuncName.Name.GetPureFuncName();
                    var firstKeyPart = k.Name.GetPureFuncName();
                    if (k.Name.StartsWith(classWithFuncName.Name) || firstKeyPart == onlyFuncName)
                    {
                        candidates.Add(d);
                        continue;
                    }

                    // 2 - generics check
                    if (classWithFuncName is AstIdGenericExpr searchGen && k is AstIdGenericExpr symbolGen)
                    {
                        int gAmountFunc = symbolGen.GenericRealTypes.Count;
                        int gAmountCall = searchGen.GenericRealTypes.Count;
                        if (onlyFuncName == firstKeyPart && gAmountFunc == gAmountCall)
                        {
                            candidates.Add(d);
                            continue;
                        }
                    }

                    // 3 - explicit interface impl check
                    /// the same as in <see cref="OtherExtensions.GetSameByNameAndTypes(List{AstFuncDecl}, AstFuncDecl, out int, bool)"/>
                    if (d.Decl.Name.AdditionalData != null)
                    {
                        string pureSearchName = onlyFuncName.GetClassNameWithoutNamespace();
                        string pureName = firstKeyPart.GetClassNameWithoutNamespace();
                        if (pureName == pureSearchName)
                        {
                            candidates.Add(d);
                            continue;
                        }
                    }
                }
            }

            // if search in parent scopes
            if (searchParent && scopeToSearch.Parent != null)
                GetCandidatesInScope(classWithFuncName, scopeToSearch.Parent, searchParent);

            return candidates;
        }

        private static IEnumerable<(AstIdExpr, DeclSymbol)> GetDecls(Scope scope)
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
        #endregion

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
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr?.FuncName, [callExpr?.FuncName.Name], ErrorCode.Get(CTEN.ExplicitMethodCall));
            }
        }
    }
}
