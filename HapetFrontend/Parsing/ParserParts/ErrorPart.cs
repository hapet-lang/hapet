using HapetFrontend.Ast;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using System.Diagnostics;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        [DebuggerStepThrough]
        [SkipInStackFrame]
        public void ReportMessage(TokenLocation Location, string[] messageArgs, IXmlMessage xmlMessage, ReportType reportType = ReportType.Error)
        {
            var (callingFunctionName, callingFunctionFile, callLineNumber) = CompilerUtils.GetCallingFunction().GetValueOrDefault(("", "", -1));
            _messageHandler.ReportMessage(_lexer.ProgramFile, new Location(Location), messageArgs, xmlMessage, null, reportType, callingFunctionFile, callingFunctionName, callLineNumber);
        }

        [DebuggerStepThrough]
        [SkipInStackFrame]
        public void ReportMessage(ILocation Location, string[] messageArgs, IXmlMessage xmlMessage, ReportType reportType = ReportType.Error)
        {
            var (callingFunctionName, callingFunctionFile, callLineNumber) = CompilerUtils.GetCallingFunction().GetValueOrDefault(("", "", -1));
            _messageHandler.ReportMessage(_lexer.ProgramFile, Location, messageArgs, xmlMessage, null, reportType, callingFunctionFile, callingFunctionName, callLineNumber);
        }

        [DebuggerStepThrough]
        [SkipInStackFrame]
        public static MessageResolver ErrMsg(string expect, string where = null)
        {
            return new MessageResolver()
            {
                XmlMessage = ErrorCode.Get(CTEN.CommonExpectedToken),
                MessageArgs = [expect, where],
            };
        }
    }
}
