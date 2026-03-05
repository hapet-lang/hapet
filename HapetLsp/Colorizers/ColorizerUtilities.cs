using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.ProjectParser;
using HapetFrontend.Types;
using HapetLastPrepare;
using HapetLsp.Colorizers;
using HapetPostPrepare;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
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

        internal static void ReparseWholeProject(Compiler compiler, ProjectXmlParser projectParser, PostPrepare postPrepare, LastPrepare lastPrepare, Action projectResolver)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            HapetType.CurrentTypeContext.ArrayTypeInstances.Clear();
            HapetType.CurrentTypeContext.NullableTypeInstances.Clear();
            postPrepare.AllGenericTypes.Clear();
            PointerType.Types.Clear();
            ReferenceType.Types.Clear();
            postPrepare.ClearLists();

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
            (compiler.MessageHandler as LspMessageHandler).RemoveDiagnosticMessagesOfFile(projectParser.XmlProgramFile);
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

            // Console.WriteLine($"Time: {stopwatch.Elapsed.TotalSeconds}");
            stopwatch.Stop();
        }

        internal static void SendMessages(Compiler compiler, ProjectXmlParser projectParser, ILanguageServerFacade facade)
        {
            List<ProgramFile> files = new List<ProgramFile>(compiler.GetFiles().Select(x => x.Value));
            files.Add(projectParser.XmlProgramFile);

            foreach (var filee in files)
            {
                var file2 = filee.OriginalFile != null ? filee.OriginalFile : filee;
                facade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
                {
                    Uri = file2.FilePath,
                    Diagnostics = Container.From((compiler.MessageHandler as LspMessageHandler).GetDiagnosticMessages(file2.FilePath.AbsolutePath))
                });
            }
        }
    }
}
