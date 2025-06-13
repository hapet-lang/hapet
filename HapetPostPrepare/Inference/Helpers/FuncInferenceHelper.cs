using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast;
using HapetFrontend.Entities;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Errors;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Parsing;
using HapetFrontend.Enums;
using System.Reflection.Metadata;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public DeclSymbol GetFuncFromCandidates(AstIdExpr name, AstCallExpr callExpr, List<AstArgumentExpr> args, 
            AstDeclaration declToSearch, bool callFromObject, out List<AstExpression> castsToBeDone)
        {
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

            // to handle all decls, their scores and casts
            List<(int, DeclSymbol, List<AstExpression>)> declWithScores = new List<(int, DeclSymbol, List<AstExpression>)>();

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
                    
                    var parType = par.Type;

                    // cringe to handle 'arglist' kw
                    if (par.ParameterModificator == ParameterModificator.Arglist)
                    {
                        paramsParamDecl = par;
                        score += 4;
                        casts.Add(argExpr);
                        continue;
                    }
                    // cringe to handle 'params' kw
                    else if (par.ParameterModificator == ParameterModificator.Params) 
                    {
                        paramsParamDecl = par;
                        // because usually like 'params object[] pivo'
                        // but we need to compare pure 'object' type then :)
                        var tmp = (par.Type as AstNestedExpr).RightPart as AstArrayExpr;
                        parType = tmp.SubExpression; 
                    }

                    // if parameter or argument has ref/out another one also has to have it
                    if (arg.ArgumentModificator == ParameterModificator.Ref ||
                        arg.ArgumentModificator == ParameterModificator.Out ||
                        par.ParameterModificator == ParameterModificator.Ref ||
                        par.ParameterModificator == ParameterModificator.Out)
                    {
                        // this is a special case when calling a function on a struct
                        // that has not been overrided. so the first arg has 'ref' 
                        // modifier and the func's first parameter is just a class
                        // we need to allow it via cast
                        bool allow = (i == 0) && callFromObject &&
                            (arg.ArgumentModificator == ParameterModificator.Ref) && parType.OutType is ClassType;

                        // they has to be the same
                        if (arg.ArgumentModificator != par.ParameterModificator && !allow)
                        {
                            score = int.MaxValue;
                            casts.Add(null);
                            break;
                        }
                    }

                    if (argExpr.OutType == parType.OutType)
                    {
                        score += 0;
                        casts.Add(argExpr);
                        continue;
                    }
                    else if (ArrayType.IsCouldBeCastedIncludingArray(argExpr.OutType, parType.OutType))
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
                    var cst = PostPrepareExpressionWithType(parType.OutType, argExpr, castResult);
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

                // add the candidate
                declWithScores.Add((score, cand, casts));
            }

            // gg if there is no that func
            if (declWithScores.Count == 0)
                return null;

            // we need to sort and handle the decls
            if (declWithScores.Count > 1)
            {
                // sort
                declWithScores = declWithScores.OrderBy(x => x.Item1).ToList();
                var best = declWithScores[0];
                // if the next one has the same score - ambiguous
                if (declWithScores[1].Item1 == best.Item1)
                {
                    // ambiguous error here that there are two func and we dk which one to call
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr,
                        [best.Item2.Name.Name, declWithScores[1].Item2.Name.Name],
                        ErrorCode.Get(CTEN.AmbiguousFunctionCall));
                }
            }

            // if the decl has MaxInt score - cringe
            if (declWithScores[0].Item1 == int.MaxValue)
                return null;

            castsToBeDone = declWithScores[0].Item3;
            return declWithScores[0].Item2;
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
                if (currPar.ParameterModificator == ParameterModificator.Params)
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
                else if (currPar.ParameterModificator == ParameterModificator.Arglist)
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
            var paramsParam = pars.FirstOrDefault(x => x.ParameterModificator == ParameterModificator.Params);
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
            var arglistParam = pars.FirstOrDefault(x => x.ParameterModificator == ParameterModificator.Arglist);
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

            candidates.AddRange(Candidates_Step1_InheritedAndCurrent(name, declToSearch, callFromObject));  // step 1
            //candidates.AddRange(Candidates_Step2_CurrentScopeAndParents(name, callExpr, callFromObject));   // step 2
            var argsAmount = args == null ? -1 : args.Count;
            candidates = Candidates_Step3_MinAmountParams(candidates, argsAmount).ToList();                 // step 3
            candidates = Candidates_Step4_OnlyOneGeneric(name, candidates).ToList();                        // step 4
            candidates = Candidates_Step5_StaticOrNon(candidates, callFromObject).ToList();                 // step 5
            Candidates_6_RemoveOverrided(candidates, callFromObject);                                       // step 6

            return candidates.Distinct().ToList();
        }

        private static List<DeclSymbol> Candidates_Step1_InheritedAndCurrent(AstIdExpr name, AstDeclaration declToSearch, bool callFromObject)
        {
            List<DeclSymbol> candidates = new List<DeclSymbol>();

            // go all over inherited types - skip interfaces

            // get only inherited parents - that has implementations
            foreach (var inh in declToSearch.GetInheritedTypes())
            {
                // WARN: we go all over the inherited types because
                // we could be searching currently in interface scope
                // and so we need to check all inherited interfaces
                var inhDecl = (inh.OutType as ClassType).Declaration;
                // get parent class decls
                candidates.AddRange(Candidates_Step1_InheritedAndCurrent(name, inhDecl, callFromObject));
            }

            // remove the same
            candidates = candidates.Distinct().ToList();

            // add current decl subscope' decls
            var currentDecls = GetCandidatesInScope(name, declToSearch.SubScope, callFromObject: callFromObject);
            foreach (var currDecl in currentDecls)
            {
                // we need to manually check shadowing funcs
                if (currDecl.Decl.SpecialKeys.Contains(TokenType.KwNew))
                {
                    // search for shadowed func
                    candidates.Select(x => x.Decl as AstFuncDecl).ToList().GetSameByNameAndTypes(currDecl.Decl as AstFuncDecl, out int index, callFromObject);
                    candidates[index] = currDecl;
                    continue;
                }

                // we need to manually check for overriding funcs
                if (currDecl.Decl.SpecialKeys.Contains(TokenType.KwOverride) && callFromObject)
                {
                    // search for overrided func
                    candidates.Select(x => x.Decl as AstFuncDecl).ToList().GetSameByNameAndTypes(currDecl.Decl as AstFuncDecl, out int index, callFromObject);
                    candidates[index] = currDecl;
                    continue;
                }

                // else - we just add it
                candidates.Add(currDecl);
            }

            return candidates;
        }

        private static List<DeclSymbol> Candidates_Step2_CurrentScopeAndParents(AstIdExpr name, AstCallExpr callExpr, bool callFromObject)
        {
            if (callExpr == null || callExpr.Scope == null)
                return new List<DeclSymbol>();
            return GetCandidatesInScope(name, callExpr.Scope, searchParent: true, callFromObject: callFromObject); // also search parents
        }

        private static IEnumerable<DeclSymbol> Candidates_Step3_MinAmountParams(IEnumerable<DeclSymbol> decls, int argsAmount)
        {
            foreach (var d in decls)
            {
                List<AstParamDecl> parameters;
                if (d.Decl is AstFuncDecl funcDecl)
                    parameters = funcDecl.Parameters;
                else
                    parameters = (d.Decl.Type.OutType as DelegateType).TargetDeclaration.Parameters;

                // if args amount is -1 - then any func is ok
                if (argsAmount == -1)
                    yield return d;

                // allow if func has equal amount of params and args
                if (parameters.Count == argsAmount)
                    yield return d;

                // check if func has bigger amount of params than args
                if (parameters.Count > argsAmount)
                {
                    // we need to be sure that the last params has default values
                    // if not - no yield probably
                    bool isOk = true;
                    var unsetParsAmount = parameters.Count - argsAmount;
                    for (int i = 0; i < unsetParsAmount; ++i)
                    {
                        var thePar = parameters[parameters.Count - 1 - i];
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
                if (parameters.Count == 0)
                    continue;

                // if not bigger - check if the last param with 'params' or 'arglist' cringe - allow
                if (parameters.Last().ParameterModificator == ParameterModificator.Params ||
                    parameters.Last().ParameterModificator == ParameterModificator.Arglist)
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
                            kType.Declaration.ParentDecl is AstDeclaration fDecl &&
                            sType.Declaration.ParentDecl is AstDeclaration sDecl)
                        {
                            // check that original decls are the same
                            var fComp1 = fDecl.IsImplOfGeneric ? fDecl.OriginalGenericDecl : fDecl;
                            var fComp2 = sDecl.IsImplOfGeneric ? sDecl.OriginalGenericDecl : sDecl;
                            if (fComp1 == fComp2 && kType.Declaration.Name.Name == sType.Declaration.Name.Name)
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

        /// <summary>
        /// The func is going to remove all parent-parent funcs that are already overriden
        /// Like calling ToString() from a struct would candidate both ValueType and Object
        /// ToString() methods. So we need to leave only one of them - ValueType'
        /// </summary>
        /// <param name="decls"></param>
        /// <returns></returns>
        private static void Candidates_6_RemoveOverrided(List<DeclSymbol> decls, bool callFromObject)
        {
            List<AstDeclaration> toRemove = new List<AstDeclaration>();
            foreach (var decl in decls)
            {
                // leave the funcs with no override for now
                if (!decl.Decl.SpecialKeys.Contains(TokenType.KwOverride) && !decl.Decl.SpecialKeys.Contains(TokenType.KwNew))
                    continue;

                // getting parent of current func and checking other func' parents to find the same
                var currentParent = decl.Decl.ContainingParent;
                foreach (var d in decls)
                {
                    var nestedParent = d.Decl.ContainingParent;
                    // skip structs
                    if (nestedParent.Type.OutType is not ClassType clsTNested)
                        continue;
                    // skip non inherited
                    if (!currentParent.Type.OutType.IsInheritedFrom(clsTNested))
                        continue;

                    // go up to the biggest parent class
                    while (clsTNested != null)
                    {
                        // search for the same func in parent
                        var theFunc = clsTNested.Declaration.Declarations.
                            Where(x => x is AstFuncDecl).
                            Select(x => x as AstFuncDecl).ToList().
                            GetSameByNameAndTypes(decl.Decl as AstFuncDecl, out int _, callFromObject);
                        if (theFunc != null)
                        {
                            toRemove.Add(theFunc);
                        }

                        // get the next parent
                        clsTNested = clsTNested.Declaration.InheritedFrom.Count > 0 ? clsTNested.Declaration.InheritedFrom[0].OutType as ClassType : null;
                    }
                }
            }

            // remove them
            foreach (var r in toRemove)
            {
                decls.RemoveAll(x => x.Decl == r);
            }
        }

        #region Helpers
        private static List<DeclSymbol> GetCandidatesInScope(AstIdExpr classWithFuncName, Scope scopeToSearch, bool searchParent = false, bool callFromObject = false)
        {
            List<DeclSymbol> candidates = new List<DeclSymbol>();
            // search for the func in the scope
            foreach (var (k, d) in GetDecls(scopeToSearch))
            {
                if (d != null && (d.Decl is AstFuncDecl || d.Decl.Type.OutType is DelegateType))
                {
                    // 1 - name similarity checks - DO NOT ALLOW GENERICS HERE 
                    // generic checks are below
                    var onlyFuncName = classWithFuncName.Name.GetPureFuncName();
                    var firstKeyPart = k.Name.GetPureFuncName();
                    if ((k.Name.StartsWith(classWithFuncName.Name) || firstKeyPart == onlyFuncName) 
                        && (d.Decl.Name is not AstIdGenericExpr && classWithFuncName is not AstIdGenericExpr))
                    {
                        // add static func if not callFromObject and add non-static if callFromObject
                        // or just add if it is a delegate
                        if ((callFromObject && !d.Decl.SpecialKeys.Contains(TokenType.KwStatic)) || 
                            (!callFromObject && d.Decl.SpecialKeys.Contains(TokenType.KwStatic)) ||
                            d.Decl.Type.OutType is DelegateType)
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
                            // add static func if not callFromObject and add non-static if callFromObject
                            // or just add if it is a delegate
                            if ((callFromObject && !d.Decl.SpecialKeys.Contains(TokenType.KwStatic)) ||
                                (!callFromObject && d.Decl.SpecialKeys.Contains(TokenType.KwStatic)) ||
                                d.Decl.Type.OutType is DelegateType)
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
                            // add static func if not callFromObject and add non-static if callFromObject
                            // or just add if it is a delegate
                            if ((callFromObject && !d.Decl.SpecialKeys.Contains(TokenType.KwStatic)) ||
                                (!callFromObject && d.Decl.SpecialKeys.Contains(TokenType.KwStatic)) ||
                                d.Decl.Type.OutType is DelegateType)
                                candidates.Add(d);
                            continue;
                        }
                    }
                }
            }

            // if search in parent scopes
            if (searchParent && scopeToSearch.Parent != null)
                candidates.AddRange(GetCandidatesInScope(classWithFuncName, scopeToSearch.Parent, searchParent, callFromObject));

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
                // skip delegates
                if (funcDecl == null)
                    continue;
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
                    // skip delegates
                    if (funcDeclIn == null)
                        continue;
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
