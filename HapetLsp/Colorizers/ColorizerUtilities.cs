using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Types;
using HapetLastPrepare;
using HapetLsp.Colorizers;
using HapetPostPrepare;
using System;
using System.Diagnostics;

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
        }

        internal static void OnRemoveText(HapetColorizer colorizer, int rangeLength, OmniSharp.Extensions.LanguageServer.Protocol.Models.Range range)
        {
            var index = colorizer.File.GetIndexFromLineAndOffset(range.Start.Line, range.Start.Character);
            colorizer.File.Text.Remove(index, rangeLength);

            // TODO: do not split again but try to use existed
            colorizer.File.TextSplitted = colorizer.File.Text.ToString().Split('\n');
        }

        internal static void ReparseWholeProject(HapetColorizer colorizer, Compiler compiler, PostPrepare postPrepare, LastPrepare lastPrepare, Action projectResolver)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            HapetType.CurrentTypeContext.ArrayTypeInstances.Clear();
            HapetType.CurrentTypeContext.NullableTypeInstances.Clear();

            postPrepare.AllClassesMetadata.Clear();
            postPrepare.AllStructsMetadata.Clear();
            postPrepare.AllEnumsMetadata.Clear();
            postPrepare.AllFunctionsMetadata.Clear();
            postPrepare.AllDelegatesMetadata.Clear();
            postPrepare.AllGenericsMetadata.Clear();

            // actualize files
            compiler.ActualizeFiles();

            // clear imported
            foreach (var (_, file) in compiler.GetFiles())
            {
                if (!file.IsImported)
                    continue;

                // clear all decls from namespace and lists
                file.NamespaceScope.ClearAllSymbols();
                file.Statements.Clear();
                file.CommentLocations.Clear();
                file.Defines.Clear();
                file.DirectiveNameLocations.Clear();
                file.NotCompiledLocations.Clear();
                file.Usings.Clear();

                // clear diagnostic messages of file
                (compiler.MessageHandler as LspMessageHandler).RemoveDiagnosticMessagesOfFile(file);
            }

            // resolve project data
            projectResolver?.Invoke();

            foreach (var (_, file) in compiler.GetFiles())
            {
                if (file.IsImported)
                    continue;
                // clear all decls from namespace and lists
                file.NamespaceScope.ClearAllSymbols();
                file.Statements.Clear();
                file.CommentLocations.Clear();
                file.Defines.Clear();
                file.DirectiveNameLocations.Clear();
                file.NotCompiledLocations.Clear();
                file.Usings.Clear();

                // clear diagnostic messages of file
                (compiler.MessageHandler as LspMessageHandler).RemoveDiagnosticMessagesOfFile(file);

                // reparse
                file.FileParser.SetLocation(new TokenLocation()
                {
                    Index = 0,
                    End = 0,
                    File = file.IsImported ? file.OriginalFile.FilePath.AbsolutePath : file.FilePath.AbsolutePath,
                    LineStartIndex = 0,
                    Line = 1,
                }, resetCurrentToken: true);
                compiler.MakeParseOfFile(file.FileParser, file, file.FilePath.AbsolutePath);
            }

            // post prepare without meta file
            int _ = postPrepare.StartPreparation(false, forLsp: true);
            // full last prepare is not required for LSP
            int __ = lastPrepare.StartPreparation(true);

            // clear all color tokens
            colorizer.CurrentSemanticTokens.Clear();
            // colorize
            colorizer.Colorize();
            // sort and add to builder
            colorizer.SortTokens();

            // Console.WriteLine($"Time: {stopwatch.Elapsed.TotalSeconds}");
            stopwatch.Stop();
        }
    }
}
