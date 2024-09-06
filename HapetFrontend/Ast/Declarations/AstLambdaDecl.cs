using HapetFrontend.Ast.Statements;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
	public class AstLambdaDecl : AstExpression
	{
		public List<AstParamDecl> Parameters { get; set; }
		public AstExpression ReturnType { get; set; }
		public AstBlockStmt Body { get; set; }

		public FunctionType FunctionType => OutType as FunctionType;

		public AstLambdaDecl(List<AstParamDecl> parameters, AstBlockStmt body, AstExpression retType, ILocation location = null)
			: base(location)
		{
			this.Parameters = parameters;
			this.ReturnType = retType;
			this.Body = body;
		}
	}
}
