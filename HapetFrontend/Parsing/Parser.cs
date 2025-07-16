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
        private delegate AstStatement ExpressionParser(ParserInInfo inInfo, ref ParserOutInfo outInfo);

        private readonly ILexer _lexer;
        private readonly Compiler _compiler;
        private readonly IMessageHandler _messageHandler;

        private Token _lastNonWhitespace = null;

        private Token _currentToken = null;
        public ProgramFile CurrentSourceFile { get; set; }

        private Token CurrentToken => _currentToken; // probably could be public

        private readonly StringBuilder _docString = new StringBuilder();

        public Parser(ILexer lex, Compiler compiler, IMessageHandler messageHandler)
        {
            _lexer = lex;
            _compiler = compiler;
            _messageHandler = messageHandler;
        }

        #region Docs
        private string GetCurrentDocString()
        {
            string doc = _docString.ToString();
            return doc;
        }

        private void ClearDocString()
        {
            _docString.Clear();
        }

        private void AppendDocString(string value)
        {
            _docString.AppendLine(value);
        }
        #endregion
    }
}
