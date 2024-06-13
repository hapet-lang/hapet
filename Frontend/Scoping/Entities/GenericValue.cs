using Frontend.Parsing.Entities;

namespace Frontend.Scoping.Entities
{
	/// <summary>
	/// This is like int in "new List<int>() ..."
	/// </summary>
	public class GenericValue : ISymbol
	{
		public string Name { get; set; }
		public ILocation Location { get; }

		public GenericValue(string name)
		{
			Name = name;
		}
	}
}
