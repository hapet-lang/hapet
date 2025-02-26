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

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstIndexerDecl(
                Type.GetDeepCopy() as AstExpression,
                Name.GetDeepCopy() as AstIdExpr,
                Documentation, Location)
            {
                IndexerParameter = IndexerParameter.GetDeepCopy() as AstParamDecl,
                HasGet = HasGet,
                HasSet = HasSet,
                GetBlock = GetBlock.GetDeepCopy() as AstBlockExpr,
                SetBlock = SetBlock.GetDeepCopy() as AstBlockExpr,
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
            };
            copy.Attributes.AddRange(Attributes);
            copy.SpecialKeys.AddRange(SpecialKeys);
            return copy;
        }
    }
}
