using HapetFrontend.Parsing;

namespace HapetFrontend.Entities
{
    internal class ParserInInfo
    {
        public bool ExpectNewline { get; set; }
        public bool AllowCommaForTuple { get; set; }
        public bool AllowFunctionDeclaration { get; set; }
        public bool AllowPointerExpression { get; set; }
        public bool AllowArrayExpression { get; set; }
        public bool AllowGeneric { get; set; }
        public MessageResolver Message { get; set; }

        public static ParserInInfo Default => new ParserInInfo()
        {
            ExpectNewline = true,
            AllowCommaForTuple = false,
            AllowFunctionDeclaration = false,
            AllowPointerExpression = false,
            AllowArrayExpression = false,
            AllowGeneric = false,
            Message = null,
        };
    }
}
