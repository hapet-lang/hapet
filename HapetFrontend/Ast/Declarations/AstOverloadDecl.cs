using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Enums;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using System.Text;

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
                Body?.GetDeepCopy() as AstBlockExpr,
                Name.GetDeepCopy() as AstIdExpr,
                Documentation, Location)
            {
                OverloadType = OverloadType,
                Operator = Operator,

                IsPropertyFunction = IsPropertyFunction,
                BaseCtorCall = BaseCtorCall?.GetDeepCopy() as AstBaseCtorStmt,
                ThisCtorCall = ThisCtorCall?.GetDeepCopy() as AstBaseCtorStmt,
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

        public static string GenerateName(OverloadType overloadType, string op, List<AstParamDecl> types)
        {
            string opNorm = string.Empty;
            switch (op)
            {
                case "+": opNorm = "pl"; break;
                case "-": opNorm = "mn"; break;
                case "*": opNorm = "pr"; break;
                case "/": opNorm = "dv"; break;
                case "%": opNorm = "os"; break;
            }

            string ovNorm = string.Empty;
            switch (overloadType)
            {
                case OverloadType.UnaryOperator: ovNorm = "un"; break;
                case OverloadType.BinaryOperator: ovNorm = "bn"; break;
                case OverloadType.ExplicitCast: ovNorm = "ex"; break;
                case OverloadType.ImplicitCast: ovNorm = "im"; break;
            }

            StringBuilder typesNorm = new StringBuilder();
            foreach (var t in types)
            {
                typesNorm.Append($"_{HapetType.AsString(t.Type.OutType)}");
            }
            return $"{ovNorm}_{opNorm}{typesNorm}";
        }
    }
}
