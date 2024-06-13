using Frontend.Parsing.Entities;
using Frontend.Types;

namespace Frontend.Scoping.Entities
{
	public interface ISymbol
	{
		public string Name { get; }
		public ILocation Location { get; }
	}

	public interface ITypedSymbol : ISymbol
	{
		public HapetType Type { get; }
	}
}
