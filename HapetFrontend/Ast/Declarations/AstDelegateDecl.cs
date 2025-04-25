using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
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

            Parameters = parameters;
            Returns = returns;
        }

        public override AstStatement GetDeepCopy()
        {
            Dictionary<AstIdExpr, List<AstNestedExpr>> copiedConstrains = new Dictionary<AstIdExpr, List<AstNestedExpr>>();
            foreach (var cc in GenericConstrains)
            {
                copiedConstrains.Add(cc.Key.GetDeepCopy() as AstIdExpr, cc.Value.Select(x => x.GetDeepCopy() as AstNestedExpr).ToList());
            }

            var copy = new AstDelegateDecl(
                Parameters.Select(x => x.GetDeepCopy() as AstParamDecl).ToList(),
                Returns.GetDeepCopy() as AstExpression,
                Name.GetDeepCopy() as AstIdExpr,
                Documentation, Location)
            {
                GenericNames = GenericNames?.Select(x => x.GetDeepCopy() as AstIdExpr).ToList(),
                GenericConstrains = copiedConstrains,
                HasGenericTypes = HasGenericTypes,
                IsImported = IsImported,
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
            };
            copy.Attributes.AddRange(Attributes);
            copy.SpecialKeys.AddRange(SpecialKeys);
            return copy;
        }

        public static AstClassDecl GetDelegateClass(Scope scope)
        {
            return (scope.GetSymbolInNamespace("System", "Delegate") as DeclSymbol).Decl as AstClassDecl;
        }
    }
}
