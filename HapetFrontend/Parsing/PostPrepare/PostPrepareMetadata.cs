using HapetFrontend.Ast.Declarations;
using Newtonsoft.Json;

namespace HapetFrontend.Parsing.PostPrepare
{
    public partial class PostPrepare
    {
        private List<AstClassDecl> _allClasses = new List<AstClassDecl>();
        private List<AstStructDecl> _allStructs = new List<AstStructDecl>();
        private List<AstFuncDecl> _allFunctions = new List<AstFuncDecl>();

        // TODO: some changes should be done in the file when impl 'using' and class inheritance

        private void PostPrepareMetadata()
        {
            PostPrepareMetadataTypes();
            PostPrepareMetadataTypeFields();
            PostPrepareMetadataFunctions();

            // creating the file
            PostPrepareMetadataCreate();
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
                        // creating a new class name with namespace
                        string newClassName = $"{file.Namespace}.{classDecl.Name.Name}";
                        classDecl.Name = classDecl.Name.GetCopy(newClassName);

                        file.FileScope.DefineDeclSymbol(classDecl.Name.Name, classDecl);
                        _allClasses.Add(classDecl);
                    }
                    else if (stmt is AstStructDecl structDecl)
                    {
                        // creating a new struct name with namespace
                        string newClassName = $"{file.Namespace}.{structDecl.Name.Name}";
                        structDecl.Name = structDecl.Name.GetCopy(newClassName);

                        file.FileScope.DefineDeclSymbol(structDecl.Name.Name, structDecl);
                        _allStructs.Add(structDecl);
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
                        foreach (var decl in classDecl.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl))
                        {
                            PostPrepareFunctionInference(decl, true);
                            _allFunctions.Add(decl);
                        }
                    }
                }
            }
        }

        private void PostPrepareMetadataCreate()
        {
            // TODO: probably should be sorted somehow by inheritance, idk
            MetadataJson metadata = new MetadataJson();
            metadata.ClassDecls = _allClasses.Select(x => x.GetJson()).ToList();
            metadata.StructDecls = _allStructs.Select(x => x.GetJson()).ToList();
            metadata.FuncDecls = _allFunctions.Select(x => x.GetJson()).ToList();

            // WARN: take care about the shite that is goin on here
            var sz = JsonConvert.SerializeObject(metadata);
            var outFolderPath = _compiler.CurrentProjectSettings.OutputDirectory;
            var projectName = _compiler.CurrentProjectSettings.ProjectName;
            File.WriteAllText($"{outFolderPath}/{projectName}.json", sz);
        }
    }

    internal class MetadataJson
    {
        public List<ClassDeclJson> ClassDecls { get; set; }
        public List<StructDeclJson> StructDecls { get; set; }
        public List<FuncDeclJson> FuncDecls { get; set; }
    }
}
