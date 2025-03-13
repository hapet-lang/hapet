using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Enums;
using HapetFrontend.Parsing;

namespace HapetFrontend.Ast.Declarations
{
    public class AstOverloadDecl : AstFuncDecl
    {
        public OverloadType OverloadType { get; set; }

        /// <summary>
        /// For operator overloading only
        /// </summary>
        public string Operator { get; set; }

        public override string AAAName => nameof(AstOverloadDecl);

        public AstOverloadDecl(List<AstParamDecl> parameters, AstExpression returns, AstBlockExpr body, AstIdExpr name, string doc = "", ILocation location = null) 
            : base(parameters, returns, body, name, doc, location)
        {
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstOverloadDecl(
                Parameters.Select(x => x.GetDeepCopy() as AstParamDecl).ToList(),
                Returns.GetDeepCopy() as AstNestedExpr,
                Body.GetDeepCopy() as AstBlockExpr,
                Name.GetDeepCopy() as AstIdExpr,
                Documentation, Location)
            {
                OverloadType = OverloadType,
                Operator = Operator,

                IsPropertyFunction = IsPropertyFunction,
                ContainingParent = ContainingParent.GetDeepCopy() as AstDeclaration,
                BaseCtorCall = BaseCtorCall.GetDeepCopy() as AstBaseCtorStmt,
                CallingConvention = CallingConvention,
                ClassFunctionType = ClassFunctionType,
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
            };
            copy.Attributes.AddRange(Attributes);
            copy.SpecialKeys.AddRange(SpecialKeys);
            return copy;
        }

        public static string GenerateName(OverloadType overloadType, string op, AstNestedExpr type)
        {
            string opNorm = string.Empty;
            switch (op)
            {
                case "+": opNorm = "Plus"; break;
                case "-": opNorm = "Minus"; break;
                case "*": opNorm = "Prod"; break;
                case "/": opNorm = "Div"; break;
                case "%": opNorm = "Proc"; break;
            }

            string typeFlatten = type == null ? string.Empty : type.TryFlatten(null, null);

            return $"{overloadType}_{opNorm}_{typeFlatten}";
        }
    }
}
