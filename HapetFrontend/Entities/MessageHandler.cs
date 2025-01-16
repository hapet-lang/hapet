using HapetFrontend.Ast;
using HapetFrontend.Errors;
using System.Runtime.CompilerServices;

namespace HapetFrontend.Entities
{
    public interface ITextOnLocationProvider
    {
        string GetText(ILocation location);
    }

    public class CompilerMessage
    {
        public IXmlMessage XmlMessage { get; set; }
        public string FileText { get; set; }
        public ILocation Location { get; set; }
        public string Message { get; set; }
        public string File { get; set; }
        public string Function { get; set; }
        public int LineNumber { get; set; }

        public ReportType ReportType { get; set; } = ReportType.Error;

        public List<CompilerMessage> SubMessages { get; set; } = new List<CompilerMessage>();
        public IEnumerable<(string message, ILocation location)> Details { get; set; }

        public CompilerMessage([CallerFilePath] string callingFunctionFile = "", [CallerMemberName] string callingFunctionName = "", [CallerLineNumber] int callLineNumber = 0)
        {
            this.File = callingFunctionFile;
            this.Function = callingFunctionName;
            this.LineNumber = callLineNumber;
        }

        public CompilerMessage(ILocation location, string message, [CallerFilePath] string callingFunctionFile = "", [CallerMemberName] string callingFunctionName = "", [CallerLineNumber] int callLineNumber = 0)
        {
            this.File = callingFunctionFile;
            this.Function = callingFunctionName;
            this.LineNumber = callLineNumber;
            this.Location = location;
            this.Message = message;
        }
    }

    public interface IMessageHandler
    {
        bool HasErrors { get; set; }

        void ReportMessage(string message, IXmlMessage xmlMessage, ReportType reportType = ReportType.Error, [CallerFilePath] string callingFunctionFile = "", [CallerMemberName] string callingFunctionName = "", [CallerLineNumber] int callLineNumber = 0);
        void ReportMessage(string text, ILocation location, string message, IXmlMessage xmlMessage, List<CompilerMessage> subMessages = null, ReportType reportType = ReportType.Error, [CallerFilePath] string callingFunctionFile = "", [CallerMemberName] string callingFunctionName = "", [CallerLineNumber] int callLineNumber = 0);
        void ReportMessage(CompilerMessage message);
    }

    public enum ReportType
    {
        Info,
        Warning,
        Error
    }
}
