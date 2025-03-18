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

            foreach (var decl in decls)
            {
                if (decl.SpecialKeys.Contains(TokenType.KwUnreflected))
                    continue;

                // TODO: doc string

                // serialize attributes
                foreach (var attr in decl.Attributes)
                {
                    CreateAttributeDecl(attr, sb, "");
                }

                CreateSpecialKeys(decl.SpecialKeys, sb, "");

                // the decl itself
                if (decl is AstClassDecl clsDecl)
                {
                    CreateClassDecl(clsDecl, sb, "");
                }
                else if (decl is AstStructDecl strDecl)
                {
                    CreateStructDecl(strDecl, sb, "");
                }
            }
        }

        private void CreateAttributeDecl(AstAttributeStmt attr, StringBuilder sb, string additionalOffset)
        {
            StringBuilder args = new StringBuilder();
            if (attr.Arguments.Count > 0)
            {
                args.Append('(');
                for (int i = 0; i < attr.Arguments.Count; i++)
                {
                    var arg = attr.Arguments[i];
                    if (arg.OutValue is string str)
                        args.Append($"\"{str}\"");
                    else
                        args.Append($"{arg.OutValue}");

                    if (i < attr.Arguments.Count - 1)
                        args.Append(", ");
                }
                args.Append(')');
            }

            sb.Append($"{additionalOffset}[{attr.AttributeName.TryFlatten(null, null)}{args}]\n");
        }

        private void CreateSpecialKeys(List<TokenType> keys, StringBuilder sb, string additionalOffset)
        {
            sb.Append(additionalOffset);
            // serialize special keys
            foreach (var sk in keys)
            {
                sb.Append($"{Lexer.GetKeywordFromToken(sk)} ");
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
                var inhs = decl.InheritedFrom.Select(x => x.GetNested().TryFlatten(null, null));
                sb.Append(string.Join(", ", inhs));
            }

            // TODO: generic constraiins 

            sb.Append($"\n{additionalOffset}{{\n");

            foreach (var d in decl.Declarations)
            {
                if (d.SpecialKeys.Contains(TokenType.KwUnreflected))
                    continue;

                // TODO: doc string

                // serialize attributes
                foreach (var attr in d.Attributes)
                {
                    CreateAttributeDecl(attr, sb, additionalOffset + _fourSpaces);
                }

                CreateSpecialKeys(d.SpecialKeys, sb, additionalOffset + _fourSpaces);

                if (d is AstFuncDecl func)
                {
                    CreateFuncDecl(func, sb, additionalOffset + _fourSpaces, decl.HasGenericTypes);
                }
                else if (d is AstPropertyDecl prop)
                {
                    CreatePropertyDecl(prop, sb, additionalOffset + _fourSpaces);
                }
                else if (d is AstVarDecl field)
                {
                    CreateFieldDecl(field, sb, additionalOffset + _fourSpaces);
                }
            }

            sb.Append($"{additionalOffset}}}\n\n");
        }

        private void CreateStructDecl(AstStructDecl decl, StringBuilder sb, string additionalOffset)
        {
            sb.Append("struct ");
            sb.Append($"{GenericsHelper.GetNameFromAst(decl.Name).GetClassNameWithoutNamespace()} ");

            if (decl.InheritedFrom.Count > 0)
            {
                sb.Append(": ");
                var inhs = decl.InheritedFrom.Select(x => x.GetNested().TryFlatten(null, null));
                sb.Append(string.Join(", ", inhs));
            }

            // TODO: generic constraiins 

            sb.Append($"\n{additionalOffset}{{\n");

            foreach (var d in decl.Declarations)
            {
                if (d.SpecialKeys.Contains(TokenType.KwUnreflected))
                    continue;

                // TODO: doc string

                // serialize attributes
                foreach (var attr in d.Attributes)
                {
                    CreateAttributeDecl(attr, sb, additionalOffset + _fourSpaces);
                }

                CreateSpecialKeys(d.SpecialKeys, sb, additionalOffset + _fourSpaces);

                if (d is AstFuncDecl func)
                {
                    CreateFuncDecl(func, sb, additionalOffset + _fourSpaces, decl.HasGenericTypes);
                }
                else if (d is AstPropertyDecl prop)
                {
                    CreatePropertyDecl(prop, sb, additionalOffset + _fourSpaces);
                }
                else if (d is AstVarDecl field)
                {
                    CreateFieldDecl(field, sb, additionalOffset + _fourSpaces);
                }
            }

            sb.Append($"{additionalOffset}}}\n\n");
        }

        private void CreateFuncDecl(AstFuncDecl decl, StringBuilder sb, string additionalOffset, bool isParentGeneric)
        {
            // return type
            sb.Append(GenericsHelper.GetNameFromType(decl.Returns.OutType));

            sb.Append($" {GenericsHelper.GetNameFromAst(decl.Name).GetPureFuncName()}");

            sb.Append('(');
            for (int i = 0; i < decl.Parameters.Count; ++i)
            {
                var par = decl.Parameters[i];
                sb.Append(GenericsHelper.GetNameFromType(par.Type.OutType));
                sb.Append(' ');
                sb.Append(GenericsHelper.GetNameFromAst(par.Name).GetPureFuncName());

                if (i < decl.Parameters.Count - 1)
                    sb.Append(", ");
            }
            sb.Append(')');

            // TODO: generic constraiins 

            if ((decl.HasGenericTypes || isParentGeneric) && !decl.SpecialKeys.Contains(TokenType.KwAbstract))
            {
                sb.Append($"\n{additionalOffset}{{\n");
                var bodyText = decl.SourceFile.Text.Substring(decl.Body.Location.Beginning.Index, decl.Body.Location.Ending.End - decl.Body.Location.Beginning.Index);
                bodyText = bodyText.TrimStart('{').TrimEnd('}');
                sb.Append(bodyText); // TODO: prettify the block text
                sb.Append($"\n{additionalOffset}}}\n");
            }
            else
            {
                sb.Append(";\n");
            }
        }

        private void CreatePropertyDecl(AstPropertyDecl decl, StringBuilder sb, string additionalOffset)
        {
            // return type
            sb.Append(GenericsHelper.GetNameFromType(decl.Type.OutType));

            sb.Append($" {GenericsHelper.GetNameFromAst(decl.Name).GetPureFuncName()}");

            sb.Append(" { get; ");
            if (decl.HasSet)
                sb.Append("set; ");
            sb.Append("}\n");
        }

        private void CreateFieldDecl(AstVarDecl decl, StringBuilder sb, string additionalOffset)
        {
            // return type
            sb.Append(GenericsHelper.GetNameFromType(decl.Type.OutType));

            sb.Append($" {GenericsHelper.GetNameFromAst(decl.Name).GetPureFuncName()}");

            sb.Append(";\n");
        }
    }
}
