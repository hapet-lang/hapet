using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Declarations
{
    public class AstIndexerDecl : AstPropertyDecl
    {
        /// <summary>
        /// Parameter that is passed to indexer
        /// </summary>
        public AstParamDecl IndexerParameter { get; set; }

        public AstIndexerDecl(AstExpression type, AstIdExpr name, string doc = "", ILocation Location = null) : 
            base(type, name, null, doc, Location)
        {
        }
    }
}
