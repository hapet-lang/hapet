using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Parsing.PostPrepare;
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

        public DeclSymbol GetFuncFromCandidates(string name, List<AstExpression> args, PostPrepare postPrepare, out List<AstExpression> castsToBeDone)
        {
            // getting only the Class::AndFuncName without params
            var splitted = name.Split('(');
            string classWithFuncName = splitted[0];

            int bestScore = int.MaxValue;
            DeclSymbol bestMatch = null;
            castsToBeDone = new List<AstExpression>();

            // skip non funcs
            // skip if different amount of params/args
            List<DeclSymbol> candidates = GetCandidates(classWithFuncName)
                .Where(x => (x.Decl is AstFuncDecl funcDecl && funcDecl.Parameters.Count == args.Count)).ToList();

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

                    PostPrepare.CastResult castResult = new PostPrepare.CastResult();
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
    }
}
