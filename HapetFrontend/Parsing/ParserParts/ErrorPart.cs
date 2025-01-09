using HapetFrontend.Ast;
using HapetFrontend.Entities;
using HapetFrontend.Helpers;
using System.Diagnostics;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        [DebuggerStepThrough]
        public void ReportMessage(TokenLocation Location, string message, ReportType reportType = ReportType.Error)
        {
            var (callingFunctionName, callingFunctionFile, callLineNumber) = CompilerUtils.GetCallingFunction().GetValueOrDefault(("", "", -1));
            _messageHandler.ReportMessage(_lexer.Text, new Location(Location), message, null, reportType, callingFunctionFile, callingFunctionName, callLineNumber);
        }

        [DebuggerStepThrough]
        public void ReportMessage(ILocation Location, string message, ReportType reportType = ReportType.Error)
        {
            var (callingFunctionName, callingFunctionFile, callLineNumber) = CompilerUtils.GetCallingFunction().GetValueOrDefault(("", "", -1));
            _messageHandler.ReportMessage(_lexer.Text, Location, message, null, reportType, callingFunctionFile, callingFunctionName, callLineNumber);
        }

        [DebuggerStepThrough]
        public static MessageResolver ErrMsg(string expect, string where = null)
        {
            return t => $"Expected {expect} {where}";
        }

        [DebuggerStepThrough]
        private static MessageResolver ErrMsgUnexpected(string expect, string where = null)
        {
            return t => $"Unexpected token {t} at {where}. Expected {expect}";
        }
    }
}
