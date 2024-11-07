using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Types;
using Newtonsoft.Json;

namespace HapetFrontend.Parsing.PostPrepare
{
    public partial class PostPrepare
    {
        public List<AstClassDecl> AllClassesMetadata { get; } = new List<AstClassDecl>();
		public List<AstStructDecl> AllStructsMetadata { get; } = new List<AstStructDecl>();
		public List<AstEnumDecl> AllEnumsMetadata { get; } = new List<AstEnumDecl>();
		public List<AstFuncDecl> AllFunctionsMetadata { get; } = new List<AstFuncDecl>();

        // TODO: some changes should be done in the file when impl 'using' and class inheritance

        private int PostPrepareMetadata()
        {
            PostPrepareMetadataTypes();
            PostPrepareMetadataFunctions();
            PostPrepareMetadataTypeFields();

            // if there were errors while preparing for metafile
			if (_compiler.MessageHandler.HasErrors)
			{
				return (int)CompilerErrors.PostPrepareMetafileError; // post prepare errors
			}

			// creating the file
			PostPrepareMetadataCreate();

			// WARN: removing all properties after saving to file
			/// unwrapping props is done in <see cref="PostPrepareClassProperties"/>
			RemoveAllProperties();

			return 0;
        }

        private void PostPrepareMetadataTypes()
        {
            foreach (var (path, file) in _compiler.GetFiles())
            {
                _currentSourceFile = file;
                foreach (var stmt in file.Statements)
                {
                    if (stmt is AstClassDecl classDecl)
                    {
						_currentClass = classDecl;

						// creating a new class name with namespace
						string newClassName = $"{file.Namespace}.{classDecl.Name.Name}";
                        classDecl.Name = classDecl.Name.GetCopy(newClassName);
                        file.NamespaceScope.DefineDeclSymbol(classDecl.Name.Name, classDecl);
						AllClassesMetadata.Add(classDecl);

                        // inferencing attrs
                        foreach (var a in classDecl.Attributes)
                        {
                            PostPrepareExprInference(a);
                        }
                    }
                    else if (stmt is AstStructDecl structDecl)
                    {
                        // creating a new struct name with namespace
                        string newClassName = $"{file.Namespace}.{structDecl.Name.Name}";
                        structDecl.Name = structDecl.Name.GetCopy(newClassName);
                        file.NamespaceScope.DefineDeclSymbol(structDecl.Name.Name, structDecl);
						AllStructsMetadata.Add(structDecl);

                        // inferencing attrs
                        foreach (var a in structDecl.Attributes)
                        {
                            PostPrepareExprInference(a);
                        }
                    }
					else if (stmt is AstEnumDecl enumDecl)
					{
						// creating a new enum name with namespace
						string newClassName = $"{file.Namespace}.{enumDecl.Name.Name}";
						enumDecl.Name = enumDecl.Name.GetCopy(newClassName);
						file.NamespaceScope.DefineDeclSymbol(enumDecl.Name.Name, enumDecl);
						AllEnumsMetadata.Add(enumDecl);

						// inferencing attrs
						foreach (var a in enumDecl.Attributes)
						{
							PostPrepareExprInference(a);
						}
					}
				}
            }
        }

        private void PostPrepareMetadataTypeFields()
        {
            foreach (var (path, file) in _compiler.GetFiles())
            {
                _currentSourceFile = file;
                foreach (var stmt in file.Statements)
                {
                    if (stmt is AstClassDecl classDecl)
                    {
						_currentClass = classDecl;
						// infer fields and props at first
						foreach (var decl in classDecl.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
                        {
                            // field or property
                            PostPrepareVarInference(decl, true);
                        }
                    }
                    else if (stmt is AstStructDecl structDecl)
                    {
						// infer fields at first
						foreach (var decl in structDecl.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
                        {
                            // field 
                            PostPrepareVarInference(decl);
						}
					}
					else if (stmt is AstEnumDecl enumDecl)
					{
                        // TODO: check here [Flags] attribute so generate 0, 1, 2, 4 and etc. 
                        // Also check is the amount of flags is not bigger than 32

                        // generating all the values of fields
                        int currentValue = 0;
                        List<int> allValues = new List<int>(enumDecl.Declarations.Count);

						// infer fields at first
						foreach (var decl in enumDecl.Declarations)
						{
							// field 
							PostPrepareVarInference(decl);
                            if (decl.Initializer == null)
                            {
                                decl.Initializer = new AstNumberExpr(NumberData.FromInt(currentValue));
                                // warn if the value already exists in enum
                                if (allValues.Contains(currentValue))
                                {
									_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl, "Enum field with the same value already exists", null, ReportType.Warning);
								}
                                allValues.Add(currentValue);
								currentValue++;
							}
                            else
                            {
                                if (decl.Initializer.OutValue == null)
                                {
									_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Initializer, "The initializer has to be compile time evaluated!");
                                    continue;
								}
                                else if (decl.Initializer.OutValue is not NumberData)
                                {
									_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Initializer, "The initializer has to be numeric type");
									continue;
								}
                                var userDefinedValue = (int)((NumberData)decl.Initializer.OutValue).IntValue;
								// warn if the value already exists in enum
								if (allValues.Contains(userDefinedValue))
								{
									_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl, "Enum field with the same value already exists", null, ReportType.Warning);
								}
								allValues.Add(userDefinedValue);
								currentValue = userDefinedValue + 1; // getting value for the next field
							}
						}
					}
				}
            }
        }

        private void PostPrepareMetadataFunctions()
        {
            foreach (var (path, file) in _compiler.GetFiles())
            {
                _currentSourceFile = file;
                foreach (var stmt in file.Statements)
                {
                    if (stmt is AstClassDecl classDecl)
                    {
						_currentClass = classDecl;
						foreach (var decl in classDecl.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl))
                        {
                            PostPrepareFunctionInference(decl, true);
							AllFunctionsMetadata.Add(decl);
                        }
                    }
                }
            }
        }

        private void PostPrepareMetadataCreate()
        {
			var projectVersion= _compiler.CurrentProjectSettings.ProjectVersion;

			// TODO: probably should be sorted somehow by inheritance, idk
			MetadataJson metadata = new MetadataJson();
            metadata.Version = projectVersion;
            // serialize all unreflected
            metadata.ClassDecls = AllClassesMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();
            metadata.StructDecls = AllStructsMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();
            metadata.EnumDecls = AllEnumsMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();
            metadata.FuncDecls = AllFunctionsMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();

            // WARN: take care about the shite that is goin on here
            var sz = JsonConvert.SerializeObject(metadata, Formatting.Indented);
            var outFolderPath = _compiler.CurrentProjectSettings.OutputDirectory;
            var projectName = _compiler.CurrentProjectSettings.ProjectName;
            File.WriteAllText($"{outFolderPath}/{projectName}.json", sz);
        }

        private void RemoveAllProperties()
        {
            foreach (var cls in AllClassesMetadata)
            {
                cls.Declarations.RemoveAll(x => x is AstPropertyDecl);
            }
        }
    }

    internal class MetadataJson
    {
        public string Version { get; set; }
        public List<ClassDeclJson> ClassDecls { get; set; }
        public List<StructDeclJson> StructDecls { get; set; }
        public List<EnumDeclJson> EnumDecls { get; set; }
        public List<FuncDeclJson> FuncDecls { get; set; }
    }
}
