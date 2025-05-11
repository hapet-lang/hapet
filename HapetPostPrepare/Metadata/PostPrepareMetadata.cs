using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Helpers;
using HapetPostPrepare.Entities;
using System;

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

        private List<AstDeclaration> _allPureGenericTypes { get; } = new List<AstDeclaration>();

        private PreparationStep _currentPreparationStep { get; set; } = PreparationStep.None;

        private int PostPrepareMetadata()
        {
            AllPostPrepareMetadataEmbededTypes();
            AllPostPrepareMetadataTypes();
            AllPostPrepareMetadataGenerics();
            AllPostPrepareMetadataInheritance();
            AllPostPrepareMetadataDelegates();
            AllPostPrepareMetadataNestedTypes();
            AllPostPrepareMetadataFunctions();
            AllPostPrepareMetadataTypeFieldDecls();
            AllPostPrepareMetadataInheritedFunctions();
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

        private void AllPostPrepareMetadataEmbededTypes()
        {
            PostPrepareMetadataEmbededTypes();
        }

        private void AllPostPrepareMetadataTypes()
        {
            _currentPreparationStep = PreparationStep.Types;

            foreach (var (path, file) in _compiler.GetFiles())
            {
                _currentSourceFile = file;
                foreach (var stmt in file.Statements)
                {
                    // do not serialize imported shite
                    var needSerialize = (!(stmt as AstDeclaration)?.IsImported ?? false);
                    PostPrepareMetadataTypes(stmt, needSerialize);
                }
            }
        }

        private void AllPostPrepareMetadataGenerics()
        {
            _currentPreparationStep = PreparationStep.Generics;

            // resolve generic shite of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                _currentParentStack.AddParent(cls);
                bool _ = PostPrepareMetadataGenerics(cls);
                _currentParentStack.RemoveParent();
            }
            // resolve generic shite of structs
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                _currentParentStack.AddParent(str);
                bool _ = PostPrepareMetadataGenerics(str);
                _currentParentStack.RemoveParent();
            }
            // resolve generic shite of delegates
            foreach (var del in AllDelegatesMetadata.ToList())
            {
                _currentSourceFile = del.SourceFile;
                _currentParentStack.AddParent(del);
                bool _ = PostPrepareMetadataGenerics(del);
                _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataInheritance()
        {
            _currentPreparationStep = PreparationStep.Inheritance;

            // resolve inheritance shite of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataInheritance(cls);
                _currentParentStack.RemoveParent();
            }
            // resolve inheritance shite of structs
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                _currentParentStack.AddParent(str);
                PostPrepareMetadataInheritance(str);
                _currentParentStack.RemoveParent();
            }
            // resolve inheritance shite of enums
            foreach (var enm in AllEnumsMetadata.ToList())
            {
                _currentSourceFile = enm.SourceFile;
                _currentParentStack.AddParent(enm);
                PostPrepareMetadataInheritance(enm);
                _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataDelegates()
        {
            _currentPreparationStep = PreparationStep.Delegates;

            // inferrencing delegates
            foreach (var del in AllDelegatesMetadata.ToList())
            {
                _currentSourceFile = del.SourceFile;
                _currentParentStack.AddParent(del);
                PostPrepareMetadataDelegates(del);
                _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataNestedTypes()
        {
            _currentPreparationStep = PreparationStep.NestedTypes;

            // inferrencing nested types
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                _currentParentStack.AddParent(cls);

                PostPrepareMetadataNestedTypes(cls);
                _currentParentStack.RemoveParent();
            }
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                _currentParentStack.AddParent(str);

                PostPrepareMetadataNestedTypes(str);
                _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataFunctions()
        {
            _currentPreparationStep = PreparationStep.Functions;

            // inferrencing funcs
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                if (cls.IsNestedDecl)
                    _currentParentStack.AddParent(cls.ParentDecl);
                _currentParentStack.AddParent(cls);

                bool isImported = cls.IsImported;
                PostPrepareMetadataFunctions(cls, !isImported, isImported);
                _currentParentStack.RemoveParent();
                if (cls.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                if (str.IsNestedDecl)
                    _currentParentStack.AddParent(str.ParentDecl);
                _currentParentStack.AddParent(str);

                bool isImported = str.IsImported;
                PostPrepareMetadataFunctions(str, !isImported, isImported);
                _currentParentStack.RemoveParent();
                if (str.IsNestedDecl)
                    _currentParentStack.RemoveParent();
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
                if (cls.IsNestedDecl)
                    _currentParentStack.AddParent(cls.ParentDecl);
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataTypeFieldDecls(cls);
                _currentParentStack.RemoveParent();
                if (cls.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            // resolve all fields of structs
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                if (str.IsNestedDecl)
                    _currentParentStack.AddParent(str.ParentDecl);
                _currentParentStack.AddParent(str);
                PostPrepareMetadataTypeFieldDecls(str);
                _currentParentStack.RemoveParent();
                if (str.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            foreach (var enm in AllEnumsMetadata.ToList())
            {
                _currentSourceFile = enm.SourceFile;
                if (enm.IsNestedDecl)
                    _currentParentStack.AddParent(enm.ParentDecl);
                _currentParentStack.AddParent(enm);
                PostPrepareMetadataTypeFieldDecls(enm);
                _currentParentStack.RemoveParent();
                if (enm.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataInheritedFunctions()
        {
            _currentPreparationStep = PreparationStep.InheritedFunctions;

            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                if (cls.IsNestedDecl)
                    _currentParentStack.AddParent(cls.ParentDecl);
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataInheritedFunctions(cls);
                _currentParentStack.RemoveParent();
                if (cls.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                if (str.IsNestedDecl)
                    _currentParentStack.AddParent(str.ParentDecl);
                _currentParentStack.AddParent(str);
                PostPrepareMetadataInheritedFunctions(str);
                _currentParentStack.RemoveParent();
                if (str.IsNestedDecl)
                    _currentParentStack.RemoveParent();
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
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataTypeInheritedFieldDecls(cls);
                _currentParentStack.RemoveParent();
            }
            foreach (var str in structures)
            {
                _currentSourceFile = str.SourceFile;
                _currentParentStack.AddParent(str);
                PostPrepareMetadataTypeInheritedFieldDecls(str);
                _currentParentStack.RemoveParent();
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
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataTypeInheritedPropsDecls(cls);
                _currentParentStack.RemoveParent();
            }
            foreach (var str in structures)
            {
                _currentSourceFile = str.SourceFile;
                _currentParentStack.AddParent(str);
                PostPrepareMetadataTypeInheritedPropsDecls(str);
                _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataTypeFieldInits()
        {
            _currentPreparationStep = PreparationStep.FieldAndPropInits;

            // resolve all fields of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataTypeFieldInits(cls);
                _currentParentStack.RemoveParent();
            }
            // resolve all fields of structs
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                _currentParentStack.AddParent(str);
                PostPrepareMetadataTypeFieldInits(str);
                _currentParentStack.RemoveParent();
            }
            foreach (var enm in AllEnumsMetadata.ToList())
            {
                _currentSourceFile = enm.SourceFile;
                _currentParentStack.AddParent(enm);
                PostPrepareMetadataTypeFieldInits(enm);
                _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataAttributes()
        {
            _currentPreparationStep = PreparationStep.Attributes;

            // inferrencing attribtues of functions
            foreach (var fnc in AllFunctionsMetadata.ToList())
            {
                _currentSourceFile = fnc.SourceFile;
                _currentParentStack.AddParent(fnc);
                PostPrepareMetadataAttributes(fnc);
                _currentParentStack.RemoveParent();
            }
            // inferrencing attribtues of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataAttributes(cls);
                _currentParentStack.RemoveParent();
            }
            // inferrencing attribtues of structs
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                _currentParentStack.AddParent(str);
                PostPrepareMetadataAttributes(str);
                _currentParentStack.RemoveParent();
            }
            // inferrencing attribtues of enums
            foreach (var enm in AllEnumsMetadata.ToList())
            {
                _currentSourceFile = enm.SourceFile;
                _currentParentStack.AddParent(enm);
                PostPrepareMetadataAttributes(enm);
                _currentParentStack.RemoveParent();
            }
            // inferrencing attribtues of delegates
            foreach (var del in AllDelegatesMetadata.ToList())
            {
                _currentSourceFile = del.SourceFile;
                _currentParentStack.AddParent(del);
                PostPrepareMetadataAttributes(del);
                _currentParentStack.RemoveParent();
            }
        }

        private void RemoveAllProperties()
        {
            // obsolete but keep it here
            _currentPreparationStep = PreparationStep.PropsRemoval;
        }
    }
}
