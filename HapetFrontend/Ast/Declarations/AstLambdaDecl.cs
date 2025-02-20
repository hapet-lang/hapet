using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;
using Newtonsoft.Json;

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
    }
}
