namespace Frontend.Parsing.Entities
{
	public class Token
	{
		public TokenType Type { get; set; }
		public TokenLocation Location { get; set; }
		public object Data { get; set; }

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

		Semicolon, // ;
		Colon, // :
		Comma, // ,
		Period, // .
		PeriodPeriod, // ..
		Equal, // =
		Ampersand, // &
		Hat, // ^
		Bang, // !
		VerticalSlash,  // |

		Plus,
		Minus,
		Asterisk,
		ForwardSlash,
		Percent,

		LessLess,  // <<
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

		LogicalOr,  // ||
		LogicalAnd,  // &&

		Arrow,  // =>

		OpenParen,  // (
		CloseParen,  // )
		OpenBrace,  // {
		CloseBrace,  // }
		OpenBracket,  // [
		CloseBracket,  // ]

		// words
		KwStruct,
		KwEnum,
		KwInterface,
		KwClass,

		KwIf,
		KwElse,
		KwSwitch,
		KwCase,
		KwLoop,
		KwFor,
		KwWhile,

		KwTrue,
		KwFalse,
		KwNull,
		KwVoid,

		KwAttach,
		KwUsing,

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

		KwStatic,
		KwAbstract,
		KwVirtual,
		KwOverride,
		KwPartial,
	}
}
