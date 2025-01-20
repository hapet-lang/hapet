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
        ProjectFileException            = 0x0004,

        // lexer errors up to 0x2000
        FileForLexerNotFound            = 0x1001,
        UnexpectedEndOfStringLit        = 0x1002,

        // parser errors up to 0x3000
        UnexpectedDeclInStruct          = 0x2001,
        ExprsExpectedInBinExpr          = 0x2002,
        ExprsExpectedInBinExprR         = 0x201E,
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
        CallTargetExprExpected          = 0x2012,
        CallNameIdentExpected           = 0x2013, // would be obsolete
        ArrayAccUnexpectedToken         = 0x2014,
        ArrayAccNoArgs                  = 0x2015,
        ArrayAccTooManyArgs             = 0x2016, // could be obsolete
        ArrayAccNotExpr                 = 0x2017,
        DeclNameIsNotIdent              = 0x2018,
        TildaUnexpectedExpr             = 0x2019, // would be obsolete
        CommonFailToParse               = 0x201A,
        CommonExpectedToken             = 0x201B,
        CommonUnexpectedToken           = 0x201C,
        CommonUnexpectedInExpr          = 0x201D,
        CommonIdentifierExpected        = 0x201F,
        CommonDotUnexpected             = 0x2020,
        CommonIdentAfterDot             = 0x2021,
        VarIniterExpr                   = 0x2022,
        PureUnexpectedToken             = 0x2023,
        StmtNotAllowedInGlobal          = 0x2024,

        // post preparer errors up to 0x5000
        EnumCouldNotBeAssigned          = 0x3001,
        RequiredTypeNotEvaluated        = 0x3002,
        ArrayVarSizeAndVals             = 0x3003,
        ArraySizeAndValsDiffer          = 0x3004,
        ArrayTypeAsElement              = 0x3005,
        ClassStaticMemStatic            = 0x3006,
        ClassFuncGetSetName             = 0x3007,
        ClassPropFieldExists            = 0x3008,
        ClassPropsFieldsSame            = 0x3009,
        ClassFuncNameNotAllowed         = 0x300A,
        ClassDtorOnlyOne                = 0x300B,
        ClassStorOnlyOne                = 0x300C,
        StmtExpectedToBeDecl            = 0x300D,
        DeclNotAllowedInNamespace       = 0x300E,
        NamespaceAlreadyContains        = 0x300F,
        EnumTypeNotInt                  = 0x3010,
        FieldAlreadyDefined             = 0x3011,
        DoubleInterfaceCringe           = 0x3012, // would be obsolete
        NoFieldImplementation           = 0x3013,
        EnumIniNotComptime              = 0x3014,
        EnumIniNotNumber                = 0x3015,
        VarVarNoIniter                  = 0x3016,
        VarVoidType                     = 0x3017,
        VarTokenNotAllowed              = 0x3018,
        ConstValueNonComptime           = 0x3019,
        UndefOpForType                  = 0x301A,
        TooManyOpsForType               = 0x301B,
        BinUndefOpForTypes              = 0x301C,
        BinTooManyOpsForTypes           = 0x301D,
        OpUsedWithVoidPtr               = 0x301E,
        CreateInterfOrAbsCls            = 0x301F,
        DeclCouldNotBeAccessed          = 0x3020,
        TypeCouldNotBeInfered           = 0x3021,
        FuncCouldNotBeAccessed          = 0x3022,
        FuncWithNameNotFound            = 0x3023,
        NonStaticFuncFromStatic         = 0x3024,
        FuncCallNotInfered              = 0x3025,
        CallNotFuncOrDelegate           = 0x3026,

        // compiler errors up to 0x6000
        DefaultWasNotInfered            = 0x5001,
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
        StmtsWouldBeIgnored             = 0x2002,

        // post preparer warnings up to 0x5000
        StaticCtorKwsIgnored            = 0x3001,
        EnumHasSameValue                = 0x3002,
        StaticFuncFromObject            = 0x3003,
    }
}
