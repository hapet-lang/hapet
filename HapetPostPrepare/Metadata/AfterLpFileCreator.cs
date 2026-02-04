using HapetFrontend.Ast;
using HapetFrontend.Entities;
using HapetPostPrepare.Entities;
using System.Text;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private bool _generatingAfterLpFile = false;

        public void GenerateAfterLpFile()
        {
            StringBuilder globalStringBuilder = new StringBuilder();

            SortDeclarationsAfterLp();
            _generatingAfterLpFile = true;
            foreach (var srt in _sortedDeclsByFiles)
            {
                _currentSourceFile = srt.Key;
                CreateFileDeclarations(srt.Key, srt.Value, globalStringBuilder);
            }
            _generatingAfterLpFile = false;

            // WARN: take care about the shite that is goin on here
            var outFolderPath = _compiler.CurrentProjectSettings.OutputDirectory;
            var asmName = _compiler.CurrentProjectSettings.AssemblyName;
            File.WriteAllText($"{outFolderPath}/{asmName}.mpt", globalStringBuilder.ToString());
        }

        private void SortDeclarationsAfterLp()
        {
            _sortedDeclsByFiles = new Dictionary<ProgramFile, List<AstDeclaration>>();
            foreach (var cls in AllClassesMetadata)
            {
                if (_sortedDeclsByFiles.TryGetValue(cls.SourceFile, out var decls))
                    decls.Add(cls);
                else
                    _sortedDeclsByFiles[cls.SourceFile] = new List<AstDeclaration>() { cls };
            }
            foreach (var str in AllStructsMetadata)
            {
                if (_sortedDeclsByFiles.TryGetValue(str.SourceFile, out var decls))
                    decls.Add(str);
                else
                    _sortedDeclsByFiles[str.SourceFile] = new List<AstDeclaration>() { str };
            }
            foreach (var enm in AllEnumsMetadata)
            {
                if (_sortedDeclsByFiles.TryGetValue(enm.SourceFile, out var decls))
                    decls.Add(enm);
                else
                    _sortedDeclsByFiles[enm.SourceFile] = new List<AstDeclaration>() { enm };
            }
            foreach (var del in AllDelegatesMetadata)
            {
                if (_sortedDeclsByFiles.TryGetValue(del.SourceFile, out var decls))
                    decls.Add(del);
                else
                    _sortedDeclsByFiles[del.SourceFile] = new List<AstDeclaration>() { del };
            }
            // no need to sort func - they would be taken when serializing classes/structs
        }
    }
}
