namespace HapetFrontend.Types
{
    public class TypeContext
    {
        public int PointerSize { get; set; }
        public StringType StringTypeInstance { get; set; }
        public Dictionary<HapetType, ArrayType> ArrayTypeInstances { get; set; } = new Dictionary<HapetType, ArrayType>();
    }
}
