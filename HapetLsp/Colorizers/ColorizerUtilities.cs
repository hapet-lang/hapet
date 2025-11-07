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

        internal static void ReparseWholeProject(HapetColorizer colorizer, Compiler compiler, PostPrepare postPrepare, LastPrepare lastPrepare)
        {
            HapetType.CurrentTypeContext.ArrayTypeInstances.Clear();
            HapetType.CurrentTypeContext.NullableTypeInstances.Clear();

            foreach (var (_, file) in compiler.GetFiles())
            {
                foreach (var g in postPrepare.AllGenericsMetadata.ToList())
                {
                    if (g.SourceFile == file)
                    {
                        RemoveDeclFromLists(g, postPrepare);
                    }
                }
                foreach (var (k, v) in HapetType.CurrentTypeContext.ArrayTypeInstances.ToList())
                {
                    if (k.GetDeclaration().SourceFile == file)
                    {
                        HapetType.CurrentTypeContext.ArrayTypeInstances.Remove(k);
                    }
                }
                foreach (var (k, v) in HapetType.CurrentTypeContext.NullableTypeInstances.ToList())
                {
                    if (k.GetDeclaration().SourceFile == file)
                    {
                        HapetType.CurrentTypeContext.NullableTypeInstances.Remove(k);
                    }
                }

                // clear all decls from namespace and lists
                foreach (var s in file.Statements)
                {
                    if (s is not AstDeclaration decl)
                        continue;
                    file.NamespaceScope.RemoveDeclSymbol(decl.Name, decl);
                    RemoveDeclFromLists(decl, postPrepare);

                    // also remove impls from scope
                    foreach (var im in decl.GenericImplementations)
                    {
                        file.NamespaceScope.RemoveDeclSymbol(im.Name, im);
                    }
                }
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
                    File = file.FilePath.AbsolutePath,
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

            Console.WriteLine("asd");
        }

        private static void RemoveDeclFromLists(AstDeclaration decl, PostPrepare postPrepare)
        {
            switch (decl)
            {
                case AstClassDecl cls:
                    postPrepare.AllClassesMetadata.Remove(cls);
                    foreach (var d in cls.GenericImplementations)
                        RemoveDeclFromLists(d, postPrepare);
                    foreach (var d in cls.Declarations)
                        RemoveDeclFromLists(d, postPrepare);
                    break;
                case AstStructDecl str:
                    postPrepare.AllStructsMetadata.Remove(str);
                    foreach (var d in str.GenericImplementations)
                        RemoveDeclFromLists(d, postPrepare);
                    foreach (var d in str.Declarations)
                        RemoveDeclFromLists(d, postPrepare);
                    break;
                case AstGenericDecl gen:
                    postPrepare.AllGenericsMetadata.Remove(gen);
                    foreach (var d in gen.Declarations)
                        RemoveDeclFromLists(d, postPrepare);
                    break;
                case AstDelegateDecl del: 
                    postPrepare.AllDelegatesMetadata.Remove(del);
                    foreach (var d in del.GenericImplementations)
                        RemoveDeclFromLists(d, postPrepare);
                    foreach (var d in del.Functions)
                        RemoveDeclFromLists(d, postPrepare);
                    break;
                case AstEnumDecl enm:
                    postPrepare.AllEnumsMetadata.Remove(enm);
                    foreach (var d in enm.GenericImplementations)
                        RemoveDeclFromLists(d, postPrepare);
                    foreach (var d in enm.Declarations)
                        RemoveDeclFromLists(d, postPrepare);
                    break;
                case AstFuncDecl fnc:
                    postPrepare.AllFunctionsMetadata.Remove(fnc);
                    foreach (var d in fnc.GenericImplementations)
                        RemoveDeclFromLists(d, postPrepare);
                    if (fnc.Body == null)
                        break;
                    foreach (var d in fnc.Body.Statements.Where(x => x is AstFuncDecl))
                        RemoveDeclFromLists(d as AstDeclaration, postPrepare);
                    break;
            }
        }
    }
}
