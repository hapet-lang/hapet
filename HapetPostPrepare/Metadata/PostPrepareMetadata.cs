using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Enums;
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
        public List<AstFuncDecl> AllFunctionsMetadata { get; set; } = new List<AstFuncDecl>();
        public List<AstGenericDecl> AllGenericsMetadata { get; } = new List<AstGenericDecl>();

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
            AllPostPrepareMetadataDelegates();
            AllPostPrepareMetadataNestedTypes();
            AllPostPrepareMetadataFunctions();
            AllPostPrepareMetadataTypeFieldDecls();
            AllPostPrepareMetadataInheritance();
            AllPostPrepareMetadataInheritedFunctions();
            AllPostPrepareMetadataTypeInheritedFieldDecls();
            AllPostPrepareMetadataTypeInheritedPropsDecls();
            AllPostPrepareMetadataNestedTypesInside();
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
            var step = PreparationStep.Types;
            _currentPreparationStep = step;

            foreach (var (path, file) in _compiler.GetFiles())
            {
                _currentSourceFile = file;
                foreach (var stmt in file.Statements)
                {
                    stmt.CurrentPreparationStep = step;

                    // do not serialize imported shite
                    var needSerialize = (!(stmt as AstDeclaration)?.IsImported ?? false);
                    PostPrepareMetadataTypes(stmt, needSerialize);
                }
            }
        }

        private void AllPostPrepareMetadataGenerics()
        {
            var step = PreparationStep.Generics;
            _currentPreparationStep = step;

            // resolve generic shite of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                cls.CurrentPreparationStep = step;

                _currentSourceFile = cls.SourceFile;
                _currentParentStack.AddParent(cls);
                bool _ = PostPrepareMetadataGenerics(cls);
                _currentParentStack.RemoveParent();
            }
            // resolve generic shite of structs
            foreach (var str in AllStructsMetadata.ToList())
            {
                str.CurrentPreparationStep = step;

                _currentSourceFile = str.SourceFile;
                _currentParentStack.AddParent(str);
                bool _ = PostPrepareMetadataGenerics(str);
                _currentParentStack.RemoveParent();
            }
            // resolve generic shite of delegates
            foreach (var del in AllDelegatesMetadata.ToList())
            {
                del.CurrentPreparationStep = step;

                _currentSourceFile = del.SourceFile;
                _currentParentStack.AddParent(del);
                bool _ = PostPrepareMetadataGenerics(del);
                _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataDelegates()
        {
            var step = PreparationStep.Delegates;
            _currentPreparationStep = step;

            // inferrencing delegates
            foreach (var del in AllDelegatesMetadata.ToList())
            {
                del.CurrentPreparationStep = step;

                _currentSourceFile = del.SourceFile;
                _currentParentStack.AddParent(del);
                PostPrepareMetadataDelegates(del);
                _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataNestedTypes()
        {
            var step = PreparationStep.NestedTypes;
            _currentPreparationStep = step;

            // inferrencing nested types
            foreach (var cls in AllClassesMetadata.ToList())
            {
                cls.CurrentPreparationStep = step;

                _currentSourceFile = cls.SourceFile;
                _currentParentStack.AddParent(cls);

                PostPrepareMetadataNestedTypes(cls);
                _currentParentStack.RemoveParent();
            }
            foreach (var str in AllStructsMetadata.ToList())
            {
                str.CurrentPreparationStep = step;

                _currentSourceFile = str.SourceFile;
                _currentParentStack.AddParent(str);

                PostPrepareMetadataNestedTypes(str);
                _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataFunctions()
        {
            var step = PreparationStep.Functions;
            _currentPreparationStep = step;

            var allFuncs = new List<AstFuncDecl>();
            // inferrencing funcs
            foreach (var cls in AllClassesMetadata.ToList())
            {
                allFuncs.AddRange(cls.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl));
            }
            foreach (var str in AllStructsMetadata.ToList())
            {
                allFuncs.AddRange(str.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl));
            }
            foreach (var gen in AllGenericsMetadata.ToList())
            {
                allFuncs.AddRange(gen.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl));
            }
            foreach (var dlg in AllDelegatesMetadata.ToList())
            {
                allFuncs.AddRange(dlg.Functions);
            }

            foreach (var func in allFuncs)
            {
                if (func.ContainingParent != null)
                    func.ContainingParent.CurrentPreparationStep = step;
                func.CurrentPreparationStep = step;

                _currentSourceFile = func.SourceFile;

                if (func.ContainingParent.IsNestedDecl)
                    _currentParentStack.AddParent(func.ContainingParent.ParentDecl);
                _currentParentStack.AddParent(func.ContainingParent);

                bool isImported = func.IsImported;
                PostPrepareMetadataFunctions(func, !isImported);

                _currentParentStack.RemoveParent();
                if (func.ContainingParent.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataTypeFieldDecls()
        {
            var step = PreparationStep.FieldAndPropDecls;
            _currentPreparationStep = step;

            // resolve all fields of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                cls.CurrentPreparationStep = step;

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
                str.CurrentPreparationStep = step;

                _currentSourceFile = str.SourceFile;
                if (str.IsNestedDecl)
                    _currentParentStack.AddParent(str.ParentDecl);
                _currentParentStack.AddParent(str);
                PostPrepareMetadataTypeFieldDecls(str);
                _currentParentStack.RemoveParent();
                if (str.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            foreach (var gen in AllGenericsMetadata.ToList())
            {
                gen.CurrentPreparationStep = step;

                _currentSourceFile = gen.SourceFile;
                if (gen.IsNestedDecl)
                    _currentParentStack.AddParent(gen.ParentDecl);
                _currentParentStack.AddParent(gen);
                PostPrepareMetadataTypeFieldDecls(gen);
                _currentParentStack.RemoveParent();
                if (gen.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            foreach (var enm in AllEnumsMetadata.ToList())
            {
                enm.CurrentPreparationStep = step;

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

        /// <summary>
        /// We need to infer all decl at first and only then - their intializers
        /// </summary>
        private void AllPostPrepareMetadataInheritance()
        {
            var step = PreparationStep.Inheritance;
            _currentPreparationStep = step;

            // resolve inheritance shite of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                cls.CurrentPreparationStep = step;

                _currentSourceFile = cls.SourceFile;
                if (cls.IsNestedDecl)
                    _currentParentStack.AddParent(cls.ParentDecl);
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataInheritance(cls);
                _currentParentStack.RemoveParent();
                if (cls.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            // resolve inheritance shite of structs
            foreach (var str in AllStructsMetadata.ToList())
            {
                str.CurrentPreparationStep = step;

                _currentSourceFile = str.SourceFile;
                if (str.IsNestedDecl)
                    _currentParentStack.AddParent(str.ParentDecl);
                _currentParentStack.AddParent(str);
                PostPrepareMetadataInheritance(str);
                _currentParentStack.RemoveParent();
                if (str.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            // resolve inheritance shite of enums
            foreach (var enm in AllEnumsMetadata.ToList())
            {
                enm.CurrentPreparationStep = step;

                _currentSourceFile = enm.SourceFile;
                if (enm.IsNestedDecl)
                    _currentParentStack.AddParent(enm.ParentDecl);
                _currentParentStack.AddParent(enm);
                PostPrepareMetadataInheritance(enm);
                _currentParentStack.RemoveParent();
                if (enm.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataInheritedFunctions()
        {
            var step = PreparationStep.InheritedFunctions;
            _currentPreparationStep = step;

            foreach (var cls in AllClassesMetadata.ToList())
            {
                cls.CurrentPreparationStep = step;

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
                str.CurrentPreparationStep = step;

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
            var step = PreparationStep.InheritedFieldDecls;
            _currentPreparationStep = step;

            var classes = AllClassesMetadata.ToList();
            var structures = AllStructsMetadata.ToList();

            // resolve all inherited fields of classes
            foreach (var cls in classes)
            {
                cls.CurrentPreparationStep = step;

                _currentSourceFile = cls.SourceFile;
                if (cls.IsNestedDecl)
                    _currentParentStack.AddParent(cls.ParentDecl);
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataTypeInheritedFieldDecls(cls);
                _currentParentStack.RemoveParent();
                if (cls.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            foreach (var str in structures)
            {
                str.CurrentPreparationStep = step;

                _currentSourceFile = str.SourceFile;
                if (str.IsNestedDecl)
                    _currentParentStack.AddParent(str.ParentDecl);
                _currentParentStack.AddParent(str);
                PostPrepareMetadataTypeInheritedFieldDecls(str);
                _currentParentStack.RemoveParent();
                if (str.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataTypeInheritedPropsDecls()
        {
            var step = PreparationStep.InheritedPropDecls;
            _currentPreparationStep = step;

            var classes = AllClassesMetadata.ToList();
            var structures = AllStructsMetadata.ToList();

            // resolve all inherited props of classes
            foreach (var cls in classes)
            {
                cls.CurrentPreparationStep = step;

                _currentSourceFile = cls.SourceFile;
                if (cls.IsNestedDecl)
                    _currentParentStack.AddParent(cls.ParentDecl);
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataTypeInheritedPropsDecls(cls);
                _currentParentStack.RemoveParent();
                if (cls.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            foreach (var str in structures)
            {
                str.CurrentPreparationStep = step;

                _currentSourceFile = str.SourceFile;
                if (str.IsNestedDecl)
                    _currentParentStack.AddParent(str.ParentDecl);
                _currentParentStack.AddParent(str);
                PostPrepareMetadataTypeInheritedPropsDecls(str);
                _currentParentStack.RemoveParent();
                if (str.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataNestedTypesInside()
        {
            var step = PreparationStep.NestedTypesInside;
            _currentPreparationStep = step;

            // inferrencing nested types
            foreach (var cls in AllClassesMetadata.ToList())
            {
                cls.CurrentPreparationStep = step;

                _currentSourceFile = cls.SourceFile;
                _currentParentStack.AddParent(cls);

                PostPrepareMetadataNestedTypesInside(cls);
                _currentParentStack.RemoveParent();
            }
            foreach (var str in AllStructsMetadata.ToList())
            {
                str.CurrentPreparationStep = step;

                _currentSourceFile = str.SourceFile;
                _currentParentStack.AddParent(str);

                PostPrepareMetadataNestedTypesInside(str);
                _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataTypeFieldInits()
        {
            var step = PreparationStep.FieldAndPropInits;
            _currentPreparationStep = step;

            // resolve all fields of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                cls.CurrentPreparationStep = step;

                _currentSourceFile = cls.SourceFile;
                if (cls.IsNestedDecl)
                    _currentParentStack.AddParent(cls.ParentDecl);
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataTypeFieldInits(cls);
                _currentParentStack.RemoveParent();
                if (cls.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            // resolve all fields of structs
            foreach (var str in AllStructsMetadata.ToList())
            {
                str.CurrentPreparationStep = step;

                _currentSourceFile = str.SourceFile;
                if (str.IsNestedDecl)
                    _currentParentStack.AddParent(str.ParentDecl);
                _currentParentStack.AddParent(str);
                PostPrepareMetadataTypeFieldInits(str);
                _currentParentStack.RemoveParent();
                if (str.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            foreach (var enm in AllEnumsMetadata.ToList())
            {
                enm.CurrentPreparationStep = step;

                _currentSourceFile = enm.SourceFile;
                if (enm.IsNestedDecl)
                    _currentParentStack.AddParent(enm.ParentDecl);
                _currentParentStack.AddParent(enm);
                PostPrepareMetadataTypeFieldInits(enm);
                _currentParentStack.RemoveParent();
                if (enm.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataAttributes()
        {
            var step = PreparationStep.Attributes;
            _currentPreparationStep = step;

            // inferrencing attribtues of functions
            foreach (var fnc in AllFunctionsMetadata.ToList())
            {
                fnc.CurrentPreparationStep = step;

                if (fnc.ContainingParent.IsNestedDecl)
                    _currentParentStack.AddParent(fnc.ContainingParent.ParentDecl);
                _currentParentStack.AddParent(fnc.ContainingParent);

                _currentSourceFile = fnc.SourceFile;
                _currentParentStack.AddParent(fnc);
                PostPrepareMetadataAttributes(fnc);
                _currentParentStack.RemoveParent();

                _currentParentStack.RemoveParent();
                if (fnc.ContainingParent.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            // inferrencing attribtues of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                cls.CurrentPreparationStep = step;

                _currentSourceFile = cls.SourceFile;
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataAttributes(cls);
                _currentParentStack.RemoveParent();
            }
            // inferrencing attribtues of structs
            foreach (var str in AllStructsMetadata.ToList())
            {
                str.CurrentPreparationStep = step;

                _currentSourceFile = str.SourceFile;
                _currentParentStack.AddParent(str);
                PostPrepareMetadataAttributes(str);
                _currentParentStack.RemoveParent();
            }
            // inferrencing attribtues of enums
            foreach (var enm in AllEnumsMetadata.ToList())
            {
                enm.CurrentPreparationStep = step;

                _currentSourceFile = enm.SourceFile;
                _currentParentStack.AddParent(enm);
                PostPrepareMetadataAttributes(enm);
                _currentParentStack.RemoveParent();
            }
            // inferrencing attribtues of delegates
            foreach (var del in AllDelegatesMetadata.ToList())
            {
                del.CurrentPreparationStep = step;

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
