namespace HapetFrontend.Enums
{
    public enum DirectiveType
    {
        None = 0, // redunant, probably not used
        MetadataFile = 1, // #file directive in .mpt file
        MetadataNamespace = 2, // #namespace directive in .mpt file
    }
}
