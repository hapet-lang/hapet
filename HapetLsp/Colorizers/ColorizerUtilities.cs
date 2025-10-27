using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetLsp.Colorizers;

namespace HapetLsp.Handlers
{
    public partial class HapetSyncHandler
    {
        internal void OnAddText(HapetColorizer colorizer, string newText, OmniSharp.Extensions.LanguageServer.Protocol.Models.Range range)
        {
            var index = colorizer.File.GetIndexFromLineAndOffset(range.Start.Line, range.Start.Character);
            colorizer.File.Text.Insert(index, newText);

            // TODO: do not split again but try to use existed
            colorizer.File.TextSplitted = colorizer.File.Text.ToString().Split('\n');
            var tst = colorizer.File.Text.ToString();
        }

        internal void OnRemoveText(HapetColorizer colorizer, int rangeLength, OmniSharp.Extensions.LanguageServer.Protocol.Models.Range range)
        {
            var index = colorizer.File.GetIndexFromLineAndOffset(range.Start.Line, range.Start.Character);
            colorizer.File.Text.Remove(index, rangeLength);

            // TODO: do not split again but try to use existed
            colorizer.File.TextSplitted = colorizer.File.Text.ToString().Split('\n');
            var tst = colorizer.File.Text.ToString();
        }

        internal void Reparse(HapetColorizer colorizer)
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
                        RemoveDeclFromLists(decl);
                    }
                    colorizer.File.Statements.Clear();
                    colorizer.File.CommentLocations.Clear();
                    colorizer.File.Defines.Clear();
                    colorizer.File.DirectiveNameLocations.Clear();
                    colorizer.File.NotCompiledLocations.Clear();
                    colorizer.File.Usings.Clear();

                    // reparse
                    colorizer.File.FileParser.SetLocation(new TokenLocation()
                    {
                        Index = 0,
                        End = 0,
                        File = colorizer.File.FilePath.AbsolutePath,
                        LineStartIndex = 0,
                        Line = 1,
                    }, resetCurrentToken: true);
                    _compiler.MakeParseOfFile(colorizer.File.FileParser, colorizer.File, colorizer.File.FilePath.AbsolutePath);
                    // pp
                    PostPrepareFile(colorizer.File);

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

        private void PostPrepareFile(ProgramFile file)
        {
            _postPrepare.ReplaceAllTuplesInFile(file);

            _postPrepare.PostPrepareFileSpecialKeys(file);
            _postPrepare.PostPrepareClassMethodsInFile(file);
            _postPrepare.PostPrepareFileScoping(file);

            _postPrepare.AllPostPrepareMetadataTypesInFile(file);
            foreach (var s in file.Statements)
                _postPrepare.PostPrepareStatementUpToCurrentStep(s, true);

            foreach (var s in file.Statements)
            {
                if (s is not AstDeclaration decl)
                    continue;
                _postPrepare.PostPrepareInheritedShiteOnDecl(decl);
            }

            // TODO: main func search?

            foreach (var s in file.Statements)
            {
                if (s is not AstDeclaration decl)
                    continue;
                _lastPrepare.CreateRequiredInDecl(decl);
            }
        }

        private void RemoveDeclFromLists(AstDeclaration decl)
        {
            switch (decl)
            {
                case AstClassDecl cls: 
                    _postPrepare.AllClassesMetadata.Remove(cls);
                    foreach (var d in cls.Declarations)
                        RemoveDeclFromLists(d);
                    break;
                case AstStructDecl str: 
                    _postPrepare.AllStructsMetadata.Remove(str);
                    foreach (var d in str.Declarations)
                        RemoveDeclFromLists(d);
                    break;
                case AstDelegateDecl del: _postPrepare.AllDelegatesMetadata.Remove(del); break;
                case AstEnumDecl enm: 
                    _postPrepare.AllEnumsMetadata.Remove(enm);
                    foreach (var d in enm.Declarations)
                        RemoveDeclFromLists(d);
                    break;
                case AstFuncDecl fnc: 
                    _postPrepare.AllFunctionsMetadata.Remove(fnc);
                    if (fnc.Body == null)
                        break;
                    foreach (var d in fnc.Body.Statements.Where(x => x is AstFuncDecl))
                        RemoveDeclFromLists(d as AstDeclaration);
                    break;
            }
        }
    }
}
