using HapetFrontend.Ast;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace HapetLsp
{
    public sealed class LspMessageHandler : IMessageHandler
    {
        public bool HasErrors { get; set; }

        private readonly List<CompilerMessage> _messages = new List<CompilerMessage>();

        public void ReportMessage(string[] messageArgs, IXmlMessage xmlMessage, ReportType reportType = ReportType.Error, [CallerFilePath] string callingFunctionFile = "", [CallerMemberName] string callingFunctionName = "", [CallerLineNumber] int callLineNumber = 0)
        {
            if (reportType == ReportType.Error)
                HasErrors = true;

            // TODO: just messages that are not connected to files
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
            _messages.Add(message);
        }

        public IEnumerable<Diagnostic> GetDiagnosticMessages(Uri filePath)
        {
            foreach (var m in _messages) 
            {
                if (m.ProgramFile.FilePath.AbsolutePath == filePath.AbsolutePath)
                    yield return GetDiagnosticMessage(m);
            }
        }

        public Diagnostic GetDiagnosticMessage(CompilerMessage message)
        {
            string stringMessage = $"[{message.XmlMessage.GetName()}] {string.Format(CultureInfo.InvariantCulture, message.XmlMessage.Text, message.MessageArgs)}";
            DiagnosticSeverity severity = message.ReportType switch
            {
                ReportType.Info => DiagnosticSeverity.Information,
                ReportType.Warning => DiagnosticSeverity.Warning,
                ReportType.Error => DiagnosticSeverity.Error,
                _ => DiagnosticSeverity.Error,
            };
            OmniSharp.Extensions.LanguageServer.Protocol.Models.Range range = 
                new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                    message.Location.Beginning.Line, 
                    message.Location.Beginning.Index, 
                    message.Location.Ending.Line, 
                    message.Location.Ending.Index);
            return new Diagnostic()
            {
                Message = stringMessage,
                Source = message.ProgramFile.FilePath.AbsolutePath,
                Severity = severity,
                Range = range
            };
        }
    }
}
