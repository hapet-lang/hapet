using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Entities;
using HapetFrontend.Types;
using System.Xml.Linq;

namespace HapetFrontend.Scoping
{
    public partial class Scope
    {
        public bool DefineLocalSymbol(ISymbol symbol, string name = null)
        {
            name ??= symbol.Name;
            if (name == "_")
                return true;

            if (_symbolTable.TryGetValue(name, out var other))
                return false;

            _symbolTable[name] = symbol;
            return true;
        }

        public bool RemoveLocalSymbol(ISymbol symbol, string name = null)
        {
            name ??= symbol.Name;
            if (name == "_")
                return true;

            if (!_symbolTable.ContainsKey(name))
                return false;

            _symbolTable.Remove(name);
            return true;
        }

        public bool DefineSymbol(ISymbol symbol, string name = null)
        {
            return DefineLocalSymbol(symbol, name);
        }

        private bool DefineTypeSymbol(string name, HapetType tp)
        {
            return DefineSymbol(new DeclSymbol(name, new AstBuiltInTypeDecl(tp)));
        }

        public bool DefineNamespaceSymbol(string name, Scope nsScope)
        {
            return DefineSymbol(new NamespaceSymbol(name, nsScope));
        }

        public bool DefineDeclSymbol(string name, AstDeclaration decl)
        {
            return DefineSymbol(new DeclSymbol(name, decl));
        }

        public bool RemoveDeclSymbol(string name, AstDeclaration decl)
        {
            return RemoveLocalSymbol(new DeclSymbol(name, decl));
        }

        public bool RenameSymbol(string oldName, string newName)
        {
            if (!_symbolTable.TryGetValue(oldName, out var other))
                return false;

            _symbolTable[newName] = other;
            _symbolTable.Remove(oldName);

            return true;
        }

        public ISymbol GetSymbol(string name, bool searchUsedScopes = true, bool searchParentScope = true, bool searchPartNamespace = false)
        {
            if (_symbolTable.ContainsKey(name))
            {
                var v = _symbolTable[name];
                return v;
            }

            // this cringe is used to search a part namespace
            if (searchPartNamespace)
            {
                foreach (var k in _symbolTable.Keys)
                {
                    if (k.StartsWith(name) && _symbolTable[k] is NamespaceSymbol)
                    {
                        return _symbolTable[k];
                    }
                }
            }

            if (_usedScopes != null && searchUsedScopes)
            {
                List<ISymbol> found = new List<ISymbol>();
                foreach (var scope in _usedScopes)
                {
                    var sym = scope.GetSymbol(name, false, false);
                    if (sym == null)
                        continue;
                    found.Add(sym);
                }

                if (found.Count == 1)
                    return found[0];
                if (found.Count > 1)
                    return new AmbiguousSymol(found);
            }

            if (searchParentScope)
                return Parent?.GetSymbol(name, searchUsedScopes, searchParentScope, searchPartNamespace);
            return null;
        }

        // to get decl in namespace
        public DeclSymbol GetSymbolInNamespace(string ns, string symbol, bool searchUsedScopes = true, bool searchParentScope = true)
        {
            NamespaceSymbol nsSymbol = GetSymbol(ns, searchUsedScopes, searchParentScope) as NamespaceSymbol;
            if (nsSymbol == null)
                return null;

            return nsSymbol.Scope.GetSymbol($"{ns}.{symbol}", searchUsedScopes, searchParentScope) as DeclSymbol;
        }

        public bool IsStringNamespaceOrPart(string testString, bool searchUsedScopes = true, bool searchParentScope = true)
        {
            NamespaceSymbol nsSymbol = GetSymbol(testString, searchUsedScopes, searchParentScope) as NamespaceSymbol;
            if (nsSymbol != null)
                return true;

            // it is probably a part
            NamespaceSymbol nsSymbolPart = GetSymbol(testString, searchUsedScopes, searchParentScope, true) as NamespaceSymbol;
            if (nsSymbolPart != null)
                return true;

            return false;
        }

        public AstClassDecl GetClass(string name)
        {
            var sym = GetSymbol(name);
            if (sym is AstClassDecl s)
                return s;
            return null;
        }

        public AstStructDecl GetStruct(string name)
        {
            var sym = GetSymbol(name);
            if (sym is AstStructDecl s)
                return s;
            return null;
        }

        public AstEnumDecl GetEnum(string name)
        {
            var sym = GetSymbol(name);
            if (sym is AstEnumDecl s)
                return s;
            return null;
        }

        public DeclSymbol GetFuncFromCandidates(string name, List<AstExpression> args, PostPrepare postPrepare, AstDeclaration declToSearch, out List<AstExpression> castsToBeDone)
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
            else
            {
                candidates.AddRange(GetCandidates(classWithFuncName)
                        .Where(x => (x.Decl is AstFuncDecl funcDecl && funcDecl.Parameters.Count == args.Count)).ToList());
            }

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
                    var cst = postPrepare.PostPrepareExpressionWithType(par.Type.OutType, arg, castResult);
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

        private List<DeclSymbol> GetCandidates(string classWithFuncName, List<DeclSymbol> cands = null)
        {
            List<DeclSymbol> candidates = cands ?? new List<DeclSymbol>();
            // search for the func in the scope
            foreach (var k in _symbolTable.Keys)
            {
                if (k.StartsWith(classWithFuncName) && _symbolTable[k] is DeclSymbol ds)
                    candidates.Add(ds);
            }
            // search in parent scope
            if (Parent != null)
                Parent.GetCandidates(classWithFuncName, candidates);
            return candidates;
        }

        private List<DeclSymbol> GetCandidatesInScope(string classWithFuncName, Scope scopeToSearch, List<DeclSymbol> cands = null)
        {
            List<DeclSymbol> candidates = cands ?? new List<DeclSymbol>();
            // search for the func in the scope
            foreach (var k in scopeToSearch._symbolTable.Keys)
            {
                if (scopeToSearch._symbolTable[k] is DeclSymbol ds && ds.Decl is AstFuncDecl)
                {
                    var onlyFuncName = classWithFuncName.Split("::")[1];
                    var firstKeyPart = k.Split("(")[0].Split("::")[1];
                    if ((k.StartsWith(classWithFuncName) || firstKeyPart == onlyFuncName))
                        candidates.Add(ds);
                }
            }
            return candidates;
        }
    }
}
