namespace HapetFrontend.Enums
{
    public enum DirectiveType
    {
        None = 0, // redunant, probably not used
        MetadataFile = 1, // #file directive in .mpt file
        MetadataMeta = 2, // #meta that contains json props like version, deps and other
        MetadataEndMeta = 3, // #endmeta 
    }
}
