namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public void PostPrepareExternalMetadata(string metadataText)
        {
            // just parsing metadata and adding its files into the current _compiler
            var files = _compiler.ParseMetadata(metadataText);
            foreach (var f in files)
            {
                _compiler.AddFile(f, f.Name);
            }
        }
    }
}
