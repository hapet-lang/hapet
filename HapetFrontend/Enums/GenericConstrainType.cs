namespace HapetFrontend.Enums
{
    public enum GenericConstrainType
    {
        None = 0,           // for what?
        CustomType = 1,     // it is custom user types like 'IAnime'
        NewType = 2,        // it is like 'new()'
        ClassType = 3,      // it is like 'class'
        StructType = 4,     // it is like 'struct'
        DelegateType = 5,   // it is like 'delegate'
        EnumType = 6,       // it is like 'enum'
    }
}
