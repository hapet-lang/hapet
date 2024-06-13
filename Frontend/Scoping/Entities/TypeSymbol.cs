using Frontend.Parsing.Entities;
using Frontend.Types;

namespace Frontend.Scoping.Entities
{
	public class TypeSymbol : ITypedSymbol
	{
		public ILocation Location => throw new NotImplementedException();
		public string Name { get; private set; }

		public HapetType Type { get; private set; }

		public TypeSymbol(string name, HapetType type)
		{
			this.Name = name;
			this.Type = type;
		}
	}
}
