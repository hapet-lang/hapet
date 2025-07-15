namespace HapetFrontend.Enums
{
    public enum DirectiveType
    {
        None = 0, // redunant, probably not used
        MetadataFile = 1, // #file directive in .mpt file
        MetadataMeta = 2, // #meta that contains json props like version, deps and other
        MetadataEndMeta = 3, // #endmeta 

        If = 4,
        Elif = 5,
        Else = 6,
        EndIf = 7,

        Define = 8,
        Undef = 9,

        Error = 10,
        Warning = 11,
    }
}
