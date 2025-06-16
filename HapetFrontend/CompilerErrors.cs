namespace HapetFrontend
{
    // biggest now - 9
    public enum CompilerErrors
    {
        Ok = 0,
        ParsingError = 1,
        PostPrepareError = 2,
        LastPrepareError = 9,
        PostPrepareMetafileError = 7,
        CodeGenerationError = 3,
        ProjectFileParseError = 4,
        ProjectReferencesError = 8,

        HapetCommandError = 5,
        HapetCommandParamsError = 6,
    }
}
