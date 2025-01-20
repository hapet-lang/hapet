using HapetFrontend.Ast;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using System.Text;

namespace HapetFrontend.Parsing
{
    public class MessageResolver
    {
        public IXmlMessage XmlMessage { get; set; }
        public string[] MessageArgs { get; set; }
    }

    public partial class Parser
    {
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
