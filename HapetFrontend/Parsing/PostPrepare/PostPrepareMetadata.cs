using HapetFrontend.Ast.Declarations;
using HapetFrontend.Entities;
using Newtonsoft.Json;

namespace HapetFrontend.Parsing.PostPrepare
{
    public partial class PostPrepare
    {
        public List<AstClassDecl> AllClassesMetadata { get; } = new List<AstClassDecl>();
		public List<AstStructDecl> AllStructsMetadata { get; } = new List<AstStructDecl>();
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
                    }
                    else if (stmt is AstStructDecl structDecl)
                    {
                        // creating a new struct name with namespace
                        string newClassName = $"{file.Namespace}.{structDecl.Name.Name}";
                        structDecl.Name = structDecl.Name.GetCopy(newClassName);

                        file.NamespaceScope.DefineDeclSymbol(structDecl.Name.Name, structDecl);
						AllStructsMetadata.Add(structDecl);
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
                            PostPrepareVarInference(decl);
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
        public List<FuncDeclJson> FuncDecls { get; set; }
    }
}
