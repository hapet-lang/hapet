using HapetFrontend.Ast.Declarations;

namespace HapetFrontend.Entities
{
    internal class ParserOutInfo
    {
        public static ParserOutInfo Default => new ParserOutInfo()
        {
        };

        /// <summary>
        /// List to handle 'anime is Anime a' shite
        /// </summary>
        public List<AstVarDecl> IsOpDeclarations { get; private set; } = new List<AstVarDecl>();
    }
}
