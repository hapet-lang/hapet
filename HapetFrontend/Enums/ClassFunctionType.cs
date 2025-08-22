namespace HapetFrontend.Enums
{
    public enum ClassFunctionType
    {
        Special = -1, // only handled in parsing
        Default = 0,
        Initializer = 1,
        Ctor = 2,
        Dtor = 3,
        StaticCtor = 4,
        StorCaller = 5,
    }
}
