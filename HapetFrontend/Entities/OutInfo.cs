using HapetFrontend.Ast.Declarations;

namespace HapetFrontend.Entities
{
    internal class ParserOutInfo
    {
        public static ParserOutInfo Default => new ParserOutInfo()
        {
        };

        /// <summary>
        /// List to handle statements that need to be added before main statement. 
        /// Handled in BlockExprParsing
        /// </summary>
        public List<AstVarDecl> StatementsToAddBefore { get; set; } = new List<AstVarDecl>();
        /// <summary>
        /// List to handle statements that need to be added after main statement. 
        /// Handled in BlockExprParsing
        /// </summary>
        public List<AstVarDecl> StatementsToAddAfter { get; set; } = new List<AstVarDecl>();
    }
}
