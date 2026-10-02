namespace HapetFrontend.Syntax
{
    public enum SyntaxKind : short
    {
        /// <summary>
        /// These are for lexer tokens
        /// </summary>
        
        Unknown,

        Whitespace,    // trivia shite
        NewLine,
        EOF,

        DocComment,

        StringLiteral,
        CharLiteral,
        NumberLiteral,

        Identifier,
        DollarIdentifier,// $
        SharpIdentifier, // #
        AtSignIdentifier,// @

        Tilda,           // ~
        Semicolon,       // ;
        Colon,           // :
        Comma,           // ,
        Period,          // .
        PeriodPeriod,    // ..
        Equal,           // =
        Ampersand,       // &
        Hat,             // ^
        Bang,            // !
        QuestionMark,    // ?
        DoubleQuestion,  // ??
        VerticalSlash,   // |

        Plus,            // +
        Minus,           // -
        Asterisk,        // *
        ForwardSlash,    // /
        Percent,         // %

        PlusPlus,        // ++
        MinusMinus,      // --

        LessLess,        // <<
        GreaterGreater,  // >>
        GreaterGreaterGreater,  // >>>

        AddEq,           // +=
        SubEq,           // -=
        MulEq,           // *=
        DivEq,           // /=
        ModEq,           // %=
        HatEq,           // ^=
        AmpEq,           // &=
        PipeEq,          // |=
        CoalesceEq,      // ??=

        Less,            // <
        LessEqual,       // <=
        Greater,         // >
        GreaterEqual,    // >=
        DoubleEqual,     // ==
        NotEqual,        // !=

        LogicalOr,       // ||
        LogicalAnd,      // &&

        Arrow,           // =>

        OpenParen,       // (
        CloseParen,      // )
        OpenBrace,       // {
        CloseBrace,      // }
        OpenBracket,     // [
        CloseBracket,    // ]

        ArrayDef,        // []

        // words
        KwStruct,
        KwEnum,
        KwInterface,
        KwClass,
        KwDelegate,

        KwIf,
        KwElse,
        KwSwitch,
        KwCase,
        KwFor,
        KwForeach, // would be used anywhere?
        KwWhile,
        KwDo,
        KwGoto,

        // exceptions
        KwTry,
        KwCatch,
        KwFinally,
        KwThrow,

        KwLock,
        KwChecked,
        KwUnchecked,

        KwTrue,
        KwFalse,
        KwNull,

        KwUsing,
        KwNamespace,

        KwBreak,
        KwContinue,
        KwReturn,
        KwYield,

        KwConst,
        KwReadonly,
        KwUnsafe,
        KwVolatile, // would be used anywhere?
        KwGlobal, // would be used anywhere?
        KwDefault,
        KwNew,
        KwStackAlloc,
        KwBase,
        KwSizeof,
        KwAlignof,
        KwTypeof,
        KwNameof,

        KwGet, // 'get' in properties
        KwSet, // 'set' in properties

        KwIn,
        KwIs,
        KwAs,
        KwRef,
        KwOut,
        KwParams,
        KwArglist,
        KwWhere,

        KwPublic,
        KwInternal,
        KwProtected,
        KwPrivate,
        KwUnreflected,

        KwAsync,
        KwAwait,

        KwStatic,
        KwAbstract,
        KwVirtual,
        KwOverride,
        KwPartial,
        KwExtern,
        KwSealed,
        KwInline,
        KwNoexcept,

        // for events
        KwEvent,
        KwAdd,
        KwRemove,

        // for overriding casts
        KwExplicit,
        KwImplicit,
        // for overriding operators
        KwOperator,


        /// <summary>
        /// These are for parser nodes
        /// </summary>
        

        // expressions
        AddressOfExpr = 1000,
        ArgumentExpr,
        ArrayAccessExpr,
        ArrayCreateExpr,
        ArrayExpr,
        BinaryExpr,
        BoolExpr,
        CallExpr,
        CastExpr,
        CharExpr,
        CheckedExpr,
        DefaultExpr,
        DefaultGenericExpr,   // when kw 'default' is used inside generic type
        IdentifierExpr,
        IdentifierGenericExpr,
        IdentifierTupledExpr, // for cringe like 'var a, b, c = FuncThatReturnsTuple();'
        InvokationExpr,       // a.b(), (Expression = a.b, ArgumentList = ())
        MemberAccessExpr,     // a.b
        NewExpr,
        NullableExpr,
        NumberExpr,
        ParenthesizedExpr,    // (expr), to keep parens
        PointerExpr,
        SATOfExpr,            // sizeof, nameof, alignof, typeof
        StringExpr,
        SwitchExpr,
        TernaryExpr,
        TupleExpr,
        UnaryPrefixExpr,
        UnaryPostfixExpr,

        // declarations
        ClassDecl = 2000,

    }
}
