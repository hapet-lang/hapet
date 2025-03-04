namespace HapetFrontend.Entities
{
    public class ParserOutInfo
    {
        public static ParserOutInfo Default => new ParserOutInfo()
        {
            ItWasProperty = false,
            ItWasIndexer = false,
        };
    }
}
