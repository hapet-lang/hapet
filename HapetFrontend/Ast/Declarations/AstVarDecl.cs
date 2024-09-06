using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
	public class AstVarDecl : AstDeclaration
	{
		/// <summary>
		/// A value to init the var
		/// </summary>
		public AstExpression Initializer { get; set; }

		public AstVarDecl(AstExpression type, AstIdExpr name, AstExpression ini = null, string doc = "", ILocation Location = null) : base(name, doc, Location)
		{
			Type = type;
			Initializer = ini;
			//Type = new AstIdExpr("var", Location);
			//Type.OutType = new VarType(this);
			if (Type.OutType is VarType)
			{
				// TODO: resolve type via Initializer
			}
		}
	}
}
