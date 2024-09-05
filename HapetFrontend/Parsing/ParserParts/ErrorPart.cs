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
			_errorHandler.ReportError(_lexer.Text, new Location(Location), message, null, callingFunctionFile, callingFunctionName, callLineNumber);
		}

		[DebuggerStepThrough]
		public void ReportError(ILocation Location, string message)
		{
			var (callingFunctionName, callingFunctionFile, callLineNumber) = CompilerUtils.GetCallingFunction().GetValueOrDefault(("", "", -1));
			_errorHandler.ReportError(_lexer.Text, Location, message, null, callingFunctionFile, callingFunctionName, callLineNumber);
		}

		[DebuggerStepThrough]
		public static ErrorMessageResolver ErrMsg(string expect, string where = null)
		{
			return t => $"Expected {expect} {where}";
		}

		[DebuggerStepThrough]
		private static ErrorMessageResolver ErrMsgUnexpected(string expect, string where = null)
		{
			return t => $"Unexpected token {t} at {where}. Expected {expect}";
		}
	}
}
