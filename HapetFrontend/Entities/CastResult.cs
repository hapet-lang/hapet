namespace HapetFrontend.Entities
{
    public class CastResult
    {
        /// <summary>
        /// true if could be casted implicitly like
        /// short a = 3;
        /// int b = a;
        /// </summary>
        public bool CouldBeCasted { get; set; }
        /// <summary>
        /// true if could be narrowed like
        /// int a = 4214244;
        /// short b = a;
        /// </summary>
        public bool CouldBeNarrowed { get; set; }
    }
}
