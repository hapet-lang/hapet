using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Entities;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetPostPrepare.Entities;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private List<AstClassDecl> _classes;
        private List<AstStructDecl> _structs;
        private List<AstEnumDecl> _enums;
        private List<AstDelegateDecl> _delegates;

        private string _externalProjectFilename;

        public void PostPrepareExternalMetadata(MetadataJson metadata, string fileName)
        {
            PosPrepareExternalLoad(metadata, fileName);

            // we need to do this to know locations of the asts
            _externalProjectName = $"Referenced assembly '.../{fileName}'";
            PostPrepareExternalScoping();
            _externalProjectName = null;

            PostPrepareExternalMetadataInternal();
        }

        private void PosPrepareExternalLoad(MetadataJson metadata, string fileName)
        {
            // getting asts
            _classes = metadata.ClassDecls.Select(x => x.GetAst(_compiler)).ToList();
            _structs = metadata.StructDecls.Select(x => x.GetAst(_compiler)).ToList();
            _enums = metadata.EnumDecls.Select(x => x.GetAst(_compiler)).ToList();
            _delegates = metadata.DelegateDecls.Select(x => x.GetAst(_compiler)).ToList();
            var funcs = metadata.FuncDecls.Select(x => x.GetAst(_compiler)).ToList();
            _externalProjectFilename = fileName;

            // setting all functions into classes. So do not use funcs anymore
            foreach (var fnc in funcs)
            {
                string typeName = string.Concat(fnc.Name.Name.TakeWhile(x => x != ':'));

                var theClass = _classes.FirstOrDefault(x => x.Name.Name == typeName);
                if (theClass != null)
                {
                    theClass.Declarations.Add(fnc);
                    fnc.ContainingParent = theClass;
                }
                var theStruct = _structs.FirstOrDefault(x => x.Name.Name == typeName);
                if (theStruct != null)
                {
                    theStruct.Declarations.Add(fnc);
                    fnc.ContainingParent = theStruct;
                }
                // TODO: error if both are null
            }
        }

        /// <summary>
        /// Like <see cref="PostPrepareScoping"/>
        /// </summary>
        private void PostPrepareExternalScoping()
        {
            foreach (var classDecl in _classes)
            {
                classDecl.IsImported = true;
                PrepareDeclarationShite(classDecl);
                _currentSourceFile = classDecl.SourceFile;
                PostPrepareClassScoping(classDecl);
            }
            foreach (var structDecl in _structs)
            {
                structDecl.IsImported = true;
                // getting namespace from full struct name
                PrepareDeclarationShite(structDecl);
                _currentSourceFile = structDecl.SourceFile;
                PostPrepareStructScoping(structDecl);
            }
            foreach (var enumDecl in _enums)
            {
                enumDecl.IsImported = true;
                // getting namespace from full enum name
                PrepareDeclarationShite(enumDecl);
                _currentSourceFile = enumDecl.SourceFile;
                PostPrepareEnumScoping(enumDecl);
            }
            foreach (var delegateDecl in _delegates)
            {
                delegateDecl.IsImported = true;
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
                PostPrepareMetadataTypes(classDecl, false);
            }
            foreach (var structDecl in _structs)
            {
                PostPrepareMetadataTypes(structDecl, false);
            }
            foreach (var enumDecl in _enums)
            {
                PostPrepareMetadataTypes(enumDecl, false);
            }
            foreach (var delegateDecl in _delegates)
            {
                PostPrepareMetadataTypes(delegateDecl, false);
            }
        }
    }
}
