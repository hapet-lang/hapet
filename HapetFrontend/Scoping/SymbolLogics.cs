using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Types;

namespace HapetFrontend.Scoping
{
    public partial class Scope
    {
		public interface ISymbol
		{
			string Name { get; }
		}

		public interface ITypedSymbol : ISymbol
		{
			HapetType Type { get; }
		}

		/// <summary>
		/// To search for a type in a scope
		/// </summary>
		public class TypeSymbol : ITypedSymbol
		{
			public string Name { get; private set; }
			public HapetType Type { get; private set; }

			public TypeSymbol(string name, HapetType type)
			{
				this.Name = name;
				this.Type = type;
			}
		}

		/// <summary>
		/// To search for a module in a global scope
		/// </summary>
		public class ModuleSymbol : ISymbol
		{
			public string Name { get; private set; }
			public ProgramFile File { get; private set; }

			public ModuleSymbol(string name, ProgramFile file)
			{
				this.Name = name;
				this.File = file;
			}
		}

		/// <summary>
		/// To search for a declaration in a global scope. Use it for local vars, params and other
		/// </summary>
		public class DeclSymbol : ISymbol
		{
			public string Name { get; private set; }
			public AstDeclaration Decl { get; private set; }

			public DeclSymbol(string name, AstDeclaration decl)
			{
				this.Name = name;
				this.Decl = decl;
			}
		}

		/// <summary>
		/// If a symbol found twice
		/// </summary>
		public class AmbiguousSymol : ISymbol
		{
			public string Name => throw new NotImplementedException();
			public ILocation Location => throw new NotImplementedException();

			public List<ISymbol> Symbols { get; }

			public AmbiguousSymol(List<ISymbol> syms)
			{
				Symbols = syms;
			}
		}

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

		public bool DefineModuleSymbol(string name, ProgramFile file)
		{
			return DefineSymbol(new ModuleSymbol(name, file));
		}

		public bool DefineDeclSymbol(string name, AstDeclaration decl)
		{
			return DefineSymbol(new DeclSymbol(name, decl));
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
