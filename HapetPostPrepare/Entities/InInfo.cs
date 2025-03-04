namespace HapetPostPrepare.Entities
{
    internal class InInfo
    {
        public bool ForMetadata { get; set; }
        public bool AllowSpecialKeys { get; set; }
        public bool FromCallExpr { get; set; }
        public bool PropertySet { get; set; }
        public bool MuteErrors { get; set; }

        public static InInfo Default => new InInfo()
        {
            ForMetadata = false,
            AllowSpecialKeys = false,
            FromCallExpr = false,
            PropertySet = false,
            MuteErrors = false,
        };
    }
}
