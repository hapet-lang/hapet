using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Statements;
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
    }
}
