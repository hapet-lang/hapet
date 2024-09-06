using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
	/// <summary>
	/// Used when there are two identifiers at once like (Random rand ...)
	/// </summary>
	public class UnknownDecl : AstDeclaration
	{
		public UnknownDecl(AstIdExpr name, AstIdExpr type, ILocation Location = null) : base(name, "", Location)
		{
			Type = type;
		}
	}
}
