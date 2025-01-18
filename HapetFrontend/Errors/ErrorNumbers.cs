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
        UnexpectedDeclInStruct          = 0x2001,
        ExprsExpectedInBinExpr          = 0x2002,
        ExprExpectedInUnExpr            = 0x2003,
        ArgumentNameNotIdent            = 0x2004,
        FailedToParseArguments          = 0x2005,
        ParameterNameNotIdent           = 0x2006,
        ParamDefaultNotExpr             = 0x2007,
        FailedToParseParameters         = 0x2008,
        LambdaParamNameNotIdent         = 0x2009,
        CastSubNotExpr                  = 0x200A,
        CastTargetNotExpr               = 0x200B,
        ArraySizeNotExpr                = 0x200C,
        ArraySizeNotSpecified           = 0x200D,
        ArrayNonLastNotSpecified        = 0x200E,
        ArrayUnexpectedToken            = 0x200F,
        ArrayElementNotExpr             = 0x2010,
        ArrayElementsUnexpectedToken    = 0x2011,
        CallArgExprExpected             = 0x2012,
        CallNameIdentExpected           = 0x2013, // would be obsolete

        // post preparer errors up to 0x5000

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

        // project warnings up to 0x1000

        // lexer warnings up to 0x2000

        // parser warnings up to 0x3000
        ArrayEmptyCreation              = 0x2001,

        // post preparer warnings up to 0x5000
    }
}
