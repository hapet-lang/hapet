using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Entities;
using HapetFrontend.Helpers;
using HapetPostPrepare.Entities;
using System;
using System.Runtime;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public List<AstClassDecl> AllClassesMetadata { get; } = new List<AstClassDecl>();
        public List<AstStructDecl> AllStructsMetadata { get; } = new List<AstStructDecl>();
        public List<AstEnumDecl> AllEnumsMetadata { get; } = new List<AstEnumDecl>();
        public List<AstDelegateDecl> AllDelegatesMetadata { get; } = new List<AstDelegateDecl>();
        public List<AstFuncDecl> AllFunctionsMetadata { get; } = new List<AstFuncDecl>();
        public List<AstGenericDecl> AllGenericsMetadata { get; } = new List<AstGenericDecl>();

        private List<AstClassDecl> _serializeClassesMetadata { get; } = new List<AstClassDecl>();
        private List<AstStructDecl> _serializeStructsMetadata { get; } = new List<AstStructDecl>();
        private List<AstEnumDecl> _serializeEnumsMetadata { get; } = new List<AstEnumDecl>();
        private List<AstDelegateDecl> _serializeDelegatesMetadata { get; } = new List<AstDelegateDecl>();
        private List<AstFuncDecl> _serializeFunctionsMetadata { get; } = new List<AstFuncDecl>();

        private List<AstDeclaration> _allPureGenericTypes { get; } = new List<AstDeclaration>();

        private PreparationStep _currentPreparationStep { get; set; } = PreparationStep.None;

        /// <summary>
        /// Used in LSP
        /// </summary>
        public void ClearLists()
        {
            _allPureGenericTypes.Clear();
            _serializeClassesMetadata.Clear();
            _serializeStructsMetadata.Clear();
            _serializeEnumsMetadata.Clear();
            _serializeDelegatesMetadata.Clear();
            _serializeFunctionsMetadata.Clear();
            AllClassesMetadata.Clear();
            AllStructsMetadata.Clear();
            AllEnumsMetadata.Clear();
            AllDelegatesMetadata.Clear();
            AllFunctionsMetadata.Clear();
            AllGenericsMetadata.Clear();
        }

        private int PostPrepareMetadata(InInfo inInfo, bool createMetadataFile = true)
        {
            AllPostPrepareMetadataTypes(inInfo);
            AllPostPrepareMetadataGenerics(inInfo);
            AllPostPrepareMetadataInheritance(inInfo);
            AllPostPrepareMetadataDelegates(inInfo);
            AllPostPrepareMetadataNestedTypes(inInfo);
            AllPostPrepareMetadataFunctions(inInfo);
            AllPostPrepareMetadataTypeFieldDecls(inInfo);
            AllPostPrepareMetadataNestedTypesInside(inInfo);
            AllPostPrepareMetadataTypeFieldInits(inInfo);
            AllPostPrepareMetadataAttributes(inInfo);

            // if there were errors while preparing for metafile
            if (_compiler.MessageHandler.HasErrors)
            {
                return (int)CompilerErrors.PostPrepareMetafileError; // post prepare errors
            }

            // creating the file
            if (createMetadataFile)
                GenerateMetadataFile();

            // WARN: removing all properties after saving to file
            // removing them only now because we need them to be presented in metadata
            /// unwrapping props is done in <see cref="PostPrepareClassProperties"/>
            RemoveAllProperties();

            return 0;
        }

        private void AllPostPrepareMetadataTypes(InInfo inInfo)
        {
            _currentPreparationStep = PreparationStep.Types;

            foreach (var (_, file) in _compiler.GetFiles())
            {
                AllPostPrepareMetadataTypesInFile(inInfo, file);
            }
        }

        public void AllPostPrepareMetadataTypesInFile(InInfo inInfo, ProgramFile file)
        {
            _currentSourceFile = file;
            foreach (var stmt in file.Statements)
            {
                // do not serialize imported shite
                var needSerialize = (!(stmt as AstDeclaration)?.IsImported ?? false);
                PostPrepareMetadataTypes(inInfo, stmt, needSerialize);
            }
        }

        private void AllPostPrepareMetadataGenerics(InInfo inInfo)
        {
            _currentPreparationStep = PreparationStep.Generics;

            // resolve generic shite of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                _currentParentStack.AddParent(cls);
                bool _ = PostPrepareMetadataGenerics(inInfo, cls);
                _currentParentStack.RemoveParent();
            }
            // resolve generic shite of structs
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                _currentParentStack.AddParent(str);
                bool _ = PostPrepareMetadataGenerics(inInfo, str);
                _currentParentStack.RemoveParent();
            }
            // resolve generic shite of delegates
            foreach (var del in AllDelegatesMetadata.ToList())
            {
                _currentSourceFile = del.SourceFile;
                _currentParentStack.AddParent(del);
                bool _ = PostPrepareMetadataGenerics(inInfo, del);
                _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataInheritance(InInfo inInfo)
        {
            _currentPreparationStep = PreparationStep.Inheritance;

            // resolve inheritance shite of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataInheritance(inInfo, cls);
                _currentParentStack.RemoveParent();
            }
            // resolve inheritance shite of structs
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                _currentParentStack.AddParent(str);
                PostPrepareMetadataInheritance(inInfo, str);
                _currentParentStack.RemoveParent();
            }
            // resolve inheritance shite of enums
            foreach (var enm in AllEnumsMetadata.ToList())
            {
                _currentSourceFile = enm.SourceFile;
                _currentParentStack.AddParent(enm);
                PostPrepareMetadataInheritance(inInfo, enm);
                _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataDelegates(InInfo inInfo)
        {
            _currentPreparationStep = PreparationStep.Delegates;

            // inferrencing delegates
            foreach (var del in AllDelegatesMetadata.ToList())
            {
                _currentSourceFile = del.SourceFile;
                _currentParentStack.AddParent(del);
                PostPrepareMetadataDelegates(inInfo, del);
                _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataNestedTypes(InInfo inInfo)
        {
            _currentPreparationStep = PreparationStep.NestedTypes;

            // inferrencing nested types
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                _currentParentStack.AddParent(cls);

                PostPrepareMetadataNestedTypes(inInfo, cls);
                _currentParentStack.RemoveParent();
            }
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                _currentParentStack.AddParent(str);

                PostPrepareMetadataNestedTypes(inInfo, str);
                _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataFunctions(InInfo inInfo)
        {
            _currentPreparationStep = PreparationStep.Functions;

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
                _currentSourceFile = func.SourceFile;

                if (func.ContainingParent.IsNestedDecl)
                    _currentParentStack.AddParent(func.ContainingParent.ParentDecl);
                _currentParentStack.AddParent(func.ContainingParent);

                bool isImported = func.IsImported;
                PostPrepareMetadataFunctions(inInfo, func, !isImported);

                _currentParentStack.RemoveParent();
                if (func.ContainingParent.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
        }

        /// <summary>
        /// We need to infer all decl at first and only then - their intializers
        /// </summary>
        private void AllPostPrepareMetadataTypeFieldDecls(InInfo inInfo)
        {
            _currentPreparationStep = PreparationStep.FieldAndPropDecls;

            // resolve all fields of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                if (cls.IsNestedDecl)
                    _currentParentStack.AddParent(cls.ParentDecl);
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataTypeFieldDecls(inInfo, cls);
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
                PostPrepareMetadataTypeFieldDecls(inInfo, str);
                _currentParentStack.RemoveParent();
                if (str.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            foreach (var gen in AllGenericsMetadata.ToList())
            {
                _currentSourceFile = gen.SourceFile;
                if (gen.IsNestedDecl)
                    _currentParentStack.AddParent(gen.ParentDecl);
                _currentParentStack.AddParent(gen);
                PostPrepareMetadataTypeFieldDecls(inInfo, gen);
                _currentParentStack.RemoveParent();
                if (gen.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            foreach (var enm in AllEnumsMetadata.ToList())
            {
                _currentSourceFile = enm.SourceFile;
                if (enm.IsNestedDecl)
                    _currentParentStack.AddParent(enm.ParentDecl);
                _currentParentStack.AddParent(enm);
                PostPrepareMetadataTypeFieldDecls(inInfo, enm);
                _currentParentStack.RemoveParent();
                if (enm.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataNestedTypesInside(InInfo inInfo)
        {
            _currentPreparationStep = PreparationStep.NestedTypesInside;

            // inferrencing nested types
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                _currentParentStack.AddParent(cls);

                PostPrepareMetadataNestedTypesInside(inInfo, cls);
                _currentParentStack.RemoveParent();
            }
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                _currentParentStack.AddParent(str);

                PostPrepareMetadataNestedTypesInside(inInfo, str);
                _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataTypeFieldInits(InInfo inInfo)
        {
            _currentPreparationStep = PreparationStep.FieldAndPropInits;

            // resolve all fields of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                if (cls.IsNestedDecl)
                    _currentParentStack.AddParent(cls.ParentDecl);
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataTypeFieldInits(inInfo, cls);
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
                PostPrepareMetadataTypeFieldInits(inInfo, str);
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
                PostPrepareMetadataTypeFieldInits(inInfo, enm);
                _currentParentStack.RemoveParent();
                if (enm.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
        }

        private void AllPostPrepareMetadataAttributes(InInfo inInfo)
        {
            _currentPreparationStep = PreparationStep.Attributes;

            // inferrencing attribtues of functions
            foreach (var fnc in AllFunctionsMetadata.ToList())
            {
                if (fnc.ContainingParent.IsNestedDecl)
                    _currentParentStack.AddParent(fnc.ContainingParent.ParentDecl);
                _currentParentStack.AddParent(fnc.ContainingParent);

                _currentSourceFile = fnc.SourceFile;
                _currentParentStack.AddParent(fnc);
                PostPrepareMetadataAttributes(inInfo, fnc);
                _currentParentStack.RemoveParent();

                _currentParentStack.RemoveParent();
                if (fnc.ContainingParent.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            // inferrencing attribtues of classes
            foreach (var cls in AllClassesMetadata.ToList())
            {
                _currentSourceFile = cls.SourceFile;
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataAttributes(inInfo, cls);
                _currentParentStack.RemoveParent();
            }
            // inferrencing attribtues of structs
            foreach (var str in AllStructsMetadata.ToList())
            {
                _currentSourceFile = str.SourceFile;
                _currentParentStack.AddParent(str);
                PostPrepareMetadataAttributes(inInfo, str);
                _currentParentStack.RemoveParent();
            }
            // inferrencing attribtues of enums
            foreach (var enm in AllEnumsMetadata.ToList())
            {
                _currentSourceFile = enm.SourceFile;
                _currentParentStack.AddParent(enm);
                PostPrepareMetadataAttributes(inInfo, enm);
                _currentParentStack.RemoveParent();
            }
            // inferrencing attribtues of delegates
            foreach (var del in AllDelegatesMetadata.ToList())
            {
                _currentSourceFile = del.SourceFile;
                _currentParentStack.AddParent(del);
                PostPrepareMetadataAttributes(inInfo, del);
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
