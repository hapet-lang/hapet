using HapetFrontend.Ast.Declarations;
using HapetFrontend.Parsing;

namespace HapetFrontend.Entities
{
    internal class ParserInInfo
    {
        public bool AllowArrayExpression { get; set; }
        /// <summary>
        /// 'true' when allowing to multiplication exist.
        /// Solves the problem:
        /// a = (test * test);
        /// func(byte* test);
        /// </summary>
        public bool AllowMultiplyExpression { get; set; }

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
            AllowArrayExpression = true,
            AllowMultiplyExpression = true,
            ExternalMetadata = false,
            Message = null,
        };
    }
}
