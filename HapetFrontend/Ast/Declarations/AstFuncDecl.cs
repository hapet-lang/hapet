using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Enums;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
	public class AstFuncDecl : AstDeclaration
	{
		public CallingConvention CallingConvention { get; } = CallingConvention.Default;

		public List<AstParamDecl> Parameters { get; set; }
		public AstParamDecl Returns { get; }

		public AstBlockStmt Body { get; set; }

		/// <summary>
		/// The class that contains the function
		/// </summary>
		public AstClassDecl ContainingClass { get; set; }

		// TODO: do i need it?
		// public Scope SubScope { get; set; }

		public AstFuncDecl(List<AstParamDecl> parameters, AstParamDecl returns, AstIdExpr name, string doc = "", ILocation Location = null) : base(name, doc, Location)
		{
			Type = new AstIdExpr("func", Location);
			Type.OutType = new FunctionType(this);

			Parameters = parameters;
			Returns = returns;
		}
	}
}
