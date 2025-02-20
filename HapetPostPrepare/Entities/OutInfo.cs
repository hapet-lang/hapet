namespace HapetPostPrepare.Entities
{
    public class OutInfo
    {
        public bool ItWasProperty { get; set; }
        public bool ItWasIndexer { get; set; }

        public static OutInfo Default => new OutInfo()
        {
            ItWasProperty = false,
            ItWasIndexer = false,
        };
    }
}
