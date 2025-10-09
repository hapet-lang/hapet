using HapetFrontend.Ast.Expressions;
using HapetFrontend.Helpers;

namespace HapetFrontend.Ast.Declarations
{
    public class AstIndexerDecl : AstPropertyDecl
    {
        /// <summary>
        /// Parameter that is passed to indexer
        /// </summary>
        public AstParamDecl IndexerParameter { get; set; }

        public override string AAAName => nameof(AstIndexerDecl);

        public AstIndexerDecl(AstNestedExpr type, AstIdExpr name, string doc = "", ILocation location = null) : 
            base(type, name, null, doc, location)
        {
        }

        public AstIndexerDecl(AstPropertyDecl prop) : base(prop.Type, prop.Name, prop.Initializer, prop.Documentation, prop.Location)
        {
            HasGet = prop.HasGet;
            HasSet = prop.HasSet;
            GetBlock = prop.GetBlock;
            SetBlock = prop.SetBlock;
            GetTokenPosition = prop.GetTokenPosition;
            SetTokenPosition = prop.SetTokenPosition;

            SpecialKeys.AddRange(prop.SpecialKeys);
            Attributes.AddRange(prop.Attributes);
        }

        public override string ToString()
        {
            return $"index:{GenericsHelper.GetNameFromAst(Name, null)}";
        }

        public override AstDeclaration GetOnlyDeclareCopy()
        {
            var copy = new AstIndexerDecl(
                Type.GetDeepCopy() as AstNestedExpr,
                Name.GetDeepCopy() as AstIdExpr,
                Documentation, Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                IndexerParameter = IndexerParameter.GetDeepCopy() as AstParamDecl,
                HasGet = HasGet,
                HasSet = HasSet,
                IsImported = IsImported,
                HasGenericTypes = HasGenericTypes,
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
                GetTokenPosition = GetTokenPosition,
                SetTokenPosition = SetTokenPosition,
                GenericConstrainLocations = GenericConstrainLocations,
            };
            copy.Attributes.AddRange(Attributes);
            copy.SpecialKeys.AddRange(SpecialKeys);
            return copy;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstIndexerDecl(
                Type.GetDeepCopy() as AstNestedExpr,
                Name.GetDeepCopy() as AstIdExpr,
                Documentation, Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                IndexerParameter = IndexerParameter.GetDeepCopy() as AstParamDecl,
                HasGet = HasGet,
                HasSet = HasSet,
                GetBlock = GetBlock?.GetDeepCopy() as AstBlockExpr,
                SetBlock = SetBlock?.GetDeepCopy() as AstBlockExpr,
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
                GetTokenPosition = GetTokenPosition,
                SetTokenPosition = SetTokenPosition,
                GenericConstrainLocations = GenericConstrainLocations,
            };
            copy.Attributes.AddRange(Attributes);
            copy.SpecialKeys.AddRange(SpecialKeys);
            return copy;
        }
    }
}
