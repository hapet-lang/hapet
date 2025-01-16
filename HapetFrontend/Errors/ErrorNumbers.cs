namespace HapetFrontend.Errors
{
    // Compile Time Error Number
    public enum CTEN
    {
        Teapot                          = 0x0000,

        // project errors up to 0x1000
        FullPathToHapetFileNotFound     = 0x0001,
        ProjectFileCouldNotBeParsed     = 0x0002,
        UnexpectedProjectFileTag        = 0x0003,

        // lexer errors up to 0x2000
        FileForLexerNotFound            = 0x1001,
        // parser errors up to 0x3000

        // post preparer errors up to 0x5000
        UnexpectedDeclInStruct = 0x3001,
    }

    // Run Time Error Number
    public enum RTEN
    {
        Teapot = 0x0000,

    }

    // Compile Time Warning Number
    public enum CTWN
    {
        Teapot = 0x0000,

    }
}
