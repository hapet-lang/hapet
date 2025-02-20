using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Ast.Declarations
{
    public class AstIndexerDecl : AstPropertyDecl
    {
        /// <summary>
        /// Parameter that is passed to indexer
        /// </summary>
        public AstParamDecl IndexerParameter { get; set; }

        public override string AAAName => nameof(AstIndexerDecl);

        public AstIndexerDecl(AstExpression type, AstIdExpr name, string doc = "", ILocation Location = null) : 
            base(type, name, null, doc, Location)
        {
        }

        public AstIndexerDecl(AstPropertyDecl prop) : base(prop.Type, prop.Name, prop.Initializer, prop.Documentation, prop.Location)
        {
            HasGet = prop.HasGet;
            HasSet = prop.HasSet;
            GetBlock = prop.GetBlock;
            SetBlock = prop.SetBlock;

            SpecialKeys.AddRange(prop.SpecialKeys);
            Attributes.AddRange(prop.Attributes);
        }
    }
}
