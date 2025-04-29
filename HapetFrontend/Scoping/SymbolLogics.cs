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
            return DefineSymbol(new DeclSymbol(name, decl));
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
                foreach (var k in _shadowSymbolTable.Keys)
                {
                    if (k.Name.StartsWith(name.Name) && _shadowSymbolTable[k] is NamespaceSymbol)
                        return _shadowSymbolTable[k];
                }
                foreach (var k in _symbolTable.Keys)
                {
                    if (k.Name.StartsWith(name.Name) && _symbolTable[k] is NamespaceSymbol)
                        return _symbolTable[k];
                }
            }

            // 1 - check for the same AstIdExpr instance
            if (_shadowSymbolTable.TryGetValue(name, out ISymbol value))
                return value;
            if (_symbolTable.TryGetValue(name, out ISymbol value2))
                return value2;

            // 2 - check for similar AstIdExpr
            if (name is not AstIdGenericExpr && name is not AstIdTupledExpr)
            {
                var k1 = _shadowSymbolTable.Keys.FirstOrDefault(x => x is not AstIdGenericExpr && x is not AstIdTupledExpr && x.Name == name.Name);
                if (k1 != null)
                    return _shadowSymbolTable[k1];
                var k2 = _symbolTable.Keys.FirstOrDefault(x => x is not AstIdGenericExpr && x is not AstIdTupledExpr && x.Name == name.Name);
                if (k2 != null)
                    return _symbolTable[k2];
            }

            // 3 - handle generics search
            if (handleGenerics && name is AstIdGenericExpr genId)
            {
                // we need to store original decl 
                // if we won't find the same decl
                // we need to return original one
                DeclSymbol originalGeneric = null;
                DeclSymbol theSameGeneric = null;
                foreach (var k in _symbolTable.Keys)
                {
                    // skip non generic keys
                    if (k is not AstIdGenericExpr genK)
                        continue;

                    // if not the same name
                    if (k.Name != name.Name)
                        continue;

                    // if not the same amount of generics
                    int gAmountSymbol = genK.GenericRealTypes.Count;
                    int gAmountSearch = genId.GenericRealTypes.Count;
                    if (gAmountSymbol != gAmountSearch)
                        continue;

                    // if the same decl
                    bool allEqual = true;
                    for (int i = 0; i < gAmountSymbol; ++i)
                    {
                        if (genK.GenericRealTypes[i].OutType != genId.GenericRealTypes[i].OutType)
                        {
                            allEqual = false;
                            break;
                        }
                    }
                    if (_symbolTable[k] is DeclSymbol ds1 && 
                        ds1.Decl.IsImplOfGeneric &&
                        allEqual)
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
    }
}
