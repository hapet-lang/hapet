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
        /// 'true' if shite like (int, int) is allowed
        /// AND!!!
        /// 'true' if shite like var a, b = ... is allowed
        /// </summary>
        public bool AllowTypedTuple { get; set; }

        /// <summary>
        /// 'true' when parsing .mpt file
        /// </summary>
        public bool ExternalMetadata { get; set; }

        /// <summary>
        /// 'true' when nested func decl is allowed in current AstBlockExpr
        /// </summary>
        public bool AllowNestedFunc { get; set; }
        /// <summary>
        /// The parent func decl that is used when <see cref="AllowNestedFunc"/> is 'true'
        /// </summary>
        public AstFuncDecl ParentFuncDecl { get; set; }

        /// <summary>
        /// This shite is used for func decl
        /// </summary>
        public AstUnknownDecl CurrentUdecl { get; set; }
        public MessageResolver Message { get; set; }

        public static ParserInInfo Default => new ParserInInfo()
        {
            AllowArrayExpression = true,
            AllowTypedTuple = false,
            AllowMultiplyExpression = true,
            ExternalMetadata = false,
            Message = null,
        };
    }
}
