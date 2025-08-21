using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using System.Xml.Linq;

namespace HapetFrontend.Scoping
{
    public partial class Scope
    {
        private bool DefineLocalSymbol(ISymbol symbol)
        {
            var name = symbol.Name;
            if (name.Name == "_")
                return true;

            // if it is a shadow decl
            if (symbol is DeclSymbol declS && declS.Decl.SpecialKeys.Contains(TokenType.KwNew))
            {
                if (_shadowSymbolTable.ContainsKey(name))
                    return false;
                _shadowSymbolTable[name] = symbol;
                return true;
            }

            if (_symbolTable.ContainsKey(name))
                return false;

            _symbolTable[name] = symbol;
            return true;
        }

        private bool RemoveLocalSymbol(ISymbol symbol)
        {
            var name = symbol.Name;

            // if it is a shadow decl
            if (symbol is DeclSymbol declS && declS.Decl.SpecialKeys.Contains(TokenType.KwNew))
            {
                if (_shadowSymbolTable.ContainsKey(name))
                {
                    _shadowSymbolTable.Remove(name);
                    return true;
                }
                var tmp1 = _shadowSymbolTable.Keys.FirstOrDefault(x => x.Name == name.Name);
                if (tmp1 != null)
                {
                    _shadowSymbolTable.Remove(tmp1);
                    return true;
                }
            }

            if (_symbolTable.ContainsKey(name))
            {
                _symbolTable.Remove(name);
                return true;
            }
            var tmp2 = _symbolTable.Keys.FirstOrDefault(x => x.Name == name.Name);
            if (tmp2 != null)
            {
                _symbolTable.Remove(tmp2);
                return true;
            }

            return false;
        }

        public bool DefineSymbol(ISymbol symbol)
        {
            return DefineLocalSymbol(symbol);
        }

        private bool DefineTypeSymbol(AstIdExpr name, HapetType tp)
        {
            return DefineSymbol(new DeclSymbol(name, new AstBuiltInTypeDecl(tp)));
        }

        public bool DefineNamespaceSymbol(string name, Scope nsScope)
        {
            return DefineSymbol(new NamespaceSymbol(new AstIdExpr(name), nsScope));
        }

        public bool DefineDeclSymbol(AstIdExpr name, AstDeclaration decl)
        {
            var smbl = new DeclSymbol(name, decl);
            decl.Symbol = smbl;
            decl.Name.FindSymbol = smbl;
            return DefineSymbol(smbl);
        }

        public bool RemoveDeclSymbol(AstIdExpr name, AstDeclaration decl)
        {
            return RemoveLocalSymbol(new DeclSymbol(name, decl));
        }

        public ISymbol GetSymbol(
            AstIdExpr name, 
            bool searchUsedScopes = true, 
            bool searchParentScope = true, 
            bool searchPartNamespace = false,
            bool handleGenerics = false)
        {
            // this cringe is used to search a part namespace
            if (searchPartNamespace)
            {
                foreach (var k in _shadowSymbolTable)
                {
                    if (k.Value is NamespaceSymbol && k.Key.Name.StartsWith(name.Name))
                        return k.Value;
                }
                foreach (var k in _symbolTable)
                {
                    if (k.Value is NamespaceSymbol && k.Key.Name.StartsWith(name.Name))
                        return k.Value;
                }
                if (searchParentScope)
                    return Parent?.GetSymbol(name, searchUsedScopes, searchParentScope, searchPartNamespace, handleGenerics);
                return null;
            }

            // 1 - check for the same AstIdExpr instance
            if (_shadowSymbolTable.TryGetValue(name, out ISymbol value))
                return value;
            if (_symbolTable.TryGetValue(name, out ISymbol value2))
                return value2;

            // 2 - check for similar AstIdExpr
            if (name is not AstIdGenericExpr && name is not AstIdTupledExpr)
            {
                // checker to check that they are the same
                //var checker = new Func<AstIdExpr, bool>((x) =>
                //{
                //    var c1 = x is not AstIdGenericExpr && x is not AstIdTupledExpr;
                //    var c2 = x.Name == name.Name;
                //    // additional data shite
                //    var c3 = x.AdditionalData == null && name.AdditionalData == null;
                //    if (x.AdditionalData != null && name.AdditionalData != null)
                //    {
                //        c3 = x.AdditionalData.OutType == name.AdditionalData.OutType;
                //    }
                //    return c1 && c2 && c3;
                //});

                var k1 = _shadowSymbolTable.Keys.FirstOrDefault(x => x.Name == name.Name && (x is not AstIdGenericExpr && x is not AstIdTupledExpr) && 
                    ((x.AdditionalData == null && name.AdditionalData == null) || 
                    ((x.AdditionalData != null && name.AdditionalData != null) && 
                    x.AdditionalData.OutType == name.AdditionalData.OutType)));
                if (k1 != null)
                    return _shadowSymbolTable[k1];
                var k2 = _symbolTable.Keys.FirstOrDefault(x => x.Name == name.Name && (x is not AstIdGenericExpr && x is not AstIdTupledExpr) &&
                    ((x.AdditionalData == null && name.AdditionalData == null) ||
                    ((x.AdditionalData != null && name.AdditionalData != null) &&
                    x.AdditionalData.OutType == name.AdditionalData.OutType)));
                if (k2 != null)
                    return _symbolTable[k2];
            }

            // 3 - handle generics search
            if (handleGenerics && name is AstIdGenericExpr genId)
            {
                // WARN: if any changes here - also change PP.FuncInferenceHelper.Candidates_Step4_OnlyOneGeneric!!!

                // we need to store original decl 
                // if we won't find the same decl
                // we need to return original one
                DeclSymbol originalGeneric = null;
                DeclSymbol theSameGeneric = null;
                foreach (var k in _symbolTable.Keys.Where(x => x.Name == name.Name && x is AstIdGenericExpr))
                {
                    var genK = k as AstIdGenericExpr;

                    // if not the same amount of generics
                    int gAmountSymbol = genK.GenericRealTypes.Count;
                    int gAmountSearch = genId.GenericRealTypes.Count;
                    if (gAmountSymbol != gAmountSearch)
                        continue;

                    // if the same decl
                    bool allEqual = true;
                    for (int i = 0; i < gAmountSymbol; ++i)
                    {
                        if (genK.GenericRealTypes[i].OutType == genId.GenericRealTypes[i].OutType)
                            continue;
                        allEqual = false;
                        break;
                    }
                    if (allEqual)
                    {
                        theSameGeneric = _symbolTable[k] as DeclSymbol;
                        continue;
                    }

                    // if original generic
                    if (_symbolTable[k] is DeclSymbol ds &&
                        ds.Decl.HasGenericTypes &&
                        !ds.Decl.IsImplOfGeneric)
                    {
                        originalGeneric = _symbolTable[k] as DeclSymbol;
                        continue;
                    }
                }

                // try at first to return the same
                if (theSameGeneric != null)
                    return theSameGeneric;
                // then try to return original generic
                if (originalGeneric != null)
                    return originalGeneric;
            }

            // 4 - search in parent
            if (searchParentScope)
                return Parent?.GetSymbol(name, searchUsedScopes, searchParentScope, searchPartNamespace, handleGenerics);
            return null;
        }

        // to get decl in namespace
        public DeclSymbol GetSymbolInNamespace(string ns, AstIdExpr symbol, bool searchUsedScopes = true, bool searchParentScope = true, bool handleGenerics = false)
        {
            NamespaceSymbol nsSymbol = GetSymbol(new AstIdExpr(ns), searchUsedScopes, searchParentScope) as NamespaceSymbol;
            if (nsSymbol == null)
                return null;

            return nsSymbol.Scope.GetSymbol(symbol.GetCopy($"{ns}.{symbol.Name}"), searchUsedScopes, searchParentScope, handleGenerics: handleGenerics) as DeclSymbol;
        }

        public bool IsStringNamespaceOrPart(string testString, bool searchUsedScopes = true, bool searchParentScope = true)
        {
            NamespaceSymbol nsSymbol = GetSymbol(new AstIdExpr(testString), searchUsedScopes, searchParentScope) as NamespaceSymbol;
            if (nsSymbol != null)
                return true;

            // it is probably a part
            NamespaceSymbol nsSymbolPart = GetSymbol(new AstIdExpr(testString), searchUsedScopes, searchParentScope, true) as NamespaceSymbol;
            if (nsSymbolPart != null)
                return true;

            return false;
        }

        public List<DeclSymbol> GetFunctionSymbols(AstIdExpr classWithFuncName, bool searchParent = false, bool callFromObject = false)
        {
            List<DeclSymbol> candidates = new List<DeclSymbol>();
            // search for the func in the scope
            foreach (var (k, d) in GetDecls())
            {
                var decl = d as DeclSymbol;

                // 1 - name similarity checks - DO NOT ALLOW GENERICS HERE 
                // generic checks are below
                var onlyFuncName = classWithFuncName.Name;
                var firstKeyPart = k.Name;
                if ((firstKeyPart == onlyFuncName)
                    && (decl.Decl.Name is not AstIdGenericExpr && classWithFuncName is not AstIdGenericExpr))
                {
                    // add static func if not callFromObject and add non-static if callFromObject
                    // or just add if it is a delegate
                    if ((callFromObject && !decl.Decl.SpecialKeys.Contains(TokenType.KwStatic)) ||
                        (!callFromObject && decl.Decl.SpecialKeys.Contains(TokenType.KwStatic)) ||
                        decl.Decl.Type.OutType is DelegateType)
                        candidates.Add(decl);
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
                        if ((callFromObject && !decl.Decl.SpecialKeys.Contains(TokenType.KwStatic)) ||
                            (!callFromObject && decl.Decl.SpecialKeys.Contains(TokenType.KwStatic)) ||
                            decl.Decl.Type.OutType is DelegateType)
                            candidates.Add(decl);
                        continue;
                    }
                }

                // 3 - explicit interface impl check
                /// the same as in <see cref="OtherExtensions.GetSameByNameAndTypes(List{AstFuncDecl}, AstFuncDecl, out int, bool)"/>
                if (decl.Decl.Name.AdditionalData != null)
                {
                    string pureSearchName = onlyFuncName;
                    string pureName = firstKeyPart;
                    if (pureName == pureSearchName)
                    {
                        // add static func if not callFromObject and add non-static if callFromObject
                        // or just add if it is a delegate
                        if ((callFromObject && !decl.Decl.SpecialKeys.Contains(TokenType.KwStatic)) ||
                            (!callFromObject && decl.Decl.SpecialKeys.Contains(TokenType.KwStatic)) ||
                            decl.Decl.Type.OutType is DelegateType)
                            candidates.Add(decl);
                        continue;
                    }
                }
            }

            // if search in parent scopes
            if (searchParent && Parent != null)
                candidates.AddRange(Parent.GetFunctionSymbols(classWithFuncName, searchParent, callFromObject));

            return candidates;

            IEnumerable<KeyValuePair<AstIdExpr, ISymbol>> GetDecls()
            {
                // search for the func in the shadow
                foreach (var k in ShadowSymbolTable.Where(x => x.Value is DeclSymbol ds && (ds.Decl is AstFuncDecl || ds.Decl is AstDelegateDecl)))
                {
                    yield return k;
                }
                // search for the func in the scope
                foreach (var k in SymbolTable.Where(x => x.Value is DeclSymbol ds && (ds.Decl is AstFuncDecl || ds.Decl is AstDelegateDecl)))
                {
                    yield return k;
                }
            }
        }
    }
}
