using HapetFrontend.Ast.Declarations;
using HapetFrontend.Types;

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

        public bool DefineTypeSymbol(string name, HapetType symbol)
        {
            return DefineSymbol(new TypeSymbol(name, symbol));
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
