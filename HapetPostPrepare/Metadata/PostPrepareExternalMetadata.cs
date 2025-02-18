using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Entities;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private List<AstClassDecl> _classes;
        private List<AstStructDecl> _structs;
        private List<AstEnumDecl> _enums;
        private List<AstDelegateDecl> _delegates;
        private List<AstFuncDecl> _funcs;

        private string _externalProjectFilename;

        public void PostPrepareExternalMetadata(MetadataJson metadata, string fileName)
        {
            // getting asts
            _classes = metadata.ClassDecls.Select(x => x.GetAst()).ToList();
            _structs = metadata.StructDecls.Select(x => x.GetAst()).ToList();
            _enums = metadata.EnumDecls.Select(x => x.GetAst()).ToList();
            _delegates = metadata.DelegateDecls.Select(x => x.GetAst()).ToList();
            _funcs = metadata.FuncDecls.Select(x => x.GetAst()).ToList();
            _externalProjectFilename = fileName;

            // setting all functions into classes. So do not use _funcs anymore
            foreach (var fnc in _funcs)
            {
                string className = string.Concat(fnc.Name.Name.TakeWhile(x => x != ':'));
                var theClass = _classes.FirstOrDefault(x => x.Name.Name == className); // TODO: error if null
                theClass.Declarations.Add(fnc);
                fnc.ContainingParent = theClass;
            }

            // we need to do this to know locations of the asts
            _externalProjectName = $"Referenced assembly '.../{fileName}'";
            PostPrepareExternalScoping();
            _externalProjectName = null;

            PostPrepareExternalMetadataInternal();
        }

        /// <summary>
        /// Like <see cref="PostPrepareScoping"/>
        /// </summary>
        private void PostPrepareExternalScoping()
        {
            foreach (var classDecl in _classes)
            {
                PrepareDeclarationShite(classDecl);
                _currentSourceFile = classDecl.SourceFile;
                PostPrepareClassScoping(classDecl);
            }
            foreach (var structDecl in _structs)
            {
                // getting namespace from full struct name
                PrepareDeclarationShite(structDecl);
                _currentSourceFile = structDecl.SourceFile;
                PostPrepareStructScoping(structDecl);
            }
            foreach (var enumDecl in _enums)
            {
                // getting namespace from full enum name
                PrepareDeclarationShite(enumDecl);
                _currentSourceFile = enumDecl.SourceFile;
                PostPrepareEnumScoping(enumDecl);
            }
            foreach (var delegateDecl in _delegates)
            {
                // getting namespace from full delegate name
                PrepareDeclarationShite(delegateDecl);
                _currentSourceFile = delegateDecl.SourceFile;
                PostPrepareDelegateScoping(delegateDecl);
            }
        }

        private void PrepareDeclarationShite(AstDeclaration decl)
        {
            // getting namespace from full decl name
            string nameSpaceName = string.Join('.', decl.Name.Name.Split('.').SkipLast(1));
            Scope nameSpaceScope = _compiler.GetNamespaceScope(nameSpaceName);
            ProgramFile tmpProgFile = new ProgramFile(_externalProjectFilename, string.Empty) { Namespace = nameSpaceName, NamespaceScope = nameSpaceScope };
            tmpProgFile.Statements.Add(decl);
            decl.Scope = nameSpaceScope;
            decl.SourceFile = tmpProgFile;
        }

        private void PostPrepareExternalMetadataInternal()
        {
            /// like <see cref="PostPrepareMetadataTypes"/>
            foreach (var classDecl in _classes)
            {
                classDecl.Scope.DefineDeclSymbol(classDecl.Name.Name, classDecl);
                AllClassesMetadata.Add(classDecl);

                PostPrepareAliases(classDecl.Name.Name, classDecl.Scope, classDecl);
            }
            foreach (var structDecl in _structs)
            {
                structDecl.Scope.DefineDeclSymbol(structDecl.Name.Name, structDecl);
                AllStructsMetadata.Add(structDecl);

                PostPrepareAliases(structDecl.Name.Name, structDecl.Scope, structDecl);
            }
            foreach (var enumDecl in _enums)
            {
                enumDecl.Scope.DefineDeclSymbol(enumDecl.Name.Name, enumDecl);
                AllEnumsMetadata.Add(enumDecl);
            }
            foreach (var delegateDecl in _delegates)
            {
                delegateDecl.Scope.DefineDeclSymbol(delegateDecl.Name.Name, delegateDecl);
                AllDelegatesMetadata.Add(delegateDecl);
            }

            /// like <see cref="PostPrepareMetadataFunctions"/>
            foreach (var cls in _classes)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                foreach (var decl in cls.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl))
                {
                    // set that the function is imported from another assembly
                    decl.SpecialKeys.Add(TokenType.KwImported);
                    PostPrepareFunctionInference(decl, true);
                    AllFunctionsMetadata.Add(decl);
                }
            }
        }
    }
}
