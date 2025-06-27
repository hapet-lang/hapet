using HapetFrontend.Ast;
using HapetFrontend.Helpers;

namespace HapetLastPrepare
{
    public partial class LastPrepare
    {
        /// <summary>
        /// This shite is created to fix AllVirtualMethods problem caused of property methods generations
        /// </summary>
        private void AppendVirtuals()
        {
            foreach (var cls in _postPreparer.AllClassesMetadata)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(cls))
                    continue;
                _postPreparer._currentSourceFile = cls.SourceFile;
                AppendVirtualsOnDecl(cls);
            }
            foreach (var str in _postPreparer.AllStructsMetadata)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(str))
                    continue;
                _postPreparer._currentSourceFile = str.SourceFile;
                AppendVirtualsOnDecl(str);
            }
        }

        private void AppendVirtualsOnDecl(AstDeclaration decl)
        {
            var decls = decl.GetDeclarations();
            var virts = decl.GetAllVirtualMethods();
            foreach (var vP in decl.GetAllVirtualProps())
            {
                // skip overrided props
                if (decls.Contains(vP))
                    continue;

                if (vP.GetFunction != null)
                    virts.Add(vP.GetFunction);
                if (vP.SetFunction != null)
                    virts.Add(vP.SetFunction);
            }

            // go all over nested
            foreach (var d in decls)
                AppendVirtualsOnDecl(d);
        }
    }
}
