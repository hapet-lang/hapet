using HapetFrontend.Ast;
using System.Diagnostics;

namespace HapetFrontend.Parsing
{
	public partial class Parser
	{
		[DebuggerStepThrough]
		public void ReportError(TokenLocation Location, string message)
		{
			var (callingFunctionName, callingFunctionFile, callLineNumber) = CompilerUtils.GetCallingFunction().GetValueOrDefault(("", "", -1));
			_messageHandler.ReportMessage(_lexer.Text, new Location(Location), message, null, Entities.ReportType.Error, callingFunctionFile, callingFunctionName, callLineNumber);
		}

		[DebuggerStepThrough]
		public void ReportError(ILocation Location, string message)
		{
			var (callingFunctionName, callingFunctionFile, callLineNumber) = CompilerUtils.GetCallingFunction().GetValueOrDefault(("", "", -1));
			_messageHandler.ReportMessage(_lexer.Text, Location, message, null, Entities.ReportType.Error, callingFunctionFile, callingFunctionName, callLineNumber);
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
