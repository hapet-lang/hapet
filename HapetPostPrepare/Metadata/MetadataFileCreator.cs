using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
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
                _currentSourceFile = srt.Key;
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
            sb.Append($"namespace {file.Namespace};\n");

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

                // doc string
                CreateDocString(decl.Documentation, sb, "");

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
            if (decl is AstClassDecl || decl is AstStructDecl)
            {
                CreateClassOrStructDecl(decl, sb, additionalOffset);
            }
            else if (decl is AstDelegateDecl delDecl)
            {
                CreateDelegateDecl(delDecl, sb, additionalOffset, false);
            }
        }

        private void CreateClassOrStructDecl(AstDeclaration decl, StringBuilder sb, string additionalOffset)
        {
            List<AstNestedExpr> inheritedFrom = new List<AstNestedExpr>();
            if (decl is AstStructDecl strDecl)
            {
                inheritedFrom = strDecl.InheritedFrom;
                sb.Append("struct ");
            }
            else if (decl is AstClassDecl clsDecl)
            {
                inheritedFrom = clsDecl.InheritedFrom;
                if (clsDecl.IsInterface)
                    sb.Append("interface ");
                else
                    sb.Append("class ");
            }
            
            sb.Append($"{GetNameFromAst(decl.Name, _compiler.MessageHandler).GetClassNameWithoutNamespace()} ");

            if (inheritedFrom.Count > 0)
            {
                sb.Append(": ");
                for (int i = 0; i < inheritedFrom.Count; i++)
                {
                    AntiParseExpr(inheritedFrom[i], sb, additionalOffset);

                    if (i < inheritedFrom.Count - 1)
                        sb.Append(", ");
                }
            }

            // TODO: generic constraiins 

            bool hasParentParentGenerics = decl.IsNestedDecl && decl.ParentDecl.HasGenericTypes;

            AstDeclaration theDecl = decl;
            List<AstDeclaration> decls = new List<AstDeclaration>();
            if (theDecl is AstClassDecl clsDecl1)
                decls = clsDecl1.Declarations;
            else if (theDecl is AstStructDecl strDecl1)
                decls = strDecl1.Declarations;

            sb.Append($"\n{additionalOffset}{{\n");

            foreach (var d in decls)
            {
                if (d.SpecialKeys.Contains(TokenType.KwUnreflected))
                    continue;
                // do not serialize prop' funcs
                if (d is AstFuncDecl func2 && func2.IsPropertyFunction)
                    continue;
                // do not serialize prop' fields
                if (d is AstVarDecl field2 && field2.IsPropertyField)
                    continue;

                // doc string
                CreateDocString(d.Documentation, sb, additionalOffset + _fourSpaces);

                // serialize attributes
                foreach (var attr in d.Attributes)
                {
                    sb.Append(additionalOffset + _fourSpaces);
                    AntiParseExpr(attr, sb, additionalOffset + _fourSpaces);
                }

                CreateSpecialKeys(d.SpecialKeys, sb, additionalOffset + _fourSpaces);

                if (d is AstFuncDecl func)
                {
                    CreateFuncDecl(func, sb, additionalOffset + _fourSpaces, theDecl.HasGenericTypes || hasParentParentGenerics);
                }
                else if (d is AstPropertyDecl prop)
                {
                    CreatePropertyDecl(prop, sb, additionalOffset + _fourSpaces, theDecl.HasGenericTypes || hasParentParentGenerics);
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

        private void CreateDelegateDecl(AstDelegateDecl decl, StringBuilder sb, string additionalOffset, bool isParentGeneric)
        {
            // return type
            sb.Append("delegate ");
            AntiParseExpr(decl.Returns, sb, additionalOffset);
            sb.Append(' ');
            sb.Append($"{GetNameFromAst(decl.Name, _compiler.MessageHandler).GetClassNameWithoutNamespace()}");

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

            sb.Append(";\n");
        }

        private void CreateFuncDecl(AstFuncDecl decl, StringBuilder sb, string additionalOffset, bool isParentGeneric)
        {
            // return type
            AntiParseExpr(decl.Returns, sb, additionalOffset);
            sb.Append(' ');

            // add additional string
            if (decl.Name.AdditionalData != null)
            {
                AntiParseExpr(decl.Name.AdditionalData, sb, additionalOffset);
                sb.Append('.');
            }
            AntiParseExpr(decl.Name.GetCopy(decl.Name.Name), sb, additionalOffset);

            sb.Append('(');
            for (int i = 0; i < decl.Parameters.Count; ++i)
            {
                var par = decl.Parameters[i];
                if (par.ParameterModificator == HapetFrontend.Enums.ParameterModificator.Arglist)
                {
                    sb.Append("arglist");
                }
                else
                {
                    if (par.ParameterModificator == HapetFrontend.Enums.ParameterModificator.Ref)
                        sb.Append("ref ");
                    else if (par.ParameterModificator == HapetFrontend.Enums.ParameterModificator.Out)
                        sb.Append("out ");
                    else if (par.ParameterModificator == HapetFrontend.Enums.ParameterModificator.Params)
                        sb.Append("params ");

                    AntiParseExpr(par.Type, sb, additionalOffset);
                    sb.Append(' ');
                    AntiParseExpr(par.Name, sb, additionalOffset);
                } 

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

            // add additional string
            if (decl.Name.AdditionalData != null)
            {
                AntiParseExpr(decl.Name.AdditionalData, sb, additionalOffset);
                sb.Append('.');
            }

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

            // TODO: generic constraiins 

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
                sb.Append($"{additionalOffset}}}");
            }
            else if (decl.HasSet)
                sb.Append("set; }");
            else
            {
                if (decl.GetBlock != null && (decl.HasGenericTypes ||
                    (isParentGeneric && !decl.SpecialKeys.Contains(TokenType.KwStatic))))
                    sb.Append($"{additionalOffset}}}");
                else
                    sb.Append("}");
            }

            if (decl.Initializer != null)
            {
                sb.Append(" = ");
                AntiParseExpr(decl.Initializer, sb, additionalOffset);
                sb.Append(";\n");
            }
            else
            {
                sb.Append("\n");
            }
        }

        private void CreateFieldDecl(AstVarDecl decl, StringBuilder sb, string additionalOffset)
        {
            // return type
            AntiParseExpr(decl.Type, sb, additionalOffset);
            sb.Append(' ');

            // add additional string
            if (decl.Name.AdditionalData != null)
            {
                AntiParseExpr(decl.Name.AdditionalData, sb, additionalOffset);
                sb.Append('.');
            }
            AntiParseExpr(decl.Name, sb, additionalOffset);

            if (decl.Initializer != null)
            {
                sb.Append(" = ");
                AntiParseExpr(decl.Initializer, sb, additionalOffset);
            }

            sb.Append(";\n");
        }

        private void CreateDocString(string doc, StringBuilder sb, string additionalOffset)
        {
            if (!string.IsNullOrWhiteSpace(doc))
                foreach (var sp in doc.Split(Environment.NewLine))
                {
                    if (!string.IsNullOrWhiteSpace(sp))
                        sb.Append($"{additionalOffset}/// {sp}\n");
                }
        }

        /// <summary>
        /// Inversed func of <see cref="GetAstIdFromName"/>
        /// </summary>
        /// <param name="idExpr"></param>
        /// <returns></returns>
        private string GetNameFromAst(AstIdExpr idExpr, IMessageHandler messageHandler)
        {
            if (idExpr is not AstIdGenericExpr genId)
                return idExpr.Name;

            StringBuilder sb = new StringBuilder("<");
            for (int i = 0; i < genId.GenericRealTypes.Count; ++i)
            {
                var g = genId.GenericRealTypes[i];
                AntiParseExpr(g, sb, string.Empty);
                if (i < genId.GenericRealTypes.Count - 1)
                    sb.Append(", ");
            }
            sb.Append('>');
            return $"{genId.Name}{sb}";
        }
    }
}
