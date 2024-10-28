using HapetFrontend.Ast;
using HapetFrontend.Entities;
using System.Text;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		public delegate string ErrorMessageResolver(Token t);
		private delegate AstStatement ExpressionParser(bool allowCommaForTuple, bool allowFunctionExpression, ErrorMessageResolver e, bool allowPointerExpressions);

		private ILexer _lexer;
		private IErrorHandler _errorHandler;

		private Token _lastNonWhitespace = null;

		private Token _currentToken = null;
		private Token CurrentToken => _currentToken; // probably could be public

		private StringBuilder _docString = new StringBuilder();

		public Parser(ILexer lex, IErrorHandler errHandler)
		{
			_lexer = lex;
			_errorHandler = errHandler;
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
