using HapetFrontend.Ast;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using System.Runtime.CompilerServices;

namespace HapetLsp
{
    public sealed class LspMessageHandler : IMessageHandler
    {
        public bool HasErrors { get; set; }

        public void ReportMessage(string[] messageArgs, IXmlMessage xmlMessage, ReportType reportType = ReportType.Error, [CallerFilePath] string callingFunctionFile = "", [CallerMemberName] string callingFunctionName = "", [CallerLineNumber] int callLineNumber = 0)
        {
            if (reportType == ReportType.Error)
                HasErrors = true;
        }

        public void ReportMessage(ProgramFile file, ILocation location, string[] messageArgs, IXmlMessage xmlMessage, List<CompilerMessage> subMessages = null, ReportType reportType = ReportType.Error, [CallerFilePath] string callingFunctionFile = "", [CallerMemberName] string callingFunctionName = "", [CallerLineNumber] int callLineNumber = 0)
        {
            ReportMessage(new CompilerMessage
            {
                XmlMessage = xmlMessage,
                ProgramFile = file,
                Location = location,
                MessageArgs = messageArgs,
                SubMessages = subMessages,
                File = callingFunctionFile,
                LineNumber = callLineNumber,
                Function = callingFunctionName,
                ReportType = reportType,
            });
        }

        public void ReportMessage(CompilerMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
