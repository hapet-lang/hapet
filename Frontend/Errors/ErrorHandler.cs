using Frontend.Parsing.Entities;
using System.Runtime.CompilerServices;

namespace Frontend.Errors
{
	public interface IErrorHandler
	{
		bool HasErrors { get; set; }
		ITextProvider TextProvider { get; set; }

		void ReportError(string message, [CallerFilePath] string callingFunctionFile = "", [CallerMemberName] string callingFunctionName = "", [CallerLineNumber] int callLineNumber = 0);
		void ReportError(string text, ILocation location, string message, List<Error> subErrors = null, [CallerFilePath] string callingFunctionFile = "", [CallerMemberName] string callingFunctionName = "", [CallerLineNumber] int callLineNumber = 0);
		void ReportError(Error error);
	}

	public class SilentErrorHandler : IErrorHandler
	{
		public bool HasErrors { get; set; }
		public ITextProvider TextProvider { get; set; }

		public List<Error> Errors { get; } = new List<Error>();

		public void ReportError(string message, [CallerFilePath] string callingFunctionFile = "", [CallerMemberName] string callingFunctionName = "", [CallerLineNumber] int callLineNumber = 0)
		{
			HasErrors = true;
			Errors.Add(new Error
			{
				Message = message,
				File = callingFunctionFile,
				Function = callingFunctionName,
				LineNumber = callLineNumber
			});
		}

		public void ReportError(string text, ILocation location, string message, List<Error> subErrors, [CallerFilePath] string callingFunctionFile = "", [CallerMemberName] string callingFunctionName = "", [CallerLineNumber] int callLineNumber = 0)
		{
			ReportError(new Error
			{
				Location = location,
				Message = message,
				SubErrors = subErrors,
				File = callingFunctionFile,
				Function = callingFunctionName,
				LineNumber = callLineNumber,
			});
		}

		public void ReportError(Error error)
		{
			HasErrors = true;
			Errors.Add(error);
		}

		public void ClearErrors()
		{
			HasErrors = false;
			Errors.Clear();
		}

		public void ForwardErrors(IErrorHandler next)
		{
			foreach (var e in Errors)
			{
				next.ReportError(e);
			}
		}
	}
}
