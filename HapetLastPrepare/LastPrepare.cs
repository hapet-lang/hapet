using HapetFrontend.Entities;
using HapetFrontend;
using HapetPostPrepare;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast;

namespace HapetLastPrepare
{
    public partial class LastPrepare
    {
        private readonly Compiler _compiler;
        private readonly PostPrepare _postPreparer;

        /// <summary>
        /// File that is currently preparing
        /// </summary>
        private ProgramFile _currentSourceFile;

        public LastPrepare(Compiler compiler, PostPrepare postPreparer)
        {
            _compiler = compiler;
            _postPreparer = postPreparer;
        }

        public int StartPreparation()
        {
            // we are checking for errors after each function 
            // because some steps won't work properly without previous
            // 0 is returned because normal error is going to be
            // returned in the caller shite

            ReplaceAllProperties();
            if (_compiler.MessageHandler.HasErrors)
                return 0;

            ReplaceAllClasses();
            if (_compiler.MessageHandler.HasErrors)
                return 0;

            return 0;
        }

        public static bool ShouldTheDeclBeSkippedFromCodeGen(AstDeclaration decl)
        {
            // skip generic (non-real) parents
            if (decl.ContainingParent?.HasGenericTypes ?? false)
                return true;
            // skip generic (non-real) funcs
            if (decl.HasGenericTypes)
                return true;
            // also skip if parent has generic types
            if (decl.IsNestedDecl && decl.ParentDecl.HasGenericTypes)
                return true;
            // skip genericDecl parents
            if (decl.ContainingParent is AstGenericDecl)
                return true;
            // happens at least when 'decl' is a func in a normal struct and the struct
            // is nested into a generic class
            if (decl.ContainingParent != null && decl.ContainingParent.IsNestedDecl &&
                decl.ContainingParent.ParentDecl.HasGenericTypes)
                return true;
            return false;
        }
    }
}
