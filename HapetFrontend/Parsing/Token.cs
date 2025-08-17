using HapetFrontend.Ast;

namespace HapetFrontend.Parsing
{
    public class Token
    {
        public TokenType Type { get; set; }
        public TokenLocation Location { get; set; }
        public object Data { get; set; }

        /// <summary>
        /// Like 0x 0b $ @
        /// </summary>
        public string Suffix { get; set; }

        public override string ToString()
        {
            return $"({Location.Line}:{Location.Index - Location.LineStartIndex}) ({Type}) {Data}";
        }
    }

    public enum TokenType : short
    {
        Unknown,

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

        AddEq,           // +=
        SubEq,           // -=
        MulEq,           // *=
        DivEq,           // /=
        ModEq,           // %=
        HatEq,           // ^=
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
        KwGoto, // would be used anywhere?

        // exceptions
        KwTry,
        KwCatch,
        KwFinally,
        KwThrow,

        KwLock,
        KwChecked, // would be used anywhere?
        KwUnchecked, // would be used anywhere?

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
        KwRef, // would be used anywhere?
        KwOut, // would be used anywhere?
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
    }
}
