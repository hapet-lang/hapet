using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Enums;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
    public class AstLambdaExpr : AstExpression
    {
        /// <summary>
        /// Keys like public/static/virtual and other
        /// </summary>
        public List<Token> SpecialKeys { get; private set; } = new List<Token>();

        public List<AstParamDecl> Parameters { get; set; }
        public AstExpression Returns { get; set; }
        public AstBlockExpr Body { get; set; }

        /// <summary>
        /// Getting symbol of itself
        /// </summary>
        public ISymbol Symbol { get; set; }

        /// <summary>
        /// The inner scope of the decl. Used to get access to it's content
		/// Not for every decl!!!
        /// </summary>
        public Scope SubScope { get; set; }

        public LambdaType FunctionType => OutType as LambdaType;

        public override string AAAName => nameof(AstLambdaExpr);

        public AstLambdaExpr(List<AstParamDecl> parameters, AstBlockExpr body, AstExpression retType, ILocation location = null)
            : base(location)
        {
            OutType = new LambdaType(this);
            Symbol = new StmtSymbol(new AstIdExpr("lambdaSymbol"), this);

            this.Parameters = parameters;
            this.Returns = retType;
            this.Body = body;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstLambdaExpr(
                Parameters.Select(x => x.GetDeepCopy() as AstParamDecl).ToList(),
                Body.GetDeepCopy() as AstBlockExpr,
                Returns.GetDeepCopy() as AstExpression,
                Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }

        /// <summary>
        /// Returns string with return type and args types but without name of func
        /// USED FOR FUNC and LAMBDA
        /// </summary>
        /// <returns></returns>
        public string ToCringeString()
        {
            string args;

            // the func is static...
            args = string.Join(":", Parameters.Select(p =>
            {
                return p.Type.OutType.ToString();
            }));

            if (Returns.OutType != HapetType.CurrentTypeContext.VoidTypeInstance)
                return $"({Returns.OutType}:({args}))";
            else
                return $"(void:({args}))";
        }
    }
}
