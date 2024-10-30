using HapetFrontend.Ast;
using HapetFrontend.Entities;
using System.Text;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		public delegate string MessageResolver(Token t);
		private delegate AstStatement ExpressionParser(bool allowCommaForTuple, bool allowFunctionExpression, MessageResolver e, bool allowPointerExpressions);

		private ILexer _lexer;
		private IMessageHandler _messageHandler;

		private Token _lastNonWhitespace = null;

		private Token _currentToken = null;
		private Token CurrentToken => _currentToken; // probably could be public

		private StringBuilder _docString = new StringBuilder();

		public Parser(ILexer lex, IMessageHandler messageHandler)
		{
			_lexer = lex;
			_messageHandler = messageHandler;
		}

		#region Docs
		private string GetCurrentDocString()
		{
			string doc = _docString.ToString();
			_docString.Clear();
			return doc;
		}

		private void AppendDocString(string value)
		{
			_docString.AppendLine(value);
		}
		#endregion
	}
}
