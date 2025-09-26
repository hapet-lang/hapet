using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HapetCompiler.ProjectConf
{
    public partial class ProjectXmlParser
    {
        private (int line, int offset) GetLineNumberAndOffsetByIndex(int index)
        {
            if (_currentSplittedFileText == null)
                return (0, 0);
            int currentIndexSum = 0;
            int currentLineNumber = 0;
            foreach (var line in _currentSplittedFileText)
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

        private (List<int> lines, List<int> offsets, List<int> widths) GetLinesAndOffsetsForComment(int start, int end)
        {
            if (_currentSplittedFileText == null)
                return ([0], [0], [0]);

            List<int> lines = new List<int>();
            List<int> offsets = new List<int>();
            List<int> widths = new List<int>();

            int currentIndexSum = 0;
            int currentLineNumber = 0;
            for (int i = 0; i < _currentSplittedFileText.Length; ++i)
            {
                var line = _currentSplittedFileText[i];
                var prevIndexSum = currentIndexSum;
                currentIndexSum += line.Length + 1; // + 1 is for \n
                if (currentIndexSum > start)
                {
                    lines.Add(currentLineNumber);
                    // if there are already elements - offset is 0
                    int offset = offsets.Count == 0 ? start - prevIndexSum : 0;
                    offsets.Add(offset);
                    // check that it is the last comment line
                    bool lastLine = (i + 1 < _currentSplittedFileText.Length) ||
                        currentIndexSum + _currentSplittedFileText[i + 1].Length > end;
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
