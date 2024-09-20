using HapetFrontend.Ast;
using HapetFrontend.Entities;
using HapetFrontend.Types;

namespace HapetFrontend.Scoping
{
	public interface ISymbol
	{
		string Name { get; }
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
}
