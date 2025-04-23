using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
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
        public bool DefineLocalSymbol(ISymbol symbol, string name = null)
        {
            name ??= symbol.Name;
            if (name == "_")
                return true;

            // if it is a shadow decl
            if (symbol is DeclSymbol declS && declS.Decl.SpecialKeys.Contains(TokenType.KwNew))
            {
                if (_shadowSymbolTable.TryGetValue(name, out var otherShadow))
                    return false;
                _shadowSymbolTable[name] = symbol;
                return true;
            }

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

            // if it is a shadow decl
            if (symbol is DeclSymbol declS && declS.Decl.SpecialKeys.Contains(TokenType.KwNew))
            {
                if (_shadowSymbolTable.TryGetValue(name, out var otherShadow))
                    return false;
                _shadowSymbolTable[name] = symbol;
                return true;
            }

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

        public ISymbol GetSymbol(
            string name, 
            bool searchUsedScopes = true, 
            bool searchParentScope = true, 
            bool searchPartNamespace = false,
            bool handleGenerics = false)
        {
            if (_shadowSymbolTable.ContainsKey(name))
            {
                var v = _shadowSymbolTable[name];
                return v;
            }

            if (_symbolTable.ContainsKey(name))
            {
                var v = _symbolTable[name];
                return v;
            }

            // this cringe is used to search a part namespace
            if (searchPartNamespace)
            {
                foreach (var k in _shadowSymbolTable.Keys)
                {
                    if (k.StartsWith(name) && _shadowSymbolTable[k] is NamespaceSymbol)
                    {
                        return _shadowSymbolTable[k];
                    }
                }
                foreach (var k in _symbolTable.Keys)
                {
                    if (k.StartsWith(name) && _symbolTable[k] is NamespaceSymbol)
                    {
                        return _symbolTable[k];
                    }
                }
            }

            // handle generics search
            if (handleGenerics && name.Contains(GenericsHelper.GENERIC_BEGIN))
            {
                foreach (var k in _symbolTable.Keys)
                {
                    // skip non generic keys
                    if (!k.Contains(GenericsHelper.GENERIC_BEGIN))
                        continue;

                    string symbolWoGenerics = k.Substring(0, k.IndexOf(GenericsHelper.GENERIC_BEGIN));
                    string searchWoGenerics = name.Substring(0, name.IndexOf(GenericsHelper.GENERIC_BEGIN));

                    string pureSymbolNs = symbolWoGenerics.GetNamespaceWithoutClassName();
                    string pureSearchNs = searchWoGenerics.GetNamespaceWithoutClassName();

                    string pureSymbolName = symbolWoGenerics.GetClassNameWithoutNamespace();
                    string pureSearchName = searchWoGenerics.GetClassNameWithoutNamespace();

                    int gAmountSymbol = k.GetGenericsAmount();
                    int gAmountSearch = name.GetGenericsAmount();

                    if (gAmountSymbol == gAmountSearch && 
                        pureSymbolName == pureSearchName &&
                        pureSymbolNs == pureSearchNs &&
                        _symbolTable[k] is DeclSymbol)
                    {
                        return _symbolTable[k];
                    }
                }
            }

            if (searchParentScope)
                return Parent?.GetSymbol(name, searchUsedScopes, searchParentScope, searchPartNamespace, handleGenerics);
            return null;
        }

        // to get decl in namespace
        public DeclSymbol GetSymbolInNamespace(string ns, string symbol, bool searchUsedScopes = true, bool searchParentScope = true, bool handleGenerics = false)
        {
            NamespaceSymbol nsSymbol = GetSymbol(ns, searchUsedScopes, searchParentScope) as NamespaceSymbol;
            if (nsSymbol == null)
                return null;

            return nsSymbol.Scope.GetSymbol($"{ns}.{symbol}", searchUsedScopes, searchParentScope, handleGenerics) as DeclSymbol;
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
    }
}
