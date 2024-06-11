using Frontend.Parsing.Entities;
using System.Runtime.CompilerServices;

namespace Frontend.Errors
{
	public class Error
	{
		public ILocation Location { get; set; }
		public string Message { get; set; }
		public string File { get; set; }
		public string Function { get; set; }
		public int LineNumber { get; set; }

		public List<Error> SubErrors { get; set; } = new List<Error>();
		public IEnumerable<(string message, ILocation location)> Details { get; set; }

		public Error([CallerFilePath] string callingFunctionFile = "", [CallerMemberName] string callingFunctionName = "", [CallerLineNumber] int callLineNumber = 0)
		{
			this.File = callingFunctionFile;
			this.Function = callingFunctionName;
			this.LineNumber = callLineNumber;
		}

		public Error(ILocation location, string message, [CallerFilePath] string callingFunctionFile = "", [CallerMemberName] string callingFunctionName = "", [CallerLineNumber] int callLineNumber = 0)
		{
			this.File = callingFunctionFile;
			this.Function = callingFunctionName;
			this.LineNumber = callLineNumber;
			this.Location = location;
			this.Message = message;
		}
	}
}
