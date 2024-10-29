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

		public ISymbol GetSymbol(string name, bool searchUsedScopes = true, bool searchParentScope = true)
        {
            if (_symbolTable.ContainsKey(name))
            {
                var v = _symbolTable[name];
                return v;
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
                return Parent?.GetSymbol(name);
            return null;
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
