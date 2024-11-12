using HapetFrontend.Ast;
using System.Xml;

namespace HapetCompiler.ProjectConf.Data
{
	public static class NodeLocationFinder
	{
		public static Location GetLocationOfNode(string text, XmlNode node, string projectPath)
		{
			(int, int) loc = FindNodeLocation(text, node);
			int lineNumberBegin = text.Substring(0, loc.Item1).Where(x => x == '\n').ToList().Count + 1;
			int lineNumberEnd = text.Substring(0, loc.Item2).Where(x => x == '\n').ToList().Count + 1;

			int currentBeginIndex = loc.Item1;
			while (text.Length > currentBeginIndex && text[currentBeginIndex] != '\n')
				currentBeginIndex--;

			int currentEndIndex = loc.Item2;
			while (text.Length > currentEndIndex && text[currentEndIndex] != '\n')
				currentEndIndex--;

			return new Location(
				new TokenLocation() { Line = lineNumberBegin, End = loc.Item1, Index = loc.Item1, LineStartIndex = currentBeginIndex, File = projectPath },
				new TokenLocation() { Line = lineNumberEnd, End = loc.Item2, Index = loc.Item2, LineStartIndex = currentEndIndex, File = projectPath });
		}

		/// <summary>
		/// Returns start and end indexes of the searching tag in provided text
		/// </summary>
		/// <param name="text">The text of xml file</param>
		/// <param name="node">The node to search</param>
		/// <returns>Start and end indexes of the tag in the text</returns>
		public static (int, int) FindNodeLocation(string text, XmlNode node)
		{
			// getting node indexes in the doc
			List<int> indexes = new List<int>();
			XmlNode currNode = node;
			while (currNode.ParentNode != null)
			{
				indexes.Add(IndexOfNode(currNode, currNode.ParentNode));
				currNode = currNode.ParentNode;
			}
			indexes.Reverse();

			int currentCharPosition = 0;
			for (int i = 0; i < indexes.Count; ++i)
			{
				int index = indexes[i];
				SkipNodes(text, index, ref currentCharPosition);

				if (i == indexes.Count - 1)
				{
					// if it is the element itself
					SkipWhitespaces(text, ref currentCharPosition);
					int beginNeededTag = currentCharPosition;
					int endNeededTag;
					while (!text.Substring(currentCharPosition).StartsWith(">"))
					{
						currentCharPosition++;
					}
					currentCharPosition++;
					endNeededTag = currentCharPosition;
					return (beginNeededTag, endNeededTag);
				}
				else
				{
					// just skip parent open tag (we don't need it)
					while (!text.Substring(currentCharPosition).StartsWith(">"))
					{
						currentCharPosition++;
					}
					currentCharPosition++;
				}
			}

			return (0, 0);
		}

		/// <summary>
		/// Return index of child node in parent
		/// </summary>
		/// <param name="node">Child node</param>
		/// <param name="parent">Parent node</param>
		/// <returns>Index of child</returns>
		private static int IndexOfNode(XmlNode node, XmlNode parent)
		{
			for (int i = 0; i < parent.ChildNodes.Count; i++)
			{
				if (parent.ChildNodes[i] == node)
				{
					return i;
				}
			}
			// should not happen =:)
			return -1;
		}

		/// <summary>
		/// Skips specified amount of nodes and returns amount of skipped chars
		/// </summary>
		/// <param name="text">The text of xml file</param>
		/// <param name="amount">Amount of nodes to be skipped</param>
		/// <param name="currentCharPosition">Char position from which we need to skip nodes</param>
		private static void SkipNodes(string text, int amount, ref int currentCharPosition)
		{
			int alreadySkipped = 0;
			while (alreadySkipped < amount)
			{
				SkipWhitespaces(text, ref currentCharPosition);
				if (text.Substring(currentCharPosition).StartsWith("<!--"))
				{
					// comment parsing
					SkipNode(text, true, ref currentCharPosition);
				}
				else
				{
					// normal node parsing parsing
					SkipNode(text, false, ref currentCharPosition);
				}
				alreadySkipped++;
			}
		}

		/// <summary>
		/// Skips a node
		/// </summary>
		/// <param name="text">The text of xml file</param>
		/// <param name="isComment">Is it a comment node</param>
		/// <param name="currentCharPosition">Char position from which we need to skip a node</param>
		private static void SkipNode(string text, bool isComment, ref int currentCharPosition)
		{
			if (isComment)
			{
				while (!text.Substring(currentCharPosition).StartsWith("-->"))
				{
					currentCharPosition++;
				}
				currentCharPosition += 3; // also skip -->
			}
			else
			{
				// need to do it manually with first one
				while (!text.Substring(currentCharPosition).StartsWith(">") && !text.Substring(currentCharPosition).StartsWith("/>"))
				{
					currentCharPosition++;
				}
				if (text.Substring(currentCharPosition).StartsWith("/>"))
				{
					currentCharPosition += 2;
					return;
				}
				else
				{
					currentCharPosition++;
					// skipping content of tags
					while (!text.Substring(currentCharPosition).StartsWith("<"))
					{
						currentCharPosition++;
					}
					// real cringe
					while (true)
					{
						SkipWhitespaces(text, ref currentCharPosition);
						if (text.Substring(currentCharPosition).StartsWith("<!--"))
						{
							// comment parsing
							SkipNode(text, true, ref currentCharPosition);
						}
						else if (text.Substring(currentCharPosition).StartsWith("</"))
						{
							while (!text.Substring(currentCharPosition).StartsWith(">"))
							{
								currentCharPosition++;
							}
							currentCharPosition++; // for >
							return;
						}
						else // <
						{
							// normal node parsing parsing
							SkipNode(text, false, ref currentCharPosition);
						}
					}
				}
			}
		}

		/// <summary>
		/// Skips newlines and whitespaces
		/// </summary>
		/// <param name="text">The text of xml file</param>
		/// <param name="currentCharPosition">Char position from which we need to skip whitespaces</param>
		private static void SkipWhitespaces(string text, ref int currentCharPosition)
		{
			while (text.Length > currentCharPosition && (char.IsWhiteSpace(text[currentCharPosition]) || text[currentCharPosition] == '\n'))
			{
				currentCharPosition++;
			}
		}
	}
}
