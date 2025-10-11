using HapetFrontend.Ast;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Scoping;
using System.Diagnostics;
using System.Text;

namespace HapetFrontend.Entities
{
    public class ProgramFile
    {
        /// <summary>
        /// Filename without path parts
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Full filepath 
        /// </summary>
        public Uri FilePath { get; set; }

        /// <summary>
        /// The full folder name where file is located
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// To grab the text only once and store it here
        /// </summary>
        public StringBuilder Text { get; set; }

        /// <summary>
        /// Splitted file text
        /// </summary>
        public string[] TextSplitted { get; set; }

        /// <summary>
        /// Is the file imported/virtual from another assembly
        /// </summary>
        public bool IsImported { get; set; }

        /// <summary>
        /// Stores all comment locations (used in LSP only probably)
        /// </summary>
        public List<ILocation> CommentLocations { get; } = new List<ILocation>();

        public Scope NamespaceScope { get; set; }

        public List<AstStatement> Statements { get; } = new List<AstStatement>();
        public List<AstUsingStmt> Usings { get; set; } = new List<AstUsingStmt>();
        /// <summary>
        /// Handles all the #define's that could be accessed accross the whole file
        /// </summary>
        public List<AstDirectiveStmt> Defines { get; private set; } = new List<AstDirectiveStmt>();

        /// <summary>
        /// Used in LSP
        /// </summary>
        public ILocation NamespaceTokenLocation { get; set; }

        /// <summary>
        /// Used in LSP
        /// </summary>
        public List<ILocation> NotCompiledLocations { get; } = new List<ILocation>();

        /// <summary>
        /// Used in LSP
        /// </summary>
        public List<ILocation> DirectiveNameLocations { get; } = new List<ILocation>();

        public ProgramFile(string name, StringBuilder text)
        {
            this.Name = name;
            this.Text = text;
        }

        public override string ToString()
        {
            return $"ProgramFile: {Name}";
        }

        public Location GetLocationFromSpan(int start, int end)
        {
            var (line, offset) = GetLineNumberAndOffsetByIndex(start);
            TokenLocation locStart = new TokenLocation()
            {
                File = FilePath.AbsolutePath,
                Line = line,
                LineStartIndex = start - offset,
                Index = start,
                End = start,
            };
            (line, offset) = GetLineNumberAndOffsetByIndex(end);
            TokenLocation locEnd = new TokenLocation()
            {
                File = FilePath.AbsolutePath,
                Line = line,
                LineStartIndex = end - offset,
                Index = end,
                End = end,
            };
            return new Location(locStart, locEnd);
        }

        public (int line, int offset) GetLineNumberAndOffsetByIndex(int index)
        {
            if (TextSplitted == null)
                return (0, 0);
            int currentIndexSum = 0;
            int currentLineNumber = 0;
            foreach (var line in TextSplitted)
            {
                var prevIndexSum = currentIndexSum;
                currentIndexSum += line.Length + 1; // + 1 is for \n
                if (currentIndexSum > index)
                    return (currentLineNumber, index - prevIndexSum);
                currentLineNumber++;
            }
            Debug.Assert(false, "Should not be here");
            return (currentLineNumber, -1); // should not be here
        }

        public (List<int> lines, List<int> offsets, List<int> widths) GetLinesAndOffsetsForXmlComment(int start, int end)
        {
            if (TextSplitted == null)
                return ([0], [0], [0]);

            List<int> lines = new List<int>();
            List<int> offsets = new List<int>();
            List<int> widths = new List<int>();

            int currentIndexSum = 0;
            int currentLineNumber = 0;
            for (int i = 0; i < TextSplitted.Length; ++i)
            {
                var line = TextSplitted[i];
                var prevIndexSum = currentIndexSum;
                currentIndexSum += line.Length + 1; // + 1 is for \n
                if (currentIndexSum > start)
                {
                    lines.Add(currentLineNumber);
                    // if there are already elements - offset is 0
                    int offset = offsets.Count == 0 ? start - prevIndexSum : 0;
                    offsets.Add(offset);
                    // check that it is the last comment line
                    bool lastLine = (i + 1 < TextSplitted.Length) ||
                        currentIndexSum + TextSplitted[i + 1].Length > end;
                    int width = lastLine ? (end - prevIndexSum) : (line.Length - offset);
                    widths.Add(width);
                }

                if (currentIndexSum > end)
                    break;
                currentLineNumber++;
            }
            return (lines, offsets, widths);
        }
    }
}
