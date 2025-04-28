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
        public bool DefineLocalSymbol(ISymbol symbol)
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

        public bool RemoveLocalSymbol(ISymbol symbol)
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

            if (!_symbolTable.ContainsKey(name))
                return false;

            _symbolTable.Remove(name);
            return true;
        }

        public bool DefineSymbol(ISymbol symbol)
        {
            return DefineLocalSymbol(symbol);
        }

        private bool DefineTypeSymbol(AstIdExpr name, HapetType tp)
        {
            return DefineSymbol(new DeclSymbol(name, new AstBuiltInTypeDecl(tp)));
        }

        public bool DefineNamespaceSymbol(AstIdExpr name, Scope nsScope)
        {
            return DefineSymbol(new NamespaceSymbol(name, nsScope));
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
                foreach (var k in _symbolTable.Keys)
                {
                    // skip non generic keys
                    if (k is not AstIdGenericExpr genK)
                        continue;

                    int gAmountSymbol = genK.GenericRealTypes.Count;
                    int gAmountSearch = genId.GenericRealTypes.Count;

                    if (gAmountSymbol == gAmountSearch && 
                        k.Name == name.Name &&
                        _symbolTable[k] is DeclSymbol ds &&
                        ds.Decl.HasGenericTypes) // only pure generics are allowed
                    {
                        return _symbolTable[k];
                    }
                }
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
