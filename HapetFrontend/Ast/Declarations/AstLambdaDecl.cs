using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Enums;
using HapetFrontend.Types;
using Newtonsoft.Json;
using System.Xml.Linq;

namespace HapetFrontend.Ast.Declarations
{
    public class AstLambdaDecl : AstExpression
    {
        public List<AstParamDecl> Parameters { get; set; }
        public AstExpression ReturnType { get; set; }
        public AstBlockExpr Body { get; set; }

        [JsonIgnore]
        public FunctionType FunctionType => OutType as FunctionType;

        public override string AAAName => nameof(AstLambdaDecl);

        public AstLambdaDecl(List<AstParamDecl> parameters, AstBlockExpr body, AstExpression retType, ILocation location = null)
            : base(location)
        {
            this.Parameters = parameters;
            this.ReturnType = retType;
            this.Body = body;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstLambdaDecl(
                Parameters.Select(x => x.GetDeepCopy() as AstParamDecl).ToList(),
                Body.GetDeepCopy() as AstBlockExpr,
                ReturnType.GetDeepCopy() as AstExpression,
                Location)
            {
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
