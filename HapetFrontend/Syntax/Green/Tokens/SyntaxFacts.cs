namespace HapetFrontend.Syntax.Green.Tokens
{
    internal static class SyntaxFacts
    {
        public static bool IsLiteral(SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.StringLiteral:
                case SyntaxKind.CharLiteral:
                case SyntaxKind.NumberLiteral:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsAnyToken(SyntaxKind kind)
        {
            if (kind >= SyntaxKind.FirstTokenWithWellKnownText &&
                kind <= SyntaxKind.LastTokenWithWellKnownText)
            {
                return true;
            }

            switch (kind)
            {
                case SyntaxKind.EOF:
                case SyntaxKind.Unknown:
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsTrivia(SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.EOF:
                case SyntaxKind.Whitespace:
                case SyntaxKind.NewLine:
                case SyntaxKind.DocComment:
                    return true;
                default:
                    return false;
            }
        }

        public static string GetText(SyntaxKind kind)
        {
            return kind switch
            {
                SyntaxKind.DollarIdentifier => "$",// $
                SyntaxKind.SharpIdentifier => "#", // #
                SyntaxKind.AtSignIdentifier => "@",// @

                SyntaxKind.Tilda => "~",           // ~
                SyntaxKind.Semicolon => ";",       // ;
                SyntaxKind.Colon => ":",           // :
                SyntaxKind.Comma => ",",           // ,
                SyntaxKind.Period => ".",          // .
                SyntaxKind.PeriodPeriod => "..",    // ..
                SyntaxKind.Equal => "=",           // =
                SyntaxKind.Ampersand => "&",       // &
                SyntaxKind.Hat => "^",             // ^
                SyntaxKind.Bang => "!",            // !
                SyntaxKind.QuestionMark => "?",    // ?
                SyntaxKind.DoubleQuestion => "??",  // ??
                SyntaxKind.VerticalSlash => "|",   // |

                SyntaxKind.Plus => "+",            // +
                SyntaxKind.Minus => "-",           // -
                SyntaxKind.Asterisk => "*",        // *
                SyntaxKind.ForwardSlash => "/",    // /
                SyntaxKind.Percent => "%",         // %

                SyntaxKind.PlusPlus => "++",        // ++
                SyntaxKind.MinusMinus => "--",      // --

                SyntaxKind.LessLess => "<<",        // <<
                SyntaxKind.GreaterGreater => ">>",  // >>
                SyntaxKind.GreaterGreaterGreater => ">>>",  // >>>

                SyntaxKind.AddEq => "+=",           // +=
                SyntaxKind.SubEq => "-=",           // -=
                SyntaxKind.MulEq => "*=",           // *=
                SyntaxKind.DivEq => "/=",           // /=
                SyntaxKind.ModEq => "%=",           // %=
                SyntaxKind.HatEq => "^=",           // ^=
                SyntaxKind.AmpEq => "&=",           // &=
                SyntaxKind.PipeEq => "|=",          // |=
                SyntaxKind.CoalesceEq => "??=",      // ??=

                SyntaxKind.Less => "<",            // <
                SyntaxKind.LessEqual => "<=",       // <=
                SyntaxKind.Greater => ">",         // >
                SyntaxKind.GreaterEqual => ">=",    // >=
                SyntaxKind.DoubleEqual => "==",     // ==
                SyntaxKind.NotEqual => "!=",        // !=

                SyntaxKind.LogicalOr => "||",       // ||
                SyntaxKind.LogicalAnd => "&&",      // &&

                SyntaxKind.Arrow => "=>",           // =>

                SyntaxKind.OpenParen => "(",       // (
                SyntaxKind.CloseParen => ")",      // )
                SyntaxKind.OpenBrace => "{",       // {
                SyntaxKind.CloseBrace => "}",      // }
                SyntaxKind.OpenBracket => "[",     // [
                SyntaxKind.CloseBracket => "]",    // ]

                SyntaxKind.ArrayDef => "[]",        // []

                // words and other

                SyntaxKind.KwStruct => "struct",
                SyntaxKind.KwEnum => "enum",
                SyntaxKind.KwInterface => "interface",
                SyntaxKind.KwClass => "class",
                SyntaxKind.KwDelegate => "delegate",

                SyntaxKind.KwIf => "if",
                SyntaxKind.KwElse => "else",
                SyntaxKind.KwSwitch => "switch",
                SyntaxKind.KwCase => "case",
                SyntaxKind.KwFor => "for",
                SyntaxKind.KwForeach => "foreach", // would be used anywhere?
                SyntaxKind.KwWhile => "while",
                SyntaxKind.KwDo => "do",
                SyntaxKind.KwGoto => "goto",

                // exceptions
                SyntaxKind.KwTry => "try",
                SyntaxKind.KwCatch => "catch",
                SyntaxKind.KwFinally => "finally",
                SyntaxKind.KwThrow => "throw",

                SyntaxKind.KwLock => "lock",
                SyntaxKind.KwChecked => "checked",
                SyntaxKind.KwUnchecked => "unchecked",

                SyntaxKind.KwTrue => "true",
                SyntaxKind.KwFalse => "false",
                SyntaxKind.KwNull => "null",

                SyntaxKind.KwUsing => "using",
                SyntaxKind.KwNamespace => "namespace",

                SyntaxKind.KwBreak => "break",
                SyntaxKind.KwContinue => "continue",
                SyntaxKind.KwReturn => "return",
                SyntaxKind.KwYield => "yield",

                SyntaxKind.KwConst => "const",
                SyntaxKind.KwReadonly => "readonly",
                SyntaxKind.KwUnsafe => "unsafe",
                SyntaxKind.KwVolatile => "volatile", // would be used anywhere?
                SyntaxKind.KwGlobal => "global", // would be used anywhere?
                SyntaxKind.KwDefault => "default",
                SyntaxKind.KwNew => "new",
                SyntaxKind.KwStackAlloc => "stackalloc",
                SyntaxKind.KwBase => "base",
                SyntaxKind.KwSizeof => "sizeof",
                SyntaxKind.KwAlignof => "alignof",
                SyntaxKind.KwTypeof => "typeof",
                SyntaxKind.KwNameof => "nameof",

                SyntaxKind.KwGet => "get", // 'get' in properties
                SyntaxKind.KwSet => "set", // 'set' in properties

                SyntaxKind.KwIn => "in",
                SyntaxKind.KwIs => "is",
                SyntaxKind.KwAs => "as",
                SyntaxKind.KwRef => "ref",
                SyntaxKind.KwOut => "out",
                SyntaxKind.KwParams => "params",
                SyntaxKind.KwArglist => "arglist",
                SyntaxKind.KwWhere => "where",

                SyntaxKind.KwPublic => "public",
                SyntaxKind.KwInternal => "internal",
                SyntaxKind.KwProtected => "protected",
                SyntaxKind.KwPrivate => "private",
                SyntaxKind.KwUnreflected => "unreflected",

                SyntaxKind.KwAsync => "async",
                SyntaxKind.KwAwait => "await",

                SyntaxKind.KwStatic => "static",
                SyntaxKind.KwAbstract => "abstract",
                SyntaxKind.KwVirtual => "virtual",
                SyntaxKind.KwOverride => "override",
                SyntaxKind.KwPartial => "partial",
                SyntaxKind.KwExtern => "extern",
                SyntaxKind.KwSealed => "sealed",
                SyntaxKind.KwInline => "inline",
                SyntaxKind.KwNoexcept => "noexcept",

                // for events
                SyntaxKind.KwEvent => "event",
                SyntaxKind.KwAdd => "add",
                SyntaxKind.KwRemove => "remove",

                // for overriding casts
                SyntaxKind.KwExplicit => "explicit",
                SyntaxKind.KwImplicit => "implicit",
                // for overriding operators
                SyntaxKind.KwOperator => "operator",

                _ => string.Empty,
            };
        }

        public static SyntaxKind GetKeywordKind(string text)
        {
            return text switch
            {
                "$" => SyntaxKind.DollarIdentifier,
                "#" => SyntaxKind.SharpIdentifier,
                "@" => SyntaxKind.AtSignIdentifier,

                "~" => SyntaxKind.Tilda,
                ";" => SyntaxKind.Semicolon,
                ":" => SyntaxKind.Colon,
                "," => SyntaxKind.Comma,
                "." => SyntaxKind.Period,
                ".." => SyntaxKind.PeriodPeriod,
                "=" => SyntaxKind.Equal,
                "&" => SyntaxKind.Ampersand,
                "^" => SyntaxKind.Hat,
                "!" => SyntaxKind.Bang,
                "?" => SyntaxKind.QuestionMark,
                "??" => SyntaxKind.DoubleQuestion,
                "|" => SyntaxKind.VerticalSlash,

                "+" => SyntaxKind.Plus,
                "-" => SyntaxKind.Minus,
                "*" => SyntaxKind.Asterisk,
                "/" => SyntaxKind.ForwardSlash,
                "%" => SyntaxKind.Percent,

                "++" => SyntaxKind.PlusPlus,
                "--" => SyntaxKind.MinusMinus,

                "<<" => SyntaxKind.LessLess,
                ">>" => SyntaxKind.GreaterGreater,
                ">>>" => SyntaxKind.GreaterGreaterGreater,

                "+=" => SyntaxKind.AddEq,
                "-=" => SyntaxKind.SubEq,
                "*=" => SyntaxKind.MulEq,
                "/=" => SyntaxKind.DivEq,
                "%=" => SyntaxKind.ModEq,
                "^=" => SyntaxKind.HatEq,
                "&=" => SyntaxKind.AmpEq,
                "|=" => SyntaxKind.PipeEq,
                "??=" => SyntaxKind.CoalesceEq,

                "<" => SyntaxKind.Less,
                "<=" => SyntaxKind.LessEqual,
                ">" => SyntaxKind.Greater,
                ">=" => SyntaxKind.GreaterEqual,
                "==" => SyntaxKind.DoubleEqual,
                "!=" => SyntaxKind.NotEqual,

                "||" => SyntaxKind.LogicalOr,
                "&&" => SyntaxKind.LogicalAnd,

                "=>" => SyntaxKind.Arrow,

                "(" => SyntaxKind.OpenParen,
                ")" => SyntaxKind.CloseParen,
                "{" => SyntaxKind.OpenBrace,
                "}" => SyntaxKind.CloseBrace,
                "[" => SyntaxKind.OpenBracket,
                "]" => SyntaxKind.CloseBracket,

                "[]" => SyntaxKind.ArrayDef,

                // words and other
                "struct" => SyntaxKind.KwStruct,
                "enum" => SyntaxKind.KwEnum,
                "interface" => SyntaxKind.KwInterface,
                "class" => SyntaxKind.KwClass,
                "delegate" => SyntaxKind.KwDelegate,

                "if" => SyntaxKind.KwIf,
                "else" => SyntaxKind.KwElse,
                "switch" => SyntaxKind.KwSwitch,
                "case" => SyntaxKind.KwCase,
                "for" => SyntaxKind.KwFor,
                "foreach" => SyntaxKind.KwForeach,
                "while" => SyntaxKind.KwWhile,
                "do" => SyntaxKind.KwDo,
                "goto" => SyntaxKind.KwGoto,

                // exceptions
                "try" => SyntaxKind.KwTry,
                "catch" => SyntaxKind.KwCatch,
                "finally" => SyntaxKind.KwFinally,
                "throw" => SyntaxKind.KwThrow,

                "lock" => SyntaxKind.KwLock,
                "checked" => SyntaxKind.KwChecked,
                "unchecked" => SyntaxKind.KwUnchecked,

                "true" => SyntaxKind.KwTrue,
                "false" => SyntaxKind.KwFalse,
                "null" => SyntaxKind.KwNull,

                "using" => SyntaxKind.KwUsing,
                "namespace" => SyntaxKind.KwNamespace,

                "break" => SyntaxKind.KwBreak,
                "continue" => SyntaxKind.KwContinue,
                "return" => SyntaxKind.KwReturn,
                "yield" => SyntaxKind.KwYield,

                "const" => SyntaxKind.KwConst,
                "readonly" => SyntaxKind.KwReadonly,
                "unsafe" => SyntaxKind.KwUnsafe,
                "volatile" => SyntaxKind.KwVolatile,
                "global" => SyntaxKind.KwGlobal,
                "default" => SyntaxKind.KwDefault,
                "new" => SyntaxKind.KwNew,
                "stackalloc" => SyntaxKind.KwStackAlloc,
                "base" => SyntaxKind.KwBase,
                "sizeof" => SyntaxKind.KwSizeof,
                "alignof" => SyntaxKind.KwAlignof,
                "typeof" => SyntaxKind.KwTypeof,
                "nameof" => SyntaxKind.KwNameof,

                "get" => SyntaxKind.KwGet,
                "set" => SyntaxKind.KwSet,

                "in" => SyntaxKind.KwIn,
                "is" => SyntaxKind.KwIs,
                "as" => SyntaxKind.KwAs,
                "ref" => SyntaxKind.KwRef,
                "out" => SyntaxKind.KwOut,
                "params" => SyntaxKind.KwParams,
                "arglist" => SyntaxKind.KwArglist,
                "where" => SyntaxKind.KwWhere,

                "public" => SyntaxKind.KwPublic,
                "internal" => SyntaxKind.KwInternal,
                "protected" => SyntaxKind.KwProtected,
                "private" => SyntaxKind.KwPrivate,
                "unreflected" => SyntaxKind.KwUnreflected,

                "async" => SyntaxKind.KwAsync,
                "await" => SyntaxKind.KwAwait,

                "static" => SyntaxKind.KwStatic,
                "abstract" => SyntaxKind.KwAbstract,
                "virtual" => SyntaxKind.KwVirtual,
                "override" => SyntaxKind.KwOverride,
                "partial" => SyntaxKind.KwPartial,
                "extern" => SyntaxKind.KwExtern,
                "sealed" => SyntaxKind.KwSealed,
                "inline" => SyntaxKind.KwInline,
                "noexcept" => SyntaxKind.KwNoexcept,

                // for events
                "event" => SyntaxKind.KwEvent,
                "add" => SyntaxKind.KwAdd,
                "remove" => SyntaxKind.KwRemove,

                // for overriding casts
                "explicit" => SyntaxKind.KwExplicit,
                "implicit" => SyntaxKind.KwImplicit,

                // for overriding operators
                "operator" => SyntaxKind.KwOperator,

                _ => SyntaxKind.Unknown,
            };
        }

        public static object GetValue(SyntaxKind kind, string text)
        {
            return kind switch
            {
                SyntaxKind.KwTrue => true,
                SyntaxKind.KwFalse => false,
                SyntaxKind.KwNull => null,
                _ => text,
            };
        }

        public static bool IsReservedKeyword(SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.KwStruct:
                case SyntaxKind.KwEnum:
                case SyntaxKind.KwInterface:
                case SyntaxKind.KwClass:
                case SyntaxKind.KwDelegate:

                case SyntaxKind.KwIf:
                case SyntaxKind.KwElse:
                case SyntaxKind.KwSwitch:
                case SyntaxKind.KwCase:
                case SyntaxKind.KwFor:
                case SyntaxKind.KwForeach: // would be used anywhere?
                case SyntaxKind.KwWhile:
                case SyntaxKind.KwDo:
                case SyntaxKind.KwGoto:

                // exceptions
                case SyntaxKind.KwTry:
                case SyntaxKind.KwCatch:
                case SyntaxKind.KwFinally:
                case SyntaxKind.KwThrow:

                case SyntaxKind.KwLock:
                case SyntaxKind.KwChecked:
                case SyntaxKind.KwUnchecked:

                case SyntaxKind.KwTrue:
                case SyntaxKind.KwFalse:
                case SyntaxKind.KwNull:

                case SyntaxKind.KwUsing:
                case SyntaxKind.KwNamespace:

                case SyntaxKind.KwBreak:
                case SyntaxKind.KwContinue:
                case SyntaxKind.KwReturn:
                case SyntaxKind.KwYield:

                case SyntaxKind.KwConst:
                case SyntaxKind.KwReadonly:
                case SyntaxKind.KwUnsafe:
                case SyntaxKind.KwVolatile: // would be used anywhere?
                case SyntaxKind.KwGlobal: // would be used anywhere?
                case SyntaxKind.KwDefault:
                case SyntaxKind.KwNew:
                case SyntaxKind.KwStackAlloc:
                case SyntaxKind.KwBase:
                case SyntaxKind.KwSizeof:
                case SyntaxKind.KwAlignof:
                case SyntaxKind.KwTypeof:
                case SyntaxKind.KwNameof:

                case SyntaxKind.KwGet: // 'get' in properties
                case SyntaxKind.KwSet: // 'set' in properties

                case SyntaxKind.KwIn:
                case SyntaxKind.KwIs:
                case SyntaxKind.KwAs:
                case SyntaxKind.KwRef:
                case SyntaxKind.KwOut:
                case SyntaxKind.KwParams:
                case SyntaxKind.KwArglist:
                case SyntaxKind.KwWhere:

                case SyntaxKind.KwPublic:
                case SyntaxKind.KwInternal:
                case SyntaxKind.KwProtected:
                case SyntaxKind.KwPrivate:
                case SyntaxKind.KwUnreflected:

                case SyntaxKind.KwAsync:
                case SyntaxKind.KwAwait:

                case SyntaxKind.KwStatic:
                case SyntaxKind.KwAbstract:
                case SyntaxKind.KwVirtual:
                case SyntaxKind.KwOverride:
                case SyntaxKind.KwPartial:
                case SyntaxKind.KwExtern:
                case SyntaxKind.KwSealed:
                case SyntaxKind.KwInline:
                case SyntaxKind.KwNoexcept:

                // for events
                case SyntaxKind.KwEvent:
                case SyntaxKind.KwAdd:
                case SyntaxKind.KwRemove:

                // for overriding casts
                case SyntaxKind.KwExplicit:
                case SyntaxKind.KwImplicit:
                // for overriding operators
                case SyntaxKind.KwOperator:
                    return true;

                default:
                    return false;
            }
        }
    }
}
