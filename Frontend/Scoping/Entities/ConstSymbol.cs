using Frontend.Parsing.Entities;
using Frontend.Types;

namespace Frontend.Scoping.Entities
{
	public class ConstSymbol : ITypedSymbol
	{
		public ILocation Location => throw new NotImplementedException();
		public string Name { get; private set; }

		public HapetType Type { get; private set; }
		public object Value { get; private set; }

		public ConstSymbol(string name, HapetType type, object value)
		{
			this.Name = name;
			this.Type = type;
			this.Value = value;
		}
	}
}
