using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Diagnostics;

namespace HapetLsp.Colorizers
{
    public partial class HapetSemanticHandler
    {
        internal void ReparseLocationOnAdd(HapetColorizer colorizer, string newText, OmniSharp.Extensions.LanguageServer.Protocol.Models.Range range)
        {
            int line = range.Start.Line + 1;
            var parentStmt = GetParentToReparse(colorizer, line);
            colorizer.File.FileParser.SetLocation(parentStmt.Location.Beginning);

            // different parents
            switch (parentStmt)
            {
                case AstBlockExpr block:
                    break;
            }
        }

        private AstStatement GetParentToReparse(HapetColorizer colorizer, int line)
        {
            AstStatement statementAtLine = null;
            AstStatement prevStatement = null; // needed if there is no anything at required line
            foreach (var (_, s) in colorizer.CurrentSemanticTokens)
            {
                if (s.Location.Beginning.Line == line)
                {
                    statementAtLine = s; 
                    break;
                }
                else if (s.Location.Beginning.Line > line)
                {
                    statementAtLine = prevStatement;
                    break;
                }
                prevStatement = s;
            }
            // go up until decl or block or null
            Debug.Assert(statementAtLine != null);
            while (!IsDeclOrBlockOrNull(statementAtLine.NormalParent))
                statementAtLine = statementAtLine.NormalParent;
            return statementAtLine.NormalParent;

            static bool IsDeclOrBlockOrNull(AstStatement statement)
            {
                return statement switch
                {
                    AstBlockExpr or AstDeclaration or null => true,
                    _ => false,
                };
            }
        }
    }
}
