using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetLastPrepare;
using HapetLsp.Colorizers;
using HapetPostPrepare;

namespace HapetLsp.Handlers
{
    public partial class HapetSyncHandler
    {
        internal static void OnAddText(HapetColorizer colorizer, string newText, OmniSharp.Extensions.LanguageServer.Protocol.Models.Range range)
        {
            var index = colorizer.File.GetIndexFromLineAndOffset(range.Start.Line, range.Start.Character);
            colorizer.File.Text.Insert(index, newText);

            // TODO: do not split again but try to use existed
            colorizer.File.TextSplitted = colorizer.File.Text.ToString().Split('\n');
            var tst = colorizer.File.Text.ToString();
        }

        internal static void OnRemoveText(HapetColorizer colorizer, int rangeLength, OmniSharp.Extensions.LanguageServer.Protocol.Models.Range range)
        {
            var index = colorizer.File.GetIndexFromLineAndOffset(range.Start.Line, range.Start.Character);
            colorizer.File.Text.Remove(index, rangeLength);

            // TODO: do not split again but try to use existed
            colorizer.File.TextSplitted = colorizer.File.Text.ToString().Split('\n');
            var tst = colorizer.File.Text.ToString();
        }

        internal static void Reparse(HapetColorizer colorizer, Compiler compiler, PostPrepare postPrepare, LastPrepare lastPrepare)
        {
            //int line = range.Start.Line + 1;
            AstStatement parentStmt = null;//GetParentToReparse(colorizer, line);

            // different parents
            switch (parentStmt)
            {
                //case AstBlockExpr block:
                //    break;
                // if parent is null - reparse whole file
                case null:
                default:
                    // clear all decls from namespace and lists
                    foreach (var s in colorizer.File.Statements)
                    {
                        if (s is not AstDeclaration decl)
                            continue;
                        colorizer.File.NamespaceScope.RemoveDeclSymbol(decl.Name, decl);
                        RemoveDeclFromLists(decl, postPrepare);
                    }
                    colorizer.File.Statements.Clear();
                    colorizer.File.CommentLocations.Clear();
                    colorizer.File.Defines.Clear();
                    colorizer.File.DirectiveNameLocations.Clear();
                    colorizer.File.NotCompiledLocations.Clear();
                    colorizer.File.Usings.Clear();

                    // clear diagnostic messages of file
                    (compiler.MessageHandler as LspMessageHandler).RemoveDiagnosticMessagesOfFile(colorizer.File);

                    // reparse
                    colorizer.File.FileParser.SetLocation(new TokenLocation()
                    {
                        Index = 0,
                        End = 0,
                        File = colorizer.File.FilePath.AbsolutePath,
                        LineStartIndex = 0,
                        Line = 1,
                    }, resetCurrentToken: true);
                    compiler.MakeParseOfFile(colorizer.File.FileParser, colorizer.File, colorizer.File.FilePath.AbsolutePath);
                    // pp
                    PostPrepareFile(colorizer.File, postPrepare, lastPrepare);

                    // clear all color tokens
                    colorizer.CurrentSemanticTokens.Clear();
                    // colorize
                    colorizer.Colorize();
                    // sort and add to builder
                    colorizer.SortTokens();

                    break;
            }
        }

        private static AstStatement GetParentToReparse(HapetColorizer colorizer, int line)
        {
            AstStatement statementAtLine = null;
            AstStatement prevStatement = null; // needed if there is no anything at required line
            foreach (var (_, s) in colorizer.CurrentSemanticTokens)
            {
                // skip nulled
                if (s == null)
                    continue;
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
            
            // there are no statements here nor above
            if (statementAtLine == null)
                return null;

            // go up until decl or block or null
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

        private static void PostPrepareFile(ProgramFile file, PostPrepare postPrepare, LastPrepare lastPrepare)
        {
            postPrepare.ReplaceAllTuplesInFile(file);

            postPrepare.PostPrepareFileSpecialKeys(file);
            postPrepare.PostPrepareClassMethodsInFile(file);
            postPrepare.PostPrepareFileScoping(file);

            postPrepare.AllPostPrepareMetadataTypesInFile(file);
            postPrepare.PostPrepareStatementUpToCurrentStep(true, file.Statements.ToArray());

            foreach (var s in file.Statements)
            {
                if (s is not AstDeclaration decl)
                    continue;
                postPrepare.PostPrepareInheritedShiteOnDecl(decl);
            }

            // TODO: main func search?

            foreach (var s in file.Statements)
            {
                if (s is not AstDeclaration decl)
                    continue;
                lastPrepare.CreateRequiredInDecl(decl);
            }
        }

        private static void RemoveDeclFromLists(AstDeclaration decl, PostPrepare postPrepare)
        {
            switch (decl)
            {
                case AstClassDecl cls:
                    postPrepare.AllClassesMetadata.Remove(cls);
                    foreach (var d in cls.Declarations)
                        RemoveDeclFromLists(d, postPrepare);
                    break;
                case AstStructDecl str:
                    postPrepare.AllStructsMetadata.Remove(str);
                    foreach (var d in str.Declarations)
                        RemoveDeclFromLists(d, postPrepare);
                    break;
                case AstDelegateDecl del: postPrepare.AllDelegatesMetadata.Remove(del); break;
                case AstEnumDecl enm:
                    postPrepare.AllEnumsMetadata.Remove(enm);
                    foreach (var d in enm.Declarations)
                        RemoveDeclFromLists(d, postPrepare);
                    break;
                case AstFuncDecl fnc:
                    postPrepare.AllFunctionsMetadata.Remove(fnc);
                    if (fnc.Body == null)
                        break;
                    foreach (var d in fnc.Body.Statements.Where(x => x is AstFuncDecl))
                        RemoveDeclFromLists(d as AstDeclaration, postPrepare);
                    break;
            }
        }
    }
}
