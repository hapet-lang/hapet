using Frontend.Parsing.Entities;

namespace Frontend.Scoping.Entities
{
	public class AmbiguousSymbol : ISymbol
	{
		public string Name => throw new NotImplementedException();
		public ILocation Location => throw new NotImplementedException();

		public List<ISymbol> Symbols { get; }

		public AmbiguousSymbol(List<ISymbol> syms)
		{
			Symbols = syms;
		}
	}
}
