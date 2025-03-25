using HapetFrontend.Ast.Declarations;
using HapetFrontend.Parsing;

namespace HapetFrontend.Entities
{
    internal class ParserInInfo
    {
        public bool ExpectNewline { get; set; }
        public bool AllowArrayExpression { get; set; }

        /// <summary>
        /// 'true' when parsing .mpt file
        /// </summary>
        public bool ExternalMetadata { get; set; }

        /// <summary>
        /// This shite is used for func decl
        /// </summary>
        public AstUnknownDecl CurrentUdecl { get; set; }
        public MessageResolver Message { get; set; }

        public static ParserInInfo Default => new ParserInInfo()
        {
            ExpectNewline = true,
            AllowArrayExpression = false,
            ExternalMetadata = false,
            Message = null,
        };
    }
}
