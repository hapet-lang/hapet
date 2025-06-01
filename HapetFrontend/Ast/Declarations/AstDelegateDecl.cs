using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
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
            /// WARN: type is set in <see cref="PostPrepareMetadataDelegates"/>

            Parameters = parameters;
            Returns = returns;
        }

        public override AstStatement GetDeepCopy()
        {
            Dictionary<AstIdExpr, List<AstConstrainStmt>> copiedConstrains = new Dictionary<AstIdExpr, List<AstConstrainStmt>>();
            foreach (var cc in GenericConstrains)
            {
                copiedConstrains.Add(cc.Key.GetDeepCopy() as AstIdExpr, cc.Value.Select(x => x.GetDeepCopy() as AstConstrainStmt).ToList());
            }

            var copy = new AstDelegateDecl(
                Parameters.Select(x => x.GetDeepCopy() as AstParamDecl).ToList(),
                Returns.GetDeepCopy() as AstExpression,
                Name.GetDeepCopy() as AstIdExpr,
                Documentation, Location)
            {
                GenericConstrains = copiedConstrains,
                HasGenericTypes = HasGenericTypes,
                IsNestedDecl = IsNestedDecl,
                ParentDecl = ParentDecl,
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
            return (scope.GetSymbolInNamespace("System", new AstIdExpr("Delegate")) as DeclSymbol).Decl as AstClassDecl;
        }
    }
}
