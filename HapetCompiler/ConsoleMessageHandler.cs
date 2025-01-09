using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace HapetCompiler
{
    class ConsoleMessageHandler : IMessageHandler
    {
        public bool HasErrors { get; set; }

        public int LinesBeforeError { get; set; } = 1;
        public int LinesAfterError { get; set; } = 1;
        public bool DoPrintLocation { get; set; } = true;

        private const int _maxPrintErrorSize = 10;

        public ConsoleMessageHandler(int linesBeforeError, int linesAfterError, bool printLocation)
        {
            this.LinesBeforeError = linesBeforeError;
            this.LinesAfterError = linesAfterError;
            this.DoPrintLocation = printLocation;
        }

        public void ReportMessage(string text, ILocation location, string message, List<CompilerMessage> subMessages, ReportType reportType = ReportType.Error, [CallerFilePath] string callingFunctionFile = "", [CallerMemberName] string callingFunctionName = "", [CallerLineNumber] int callLineNumber = 0)
        {
            ReportMessage(new CompilerMessage
            {
                FileText = text,
                Location = location,
                Message = message,
                SubMessages = subMessages,
                File = callingFunctionFile,
                LineNumber = callLineNumber,
                Function = callingFunctionName,
                ReportType = reportType,
            });
        }

        public void ReportMessage(string message, ReportType reportType = ReportType.Error, [CallerFilePath] string callingFunctionFile = "", [CallerMemberName] string callingFunctionName = "", [CallerLineNumber] int callLineNumber = 0)
        {
            // getting the print color from message type
            ConsoleColor printColor = GetColorByReportType(reportType);

            if (reportType == ReportType.Error)
                HasErrors = true;

#if DEBUG && PRINT_SRC_LOCATION
            Log($"{callingFunctionFile}:{callLineNumber} - {callingFunctionName}()", ConsoleColor.DarkYellow);
#endif
            Log(message, printColor);
        }

        public void ReportMessage(CompilerMessage message)
        {
            ReportMessageInternal(message);
        }

        private void ReportMessageInternal(CompilerMessage message)
        {
            if (message.ReportType == ReportType.Error)
                HasErrors = true;

            // do not report warning messages from other assemblies
            // TODO: you can get a parameter from cmd to enable the warnings :)
            if (message.ReportType == ReportType.Warning &&
                string.IsNullOrWhiteSpace(message.FileText))
            {
                return;
            }

#if DEBUG && PRINT_SRC_LOCATION
            Log($"{error.File}:{error.LineNumber} - {error.Function}()", ConsoleColor.DarkYellow);
#endif

            // getting the print color from message type
            ConsoleColor printColor = GetColorByReportType(message.ReportType);

            if (message.Location != null)
            {
                TokenLocation beginning = message.Location.Beginning;
                TokenLocation end = message.Location.Ending;

                // location, message
                LogInline($"{(beginning)}: \n", ConsoleColor.White);
                Log(message.Message, printColor);

                if (DoPrintLocation)
                    PrintLocation(message.FileText, message.Location, linesBefore: LinesBeforeError, linesAfter: LinesAfterError, highlightColor: printColor);
            }
            else
            {
                Log(message.Message, printColor);
            }

            // details
            if (message.Details != null)
            {
                foreach (var d in message.Details)
                {
                    Console.WriteLine("|");

                    foreach (var line in d.message.Split('\n'))
                    {
                        if (!string.IsNullOrEmpty(line))
                            Log("| " + line, ConsoleColor.White);
                    }

                    if (d.location != null)
                    {
                        Log($"{d.location.Beginning}: ", ConsoleColor.White);

                        if (DoPrintLocation)
                            PrintLocation(message.FileText, d.location, linesBefore: 0, highlightColor: ConsoleColor.Green);
                    }
                }
            }

            if (message.SubMessages?.Count > 0)
            {
                Log("| Related:", ConsoleColor.White);

                foreach (var e in message.SubMessages)
                {
                    ReportMessage(e);
                }
            }
        }

        private void PrintLocation(string text, ILocation location, bool underline = true, int linesBefore = 2, int linesAfter = 0, ConsoleColor highlightColor = ConsoleColor.Red, ConsoleColor textColor = ConsoleColor.DarkGreen)
        {
            TokenLocation beginning = location.Beginning;
            TokenLocation end = location.Ending;

            if (string.IsNullOrWhiteSpace(text) || beginning == null || end == null)
                return;

            int index = beginning.Index;
            int lineNumber = beginning.Line;
            int lineStart = GetLineStartIndex(text, index);
            int lineEnd = GetLineEndIndex(text, end.End);
            int linesSpread = CountLines(text, index, end.End);
            linesSpread = Math.Min(linesSpread, _maxPrintErrorSize);

            int lineNumberWidth = (end.Line + linesAfter).ToString(CultureInfo.InvariantCulture).Length;

            // lines before current line
            {
                List<string> previousLines = new List<string>();
                int startIndex = lineStart;
                for (int i = 0; i < linesBefore && startIndex > 0; i++)
                {
                    var prevLineEnd = startIndex - 1;
                    var prevLineStart = GetLineStartIndex(text, prevLineEnd);
                    previousLines.Add(text.Substring(prevLineStart, prevLineEnd - prevLineStart));

                    startIndex = prevLineStart;
                }

                for (int i = previousLines.Count - 1; i >= 0; i--)
                {
                    int line = lineNumber - 1 - i;
                    LogInline(string.Format(CultureInfo.InvariantCulture, $"{{0,{lineNumberWidth}}}> ", line), ConsoleColor.White);
                    Log(previousLines[i], textColor);
                }
            }

            // line containing error (may be multiple lines)
            {
                var firstLine = beginning.Line;
                var ls = lineStart; // lineStart
                var le = GetLineEndIndex(text, index); // lineEnd
                var ei = Math.Min(le, end.End); // endIndex
                var i = index;

                for (var line = 0; line < linesSpread; ++line)
                {
                    var part1 = text.Substring(ls, i - ls);
                    var part2 = text.Substring(i, ei - i);
                    var part3 = text.Substring(ei, le - ei);

                    LogInline(string.Format(CultureInfo.InvariantCulture, $"{{0,{lineNumberWidth}}}> ", line + firstLine), ConsoleColor.White);

                    LogInline(part1, textColor);
                    LogInline(part2, highlightColor);
                    Log(part3, textColor);

                    ls = le + 1;
                    i = ls;
                    le = GetLineEndIndex(text, i);
                    ei = Math.Min(le, end.End);
                }
            }

            // underline
            if (linesSpread == 1 && underline)
            {
                char firstChar = '^'; // ^ ~
                char underlineChar = '—'; // — ~
                var str = new string(' ', index - lineStart + lineNumberWidth + 2) + firstChar;
                if (end.End - index - 1 > 0)
                    str += new string(underlineChar, end.End - index - 1);
                Log(str, GetDarkColor(highlightColor));
            }

            // lines after current line
            {
                var sb = new StringBuilder();
                int lineBegin = lineEnd + 1;
                for (int i = 0; i < linesAfter; i++)
                {
                    int line = end.Line + i + 1;
                    lineEnd = GetLineEndIndex(text, lineBegin);
                    if (lineEnd >= text.Length)
                        break;
                    var str = text.Substring(lineBegin, lineEnd - lineBegin);
                    LogInline(string.Format(CultureInfo.InvariantCulture, $"{{0,{lineNumberWidth}}}> ", line), ConsoleColor.White);
                    Log(str, textColor);
                    lineBegin = lineEnd + 1;
                }
            }
        }

        private static ConsoleColor GetDarkColor(ConsoleColor color)
        {
            switch (color)
            {
                case ConsoleColor.Blue: return ConsoleColor.DarkBlue;
                case ConsoleColor.Cyan: return ConsoleColor.DarkCyan;
                case ConsoleColor.Gray: return ConsoleColor.DarkGray;
                case ConsoleColor.Green: return ConsoleColor.DarkGreen;
                case ConsoleColor.Magenta: return ConsoleColor.Magenta;
                case ConsoleColor.Red: return ConsoleColor.DarkRed;
                case ConsoleColor.Yellow: return ConsoleColor.DarkYellow;
                default: return color;
            }
        }

        private static int CountLines(string text, int start, int end)
        {
            int lines = 1;
            for (; start < end && start < text.Length; start++)
            {
                if (text[start] == '\n')
                    lines++;
            }

            return lines;
        }

        private static int GetLineEndIndex(string text, int currentIndex)
        {
            for (; currentIndex < text.Length; currentIndex++)
            {
                if (text[currentIndex] == '\n')
                    break;
            }

            return currentIndex;
        }

        private static int GetLineStartIndex(string text, int currentIndex)
        {
            if (currentIndex >= text.Length)
                currentIndex = text.Length - 1;

            if (text[currentIndex] == '\n')
                currentIndex--;

            for (; currentIndex >= 0; currentIndex--)
            {
                if (text[currentIndex] == '\n')
                    return currentIndex + 1;
            }

            return 0;
        }

        private static void Log(string message, ConsoleColor foreground)
        {
            var colf = Console.ForegroundColor;
            var colb = Console.BackgroundColor;
            Console.ForegroundColor = foreground;
            // Console.BackgroundColor = background;
            Console.Error.WriteLine(message);
            Console.ForegroundColor = colf;
            Console.BackgroundColor = colb;
        }

        private static void LogInline(string message, ConsoleColor foreground)
        {
            var colf = Console.ForegroundColor;
            var colb = Console.BackgroundColor;
            Console.ForegroundColor = foreground;
            // Console.BackgroundColor = background;
            Console.Error.Write(message);
            Console.ForegroundColor = colf;
            Console.BackgroundColor = colb;
        }

        private ConsoleColor GetColorByReportType(ReportType reportType)
        {
            ConsoleColor outColor = ConsoleColor.Red;
            switch (reportType)
            {
                case ReportType.Info: outColor = ConsoleColor.Cyan; break;
                case ReportType.Warning: outColor = ConsoleColor.Yellow; break;
                case ReportType.Error: outColor = ConsoleColor.Red; break;
            }
            return outColor;
        }
    }
}
