using Frontend.Parsing.Entities;

namespace Frontend
{
	public interface ITextProvider
	{
		string GetText(ILocation location);
	}

	public class Compiler : ITextProvider
	{
		public string GetText(ILocation location)
		{
			throw new NotImplementedException();
		}
	}
}
