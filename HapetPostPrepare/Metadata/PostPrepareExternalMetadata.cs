namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public void PostPrepareExternalMetadata(string metadataText, string fileName)
        {
            var files = _compiler.ParseMetadata(metadataText);
        }
    }
}
