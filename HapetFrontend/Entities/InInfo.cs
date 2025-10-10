using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Parsing;

namespace HapetFrontend.Entities
{
    public class ParserInInfo
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
        /// If 'true' - generic shite is prefered. Check this
        /// https://github.com/hapet-lang/hapet/issues/67
        /// </summary>
        public bool PreferGenericShite { get; set; }

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
        /// Handles previous nested expr for inlined null checks like anime?.Cringe?.Pivo;
        /// </summary>
        public AstNestedExpr PreviousNestedForNullCheck { get; set; }

        /// <summary>
        /// if 'true' then block expr parser would skip semicolon checks when sees 'default' kw
        /// added because of https://github.com/hapet-lang/hapet/issues/69
        /// </summary>
        public bool SkipDefaultSemicolonChecks { get; set; }

        /// <summary>
        /// 'true' if currently tuple is parsing. Used to restrict AstIdTupled parsing
        /// </summary>
        public bool IsInTupleParsing { get; set; }

        /// <summary>
        /// 'true' if statements inside 'if' directives should be handled as statements 
        /// in block expr. (This adds ; requirement)
        /// </summary>
        public bool HandleDirectiveInBlock { get; set; }

        /// <summary>
        /// 'true' if directive is parsing now
        /// </summary>
        public bool CurrentlyParsingDirective { get; set; }

        /// <summary>
        /// 'true' is expecting 'default' as a case
        /// </summary>
        public bool ExpectDefaultCase { get; set; }

        /// <summary>
        /// 'true' if currently parsing with look ahead feature
        /// </summary>
        public bool IsLookAheadParsing { get; set; }

        /// <summary>
        /// This shite is used for func decl
        /// </summary>
        public AstUnknownDecl CurrentUdecl { get; set; }

        /// <summary>
        /// 'true' if one-word stmt allowed in udecl preparation. Usually allowed only in 
        /// arrowed functions and shite like this.
        /// </summary>
        public bool AllowOneWordStatement { get; set; }
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
