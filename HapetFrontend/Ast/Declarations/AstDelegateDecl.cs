using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using System.Xml.Linq;

namespace HapetFrontend.Ast.Declarations
{
    public class AstDelegateDecl : AstDeclaration
    {
        public List<AstParamDecl> Parameters { get; set; }
        public AstExpression Returns { get; set; }

        public override string AAAName => nameof(AstDelegateDecl);

        public AstDelegateDecl(List<AstParamDecl> parameters, AstExpression returns, AstIdExpr name, string doc = "", ILocation location = null) : base(name, doc, location)
        {
            Type = new AstIdExpr("delegate", location);
            Type.OutType = new DelegateType(this);

            Parameters = parameters;
            Returns = returns;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstDelegateDecl(
                Parameters.Select(x => x.GetDeepCopy() as AstParamDecl).ToList(),
                Returns.GetDeepCopy() as AstExpression,
                Name.GetDeepCopy() as AstIdExpr,
                Documentation, Location)
            {
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
