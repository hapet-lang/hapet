using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetPostPrepare.Entities;
using System.Text;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private const string _fourSpaces = "    ";

        private Dictionary<ProgramFile, List<AstDeclaration>> _sortedDeclsByFiles;

        private void GenerateMetadataFile()
        {
            _currentPreparationStep = PreparationStep.MetadataCreation;

            var projectVersion = _compiler.CurrentProjectSettings.ProjectVersion;

            SortDeclarations();

            StringBuilder globalStringBuilder = new StringBuilder();
            foreach (var srt in _sortedDeclsByFiles)
            {
                CreateFileDeclarations(srt.Key, srt.Value, globalStringBuilder);
            }

            // WARN: take care about the shite that is goin on here
            var outFolderPath = _compiler.CurrentProjectSettings.OutputDirectory;
            var projectName = _compiler.CurrentProjectSettings.ProjectName;
            File.WriteAllText($"{outFolderPath}/{projectName}.mpt", globalStringBuilder.ToString());
        }

        private void SortDeclarations()
        {
            _sortedDeclsByFiles = new Dictionary<ProgramFile, List<AstDeclaration>>();
            foreach (var cls in _serializeClassesMetadata)
            {
                if (_sortedDeclsByFiles.TryGetValue(cls.SourceFile, out var decls))
                    decls.Add(cls);
                else
                    _sortedDeclsByFiles[cls.SourceFile] = new List<AstDeclaration>() { cls };
            }
            foreach (var str in _serializeStructsMetadata)
            {
                if (_sortedDeclsByFiles.TryGetValue(str.SourceFile, out var decls))
                    decls.Add(str);
                else
                    _sortedDeclsByFiles[str.SourceFile] = new List<AstDeclaration>() { str };
            }
            foreach (var enm in _serializeEnumsMetadata)
            {
                if (_sortedDeclsByFiles.TryGetValue(enm.SourceFile, out var decls))
                    decls.Add(enm);
                else
                    _sortedDeclsByFiles[enm.SourceFile] = new List<AstDeclaration>() { enm };
            }
            foreach (var del in _serializeDelegatesMetadata)
            {
                if (_sortedDeclsByFiles.TryGetValue(del.SourceFile, out var decls))
                    decls.Add(del);
                else
                    _sortedDeclsByFiles[del.SourceFile] = new List<AstDeclaration>() { del };
            }
            // no need to sort func - they would be taken when serializing classes/structs
        }

        private void CreateFileDeclarations(ProgramFile file, List<AstDeclaration> decls, StringBuilder sb)
        {
            sb.Append($"#file \"{CompilerUtils.GetFileRelativePath(_compiler.CurrentProjectSettings.ProjectPath, file.Name)}\"\n");
            sb.Append($"#namespace \"{file.Namespace}\"\n");

            // serialize usings
            foreach (var usng in file.Usings)
            {
                AntiParseExpr(usng, sb, "");
            }

            // serialize all decls
            foreach (var decl in decls)
            {
                if (decl.SpecialKeys.Contains(TokenType.KwUnreflected))
                    continue;

                // TODO: doc string

                // serialize attributes
                foreach (var attr in decl.Attributes)
                {
                    AntiParseExpr(attr, sb, "");
                }

                CreateSpecialKeys(decl.SpecialKeys, sb, "");
                CreateDecl(decl, sb, "");
            }
        }

        private void CreateSpecialKeys(List<Token> keys, StringBuilder sb, string additionalOffset)
        {
            sb.Append(additionalOffset);
            // serialize special keys
            foreach (var sk in keys)
            {
                sb.Append($"{Lexer.GetKeywordFromToken(sk.Type)} ");
            }
        }

        private void CreateDecl(AstDeclaration decl, StringBuilder sb, string additionalOffset)
        {
            // the decl itself
            if (decl is AstClassDecl clsDecl)
            {
                CreateClassDecl(clsDecl, sb, additionalOffset);
            }
            else if (decl is AstStructDecl strDecl)
            {
                CreateStructDecl(strDecl, sb, additionalOffset);
            }
        }

        private void CreateClassDecl(AstClassDecl decl, StringBuilder sb, string additionalOffset)
        {
            if (decl.IsInterface)
                sb.Append("interface ");
            else
                sb.Append("class ");
            sb.Append($"{GenericsHelper.GetNameFromAst(decl.Name).GetClassNameWithoutNamespace()} ");

            if (decl.InheritedFrom.Count > 0)
            {
                sb.Append(": ");
                for (int i = 0; i < decl.InheritedFrom.Count; i++)
                {
                    AntiParseExpr(decl.InheritedFrom[i], sb, additionalOffset);

                    if (i < decl.InheritedFrom.Count - 1)
                        sb.Append(", ");
                }
            }

            // TODO: generic constraiins 

            AstClassDecl theDecl = decl;
            if (decl.HasGenericTypes)
                theDecl = (decl.OriginalGenericDecl as AstClassDecl);

            sb.Append($"\n{additionalOffset}{{\n");

            foreach (var d in theDecl.Declarations)
            {
                if (d.SpecialKeys.Contains(TokenType.KwUnreflected))
                    continue;
                // do not serialize prop' funcs
                if (d is AstFuncDecl func2 && func2.IsPropertyFunction)
                    continue;
                // do not serialize prop' fields
                if (d is AstVarDecl field2 && field2.IsPropertyField)
                    continue;

                // TODO: doc string

                // serialize attributes
                foreach (var attr in d.Attributes)
                {
                    sb.Append(additionalOffset + _fourSpaces);
                    AntiParseExpr(attr, sb, additionalOffset + _fourSpaces);
                }

                CreateSpecialKeys(d.SpecialKeys, sb, additionalOffset + _fourSpaces);

                if (d is AstFuncDecl func)
                {
                    CreateFuncDecl(func, sb, additionalOffset + _fourSpaces, theDecl.HasGenericTypes);
                }
                else if (d is AstPropertyDecl prop)
                {
                    CreatePropertyDecl(prop, sb, additionalOffset + _fourSpaces, theDecl.HasGenericTypes);
                }
                else if (d is AstVarDecl field)
                {
                    CreateFieldDecl(field, sb, additionalOffset + _fourSpaces);
                }
                else
                {
                    CreateDecl(d, sb, additionalOffset + _fourSpaces);
                }
            }

            sb.Append($"{additionalOffset}}}\n");
            // looks better :)
            if (!decl.IsNestedDecl)
                sb.Append('\n');
        }

        private void CreateStructDecl(AstStructDecl decl, StringBuilder sb, string additionalOffset)
        {
            sb.Append("struct ");
            sb.Append($"{GenericsHelper.GetNameFromAst(decl.Name).GetClassNameWithoutNamespace()} ");

            if (decl.InheritedFrom.Count > 0)
            {
                sb.Append(": ");
                for (int i = 0; i < decl.InheritedFrom.Count; i++)
                {
                    AntiParseExpr(decl.InheritedFrom[i], sb, additionalOffset);

                    if (i < decl.InheritedFrom.Count - 1)
                        sb.Append(", ");
                }
            }

            // TODO: generic constraiins 

            AstStructDecl theDecl = decl;
            if (decl.HasGenericTypes)
                theDecl = (decl.OriginalGenericDecl as AstStructDecl);

            sb.Append($"\n{additionalOffset}{{\n");

            foreach (var d in theDecl.Declarations)
            {
                if (d.SpecialKeys.Contains(TokenType.KwUnreflected))
                    continue;
                // do not serialize prop' funcs
                if (d is AstFuncDecl func2 && func2.IsPropertyFunction)
                    continue;
                // do not serialize prop' fields
                if (d is AstVarDecl field2 && field2.IsPropertyField)
                    continue;

                // TODO: doc string

                // serialize attributes
                foreach (var attr in d.Attributes)
                {
                    sb.Append(additionalOffset + _fourSpaces);
                    AntiParseExpr(attr, sb, additionalOffset + _fourSpaces);
                }

                CreateSpecialKeys(d.SpecialKeys, sb, additionalOffset + _fourSpaces);

                if (d is AstFuncDecl func)
                {
                    CreateFuncDecl(func, sb, additionalOffset + _fourSpaces, theDecl.HasGenericTypes);
                }
                else if (d is AstPropertyDecl prop)
                {
                    CreatePropertyDecl(prop, sb, additionalOffset + _fourSpaces, theDecl.HasGenericTypes);
                }
                else if (d is AstVarDecl field)
                {
                    CreateFieldDecl(field, sb, additionalOffset + _fourSpaces);
                }
                else
                {
                    CreateDecl(d, sb, additionalOffset + _fourSpaces);
                }
            }

            sb.Append($"{additionalOffset}}}\n");
            // looks better :)
            if (!decl.IsNestedDecl)
                sb.Append('\n');
        }

        private void CreateFuncDecl(AstFuncDecl decl, StringBuilder sb, string additionalOffset, bool isParentGeneric)
        {
            // return type
            AntiParseExpr(decl.Returns, sb, additionalOffset);
            sb.Append(' ');
            AntiParseExpr(decl.Name, sb, additionalOffset);

            sb.Append('(');
            for (int i = 0; i < decl.Parameters.Count; ++i)
            {
                var par = decl.Parameters[i];
                AntiParseExpr(par.Type, sb, additionalOffset);
                sb.Append(' ');
                AntiParseExpr(par.Name, sb, additionalOffset);

                if (i < decl.Parameters.Count - 1)
                    sb.Append(", ");
            }
            sb.Append(')');

            // TODO: generic constraiins 

            // if the func is generic && not abstract - serialize
            // if parent is generic && func is not static  && func not abstract - serialize
            if ((decl.HasGenericTypes || (isParentGeneric && !decl.SpecialKeys.Contains(TokenType.KwStatic))) && 
                !decl.SpecialKeys.Contains(TokenType.KwAbstract))
            {
                sb.Append('\n');

                AstFuncDecl theDecl = decl;
                if (decl.HasGenericTypes)
                    theDecl = (decl.OriginalGenericDecl as AstFuncDecl);

                AntiParseExpr(theDecl.Body, sb, additionalOffset);
            }
            else
            {
                sb.Append(";\n");
            }
        }

        private void CreatePropertyDecl(AstPropertyDecl decl, StringBuilder sb, string additionalOffset, bool isParentGeneric)
        {
            // return type
            AntiParseExpr(decl.Type, sb, additionalOffset);
            sb.Append(' ');
            // generate another shite for indexer
            if (decl is AstIndexerDecl ind)
            {
                sb.Append("this[");
                var par = ind.IndexerParameter;
                AntiParseExpr(par.Type, sb, additionalOffset);
                sb.Append(' ');
                AntiParseExpr(par.Name, sb, additionalOffset);
                sb.Append(']');
            }
            else
            {
                AntiParseExpr(decl.Name, sb, additionalOffset);
            }

            if (decl.HasGet && decl.GetBlock != null && (decl.HasGenericTypes || 
                (isParentGeneric && !decl.SpecialKeys.Contains(TokenType.KwStatic))))
            {
                sb.Append($" \n{additionalOffset}{{ \n{additionalOffset + _fourSpaces}get \n");
                AntiParseExpr(decl.GetBlock, sb, additionalOffset + _fourSpaces);
            }
            else if (decl.HasGet)
                sb.Append(" { get; ");

            if (decl.HasSet && decl.SetBlock != null && (decl.HasGenericTypes || 
                (isParentGeneric && !decl.SpecialKeys.Contains(TokenType.KwStatic))))
            {
                // pohuy :)
                if (sb[^1] != '\n')
                    sb.Append('\n');

                sb.Append($"{additionalOffset + _fourSpaces}set \n");
                AntiParseExpr(decl.SetBlock, sb, additionalOffset + _fourSpaces);
                sb.Append($"{additionalOffset}}}\n");
            }
            else if (decl.HasSet)
                sb.Append("set; }\n");
            else
                sb.Append("}\n");
        }

        private void CreateFieldDecl(AstVarDecl decl, StringBuilder sb, string additionalOffset)
        {
            // return type
            AntiParseExpr(decl.Type, sb, additionalOffset);
            sb.Append(' ');
            AntiParseExpr(decl.Name, sb, additionalOffset);

            sb.Append(";\n");
        }
    }
}
