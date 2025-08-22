using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Enums;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetPostPrepare.Entities;
using Newtonsoft.Json;
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
            StringBuilder globalStringBuilder = new StringBuilder();

            // create #meta block
            CreateMetadataMetadata(globalStringBuilder);

            SortDeclarations();
            foreach (var srt in _sortedDeclsByFiles)
            {
                _currentSourceFile = srt.Key;
                CreateFileDeclarations(srt.Key, srt.Value, globalStringBuilder);
            }

            // WARN: take care about the shite that is goin on here
            var outFolderPath = _compiler.CurrentProjectSettings.OutputDirectory;
            var asmName = _compiler.CurrentProjectSettings.AssemblyName;
            File.WriteAllText($"{outFolderPath}/{asmName}.mpt", globalStringBuilder.ToString());
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

        private void CreateMetadataMetadata(StringBuilder sb)
        {
            // #meta block
            var metadataMetadataJson = new MetadataMetadataJson()
            {
                Name = _compiler.CurrentProjectSettings.ProjectName,
                Version = _compiler.CurrentProjectSettings.ProjectVersion,
                Dependencies = _compiler.CurrentProjectData.References.ToArray(),
            };
            var metadataMetadataText = JsonConvert.SerializeObject(metadataMetadataJson, Formatting.Indented);
            sb.AppendLine("#meta");
            sb.AppendLine(metadataMetadataText);
            sb.AppendLine("#endmeta");
        }

        private void CreateFileDeclarations(ProgramFile file, List<AstDeclaration> decls, StringBuilder sb)
        {
            sb.Append($"#file \"{CompilerUtils.GetFileRelativePath(_compiler.CurrentProjectSettings.ProjectPath, file.Name)}\";\n");
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
            else if (decl is AstEnumDecl enmDecl)
            {
                CreateEnumDecl(enmDecl, sb, additionalOffset);
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

            // generic constraiins 
            CreateGenericConstrains(decl, sb, additionalOffset);

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

            // generic constraiins 
            CreateGenericConstrains(decl, sb, additionalOffset);

            sb.Append(";\n");
        }

        private void CreateEnumDecl(AstEnumDecl decl, StringBuilder sb, string additionalOffset)
        {
           AstNestedExpr inheritedFrom = decl.InheritedType;
            sb.Append("enum ");
            sb.Append($"{GetNameFromAst(decl.Name, _compiler.MessageHandler).GetClassNameWithoutNamespace()} ");

            sb.Append(": ");
            AntiParseExpr(inheritedFrom, sb, additionalOffset);

            List<AstVarDecl> decls = decl.Declarations;
            sb.Append($"\n{additionalOffset}{{\n");

            foreach (var d in decls)
            {
                if (d.SpecialKeys.Contains(TokenType.KwUnreflected))
                    continue;

                // doc string
                CreateDocString(d.Documentation, sb, additionalOffset + _fourSpaces);

                // serialize attributes
                foreach (var attr in d.Attributes)
                {
                    sb.Append(additionalOffset + _fourSpaces);
                    AntiParseExpr(attr, sb, additionalOffset + _fourSpaces);
                }

                sb.Append(additionalOffset + _fourSpaces);
                AntiParseExpr(d.Name, sb, additionalOffset + _fourSpaces);
                if (d.Initializer != null)
                {
                    sb.Append(" = ");
                    AntiParseExpr(d.Initializer, sb, additionalOffset + _fourSpaces);
                }
                sb.Append(",\n");
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

            sb.Append(GetFuncNameAsOriginal(decl));

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

            // generic constraiins 
            CreateGenericConstrains(decl, sb, additionalOffset);

            // if the func is generic && not abstract && not stor - serialize
            // if parent is generic && func not abstract && not stor - serialize
            // if func is not extern - serialize
            if ((decl.HasGenericTypes || isParentGeneric) && 
                !decl.SpecialKeys.Contains(TokenType.KwAbstract) &&
                decl.ClassFunctionType != ClassFunctionType.StaticCtor &&
                !decl.SpecialKeys.Contains(TokenType.KwExtern))
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

            // generic constraiins 
            CreateGenericConstrains(decl, sb, additionalOffset);

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
                    sb.Append('}');
            }

            if (decl.Initializer != null)
            {
                sb.Append(" = ");
                AntiParseExpr(decl.Initializer, sb, additionalOffset);
                sb.Append(";\n");
            }
            else
            {
                sb.Append('\n');
            }
        }

        private void CreateFieldDecl(AstVarDecl decl, StringBuilder sb, string additionalOffset)
        {
            if (decl.IsEvent)
            {
                sb.Append("event ");
                // type without System.Event
                AntiParseExpr(((decl.Type as AstNestedExpr).RightPart as AstIdGenericExpr).GenericRealTypes[0], sb, additionalOffset);
            }
            else
            {
                // type
                AntiParseExpr(decl.Type, sb, additionalOffset);
            }
            sb.Append(' ');

            // add additional string
            if (decl.Name.AdditionalData != null)
            {
                AntiParseExpr(decl.Name.AdditionalData, sb, additionalOffset);
                sb.Append('.');
            }
            AntiParseExpr(decl.Name, sb, additionalOffset);

            // do not add initializer if it is an event
            if (decl.Initializer != null && !decl.IsEvent)
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

        private void CreateGenericConstrains(AstDeclaration decl, StringBuilder sb, string additionalOffset)
        {
            foreach (var c in decl.GenericConstrains)
            {
                sb.Append('\n');
                sb.Append($"{additionalOffset + _fourSpaces}where {c.Key.Name}: ");
                for (int i = 0; i < c.Value.Count; ++i)
                {
                    var cc = c.Value[i];
                    switch (cc.ConstrainType)
                    {
                        case GenericConstrainType.CustomType: AntiParseExpr(cc.Expr, sb, string.Empty); break;
                        case GenericConstrainType.ClassType: sb.Append($"class"); break;
                        case GenericConstrainType.EnumType: sb.Append($"enum"); break;
                        case GenericConstrainType.StructType: sb.Append($"struct"); break;
                        case GenericConstrainType.DelegateType: sb.Append($"delegate"); break;
                        case GenericConstrainType.NewType: 
                            sb.Append($"new(");
                            for (int j = 0; j < cc.AdditionalExprs.Count; ++j)
                            {
                                AntiParseExpr(cc.AdditionalExprs[j], sb, string.Empty);
                                // there are more types
                                if (j + 1 != cc.AdditionalExprs.Count)
                                    sb.Append($", ");
                            }
                            sb.Append(')');
                            break;
                    }

                    // there are more constrains
                    if (i + 1 != c.Value.Count)
                        sb.Append($", ");
                }
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

        public string GetFuncNameAsOriginal(AstFuncDecl decl)
        {
            StringBuilder sb = new StringBuilder();
            // add additional string
            if (decl.Name.AdditionalData != null)
            {
                AntiParseExpr(decl.Name.AdditionalData, sb, string.Empty);
                sb.Append('.');
            }
            if (decl is AstOverloadDecl over &&
                (over.OverloadType == OverloadType.UnaryOperator || over.OverloadType == OverloadType.BinaryOperator))
                sb.Append($"operator {over.Operator}");
            else
                AntiParseExpr(decl.Name.GetCopy(decl.Name.Name), sb, string.Empty);
            return sb.ToString();
        }
    }
}
