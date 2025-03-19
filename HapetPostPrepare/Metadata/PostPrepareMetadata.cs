using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetPostPrepare.Entities;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public List<AstClassDecl> AllClassesMetadata { get; } = new List<AstClassDecl>();
        public List<AstStructDecl> AllStructsMetadata { get; } = new List<AstStructDecl>();
        public List<AstEnumDecl> AllEnumsMetadata { get; } = new List<AstEnumDecl>();
        public List<AstDelegateDecl> AllDelegatesMetadata { get; } = new List<AstDelegateDecl>();
        public List<AstFuncDecl> AllFunctionsMetadata { get; } = new List<AstFuncDecl>();

        private List<AstClassDecl> _serializeClassesMetadata { get; } = new List<AstClassDecl>();
        private List<AstStructDecl> _serializeStructsMetadata { get; } = new List<AstStructDecl>();
        private List<AstEnumDecl> _serializeEnumsMetadata { get; } = new List<AstEnumDecl>();
        private List<AstDelegateDecl> _serializeDelegatesMetadata { get; } = new List<AstDelegateDecl>();
        private List<AstFuncDecl> _serializeFunctionsMetadata { get; } = new List<AstFuncDecl>();

        private PreparationStep _currentPreparationStep { get; set; } = PreparationStep.None;

        private int PostPrepareMetadata()
        {
            AllPostPrepareMetadataTypes();
            AllPostPrepareMetadataGenerics();
            AllPostPrepareMetadataInheritance();
            AllPostPrepareMetadataDelegates();
            AllPostPrepareMetadataFunctions();
            AllPostPrepareMetadataInheritedFunctions();
            AllPostPrepareMetadataTypeFieldDecls();
            AllPostPrepareMetadataTypeInheritedFieldDecls();
            AllPostPrepareMetadataTypeInheritedPropsDecls();
            AllPostPrepareMetadataTypeFieldInits();
            AllPostPrepareMetadataAttributes();

            // if there were errors while preparing for metafile
            if (_compiler.MessageHandler.HasErrors)
            {
                return (int)CompilerErrors.PostPrepareMetafileError; // post prepare errors
            }

            // creating the file
            GenerateMetadataFile();

            // WARN: removing all properties after saving to file
            // removing them only now because we need them to be presented in metadata
            /// unwrapping props is done in <see cref="PostPrepareClassProperties"/>
            RemoveAllProperties();

            return 0;
        }

        private void AllPostPrepareMetadataTypes()
        {
            _currentPreparationStep = PreparationStep.Types;

            foreach (var (path, file) in _compiler.GetFiles())
            {
                _currentSourceFile = file;
                foreach (var stmt in file.Statements)
                {
                    /// DO NOT SERIALIZE PURE GENERICS - SERIALIZE T-LIKE GENERICS <see cref="AllPostPrepareMetadataGenerics"/>
                    bool needSerialize = !(stmt is AstClassDecl classDecl && classDecl.HasGenericTypes);
                    // do not serialize imported shite
                    needSerialize = needSerialize && (!(stmt as AstDeclaration)?.IsImported ?? false);

                    PostPrepareMetadataTypes(stmt, needSerialize);
                }
            }
        }

        private void AllPostPrepareMetadataGenerics()
        {
            _currentPreparationStep = PreparationStep.Generics;

            // resolve inheritance shite of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                bool itWasGeneric = PostPrepareMetadataGenerics(cls, out var realDecl);

                // do not inference generics
                if (itWasGeneric)
                {
                    // append for serialization T-like (if not imported)
                    if (!cls.IsImported)
                        _serializeClassesMetadata.Add(realDecl as AstClassDecl);
                    // remove from inferencing
                    AllClassesMetadata.Remove(cls);
                }
            }
        }

        private void AllPostPrepareMetadataInheritance()
        {
            _currentPreparationStep = PreparationStep.Inheritance;

            // resolve inheritance shite of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataInheritance(cls);
            }
            // resolve inheritance shite of structs
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                PostPrepareMetadataInheritance(str);
            }
            // resolve inheritance shite of enums
            foreach (var enm in AllEnumsMetadata.ToList())
            {
                _currentSourceFile = enm.SourceFile;
                PostPrepareMetadataInheritance(enm);
            }
        }

        private void AllPostPrepareMetadataDelegates()
        {
            _currentPreparationStep = PreparationStep.Delegates;

            // inferrencing delegates
            foreach (var del in AllDelegatesMetadata.ToList())
            {
                _currentSourceFile = del.SourceFile;
                PostPrepareMetadataDelegates(del);
            }
        }

        private void AllPostPrepareMetadataFunctions()
        {
            _currentPreparationStep = PreparationStep.Functions;

            // inferrencing funcs
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;

                bool isImported = cls.IsImported;
                PostPrepareMetadataFunctions(cls, !isImported, isImported, true);
            }
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;

                bool isImported = str.IsImported;
                PostPrepareMetadataFunctions(str, !isImported, isImported, true);
            }
        }

        private void AllPostPrepareMetadataInheritedFunctions()
        {
            _currentPreparationStep = PreparationStep.InheritedFunctions;

            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataInheritedFunctions(cls);
            }
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                PostPrepareMetadataInheritedFunctions(str);
            }
        }

        /// <summary>
        /// We need to infer all decl at first and only then - their intializers
        /// </summary>
        private void AllPostPrepareMetadataTypeFieldDecls()
        {
            _currentPreparationStep = PreparationStep.FieldAndPropDecls;

            // resolve all fields of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataTypeFieldDecls(cls);
            }
            // resolve all fields of structs
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                PostPrepareMetadataTypeFieldDecls(str);
            }
            foreach (var enm in AllEnumsMetadata.ToList())
            {
                _currentSourceFile = enm.SourceFile;
                PostPrepareMetadataTypeFieldDecls(enm);
            }
        }

        private void AllPostPrepareMetadataTypeInheritedFieldDecls()
        {
            _currentPreparationStep = PreparationStep.InheritedFieldDecls;

            var classes = AllClassesMetadata.ToList();
            var structures = AllStructsMetadata.ToList();

            // resolve all inherited fields of classes
            foreach (var cls in classes)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataTypeInheritedFieldDecls(cls);
            }
            foreach (var str in structures)
            {
                _currentSourceFile = str.SourceFile;
                PostPrepareMetadataTypeInheritedFieldDecls(str);
            }

            foreach (var cls in classes)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataTypeInheritedFieldDeclsCopy(cls);
            }
            foreach (var str in structures)
            {
                _currentSourceFile = str.SourceFile;
                PostPrepareMetadataTypeInheritedFieldDeclsCopy(str);
            }
        }

        private void AllPostPrepareMetadataTypeInheritedPropsDecls()
        {
            _currentPreparationStep = PreparationStep.InheritedPropDecls;

            var classes = AllClassesMetadata.ToList();
            var structures = AllStructsMetadata.ToList();

            // resolve all inherited props of classes
            foreach (var cls in classes)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataTypeInheritedPropsDecls(cls);
            }
            foreach (var str in structures)
            {
                _currentSourceFile = str.SourceFile;
                PostPrepareMetadataTypeInheritedPropsDecls(str);
            }

            foreach (var cls in classes)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataTypeInheritedPropsDeclsCopy(cls);
            }
            foreach (var str in structures)
            {
                _currentSourceFile = str.SourceFile;
                PostPrepareMetadataTypeInheritedPropsDeclsCopy(str);
            }
        }

        private void AllPostPrepareMetadataTypeFieldInits()
        {
            _currentPreparationStep = PreparationStep.FieldAndPropInits;

            // resolve all fields of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataTypeFieldInits(cls);
            }
            // resolve all fields of structs
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                PostPrepareMetadataTypeFieldInits(str);
            }
            foreach (var enm in AllEnumsMetadata.ToList())
            {
                _currentSourceFile = enm.SourceFile;
                PostPrepareMetadataTypeFieldInits(enm);
            }
        }

        private void AllPostPrepareMetadataAttributes()
        {
            _currentPreparationStep = PreparationStep.Attributes;

            // inferrencing attribtues of functions
            foreach (var fnc in AllFunctionsMetadata.ToList())
            {
                _currentSourceFile = fnc.SourceFile;
                PostPrepareMetadataAttributes(fnc);
            }
            // inferrencing attribtues of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataAttributes(cls);
            }
            // inferrencing attribtues of structs
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                PostPrepareMetadataAttributes(str);
            }
            // inferrencing attribtues of enums
            foreach (var enm in AllEnumsMetadata.ToList())
            {
                _currentSourceFile = enm.SourceFile;
                PostPrepareMetadataAttributes(enm);
            }
            // inferrencing attribtues of delegates
            foreach (var del in AllDelegatesMetadata.ToList())
            {
                _currentSourceFile = del.SourceFile;
                PostPrepareMetadataAttributes(del);
            }
        }

        private void RemoveAllProperties()
        {
            // obsolete but keep it here
            _currentPreparationStep = PreparationStep.PropsRemoval;
        }
    }
}
