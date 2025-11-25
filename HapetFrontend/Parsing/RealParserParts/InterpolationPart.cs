using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using System.Text;

namespace HapetFrontend.Parsing
{
    public partial class Parser
    {
        private AstCallExpr PrepareStringInterpolation(AstStringExpr expr, ParserInInfo inInfo, ref ParserOutInfo outInfo)
        {
            if (expr == null)
                return null;

            // token we need to restore then
            var tokenToRestore = PeekToken(inInfo);

            StringBuilder sb = new StringBuilder(expr.OutValue as string);

            // ranges to be replaced with numbers
            List<(int st, int len)> rangesToRemove = new List<(int, int)>();
            // current parsing range
            (int st, int len) currentRange = (0, 0);
            // names of variables
            List<string> names = new List<string>();
            // current parsing name
            StringBuilder currentName = new StringBuilder();
            // is currently parsing name
            bool isNameParsing = false;
            int i = -1;
            while (i < sb.Length - 1)
            {
                i++;
                char curr = sb[i];
                // shadowing }
                if (curr == '}' && i + 1 != sb.Length && sb[i + 1] == '}')
                {
                    i++;
                    continue;
                }
                // shadowing {
                if (curr == '{' && i + 1 != sb.Length && sb[i + 1] == '{')
                {
                    i++;
                    continue;
                }

                if (curr == '{' && !isNameParsing)
                {
                    // this is like "asd{" - cringe
                    if (i + 1 == sb.Length)
                        return null;
                    
                    // name parsing start
                    isNameParsing = true;
                    currentRange = (i + 1, 0);
                    continue;
                }
                else if ((curr == '}' || curr == ':') && isNameParsing)
                {
                    // name parsing stop
                    isNameParsing = false;
                    currentRange = (currentRange.st, i - currentRange.st);

                    rangesToRemove.Add(currentRange);
                    names.Add(currentName.ToString());
                    currentName.Clear();
                    continue;
                }
                else if (isNameParsing)
                {
                    currentName.Append(curr);
                }
            }

            // replacing names with numbers
            int offset = 0;
            for (int j = 0; j < rangesToRemove.Count; j++)
            {
                string num = j.ToString();
                (int st, int len) = rangesToRemove[j];
                sb.Remove(st + offset, len);
                sb.Insert(st + offset, num);
                offset -= len;
                offset += num.Length;
            }
            expr.OutValue = sb.ToString();

            // reparsing exprs
            var savedText = CurrentSourceFile.Text;
            int quoteOffset = 1; // always 1 because of " at the beginning
            TokenLocation tknLocation = expr.Location.Beginning;
            List<AstArgumentExpr> argsForFormat = new List<AstArgumentExpr>(names.Count);
            foreach ((int st, int len) in rangesToRemove)
            {
                // creating token location to reparse
                var tknCloned = tknLocation.Clone();
                tknCloned.End = tknCloned.Index + st + len + quoteOffset;
                tknCloned.Index = tknCloned.Index + st + quoteOffset;

                // cut textsavedText
                var cutText = new StringBuilder(savedText.Length);
                cutText.Append(savedText.ToString(0, tknCloned.End + 1));
                CurrentSourceFile.Text = cutText;

                // set location to expr inside to be parsed
                SetLocation(tknCloned, true);
                var exprInside = ParseExpression(inInfo, ref outInfo);
                argsForFormat.Add(new AstArgumentExpr(exprInside as AstExpression, location: exprInside.Location));
            }
            CurrentSourceFile.Text = savedText;
            // restore location
            SetLocation(tokenToRestore.Location, true);

            // creating ast
            AstNestedExpr stringName = new AstNestedExpr(new AstIdExpr("string", expr.Location)
            {
                IsSyntheticStatement = true,
            }, null, expr.Location)
            {
                IsSyntheticStatement = true,
            };
            AstIdExpr formatFuncName = new AstIdExpr("Format", expr.Location)
            {
                IsSyntheticStatement = true,
            };

            // append changed string to list
            argsForFormat.Insert(0, new AstArgumentExpr(expr as AstExpression, location: expr.Location));
            AstCallExpr callExpr = new AstCallExpr(stringName, formatFuncName, argsForFormat, expr.Location)
            {
                IsSyntheticStatement = true,
            };

            return callExpr;
        }
    }
}
