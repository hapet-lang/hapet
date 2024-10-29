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
		DollarIdentifier,
		SharpIdentifier,
		AtSignIdentifier,

		Tilda,           // ~
		Semicolon,		 // ;
		Colon,			 // :
		Comma,			 // ,
		Period,			 // .
		PeriodPeriod,	 // ..
		Equal,			 // =
		Ampersand,		 // &
		Hat,			 // ^
		Bang,			 // !
		VerticalSlash,   // |

		Plus,
		Minus,
		Asterisk,		 // *
		ForwardSlash,
		Percent,

		LessLess,		 // <<
		GreaterGreater,  // >>

		AddEq,
		SubEq,
		MulEq,
		DivEq,
		ModEq,

		Less,
		LessEqual,
		Greater,
		GreaterEqual,
		DoubleEqual,
		NotEqual,

		LogicalOr,		 // ||
		LogicalAnd,		 // &&

		Arrow,			 // =>

		OpenParen,		 // (
		CloseParen,		 // )
		OpenBrace,		 // {
		CloseBrace,		 // }
		OpenBracket,	 // [
		CloseBracket,    // ]

		ArrayDef,		 // []

		// words
		KwStruct,
		KwEnum,
		KwInterface,
		KwClass,

		KwIf,
		KwElse,
		KwSwitch,
		KwCase,
		KwFor,
		KwWhile,

		KwTrue,
		KwFalse,
		KwNull,

		KwUsing,
		KwNamespace,

		KwBreak,
		KwContinue,
		KwReturn,

		KwConst,
		KwDefault,
		KwNew,

		KwIn,
		KwIs,
		KwAs,

		KwPublic,
		KwProtected,
		KwPrivate,
		KwUnreflected,

		KwAsync,

		KwStatic,		
		KwAbstract,
		KwVirtual,
		KwOverride,
		KwPartial,
		KwExtern,
		KwSealed,
	}
}
