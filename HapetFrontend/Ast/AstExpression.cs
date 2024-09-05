using HapetFrontend.Scoping;
using HapetFrontend.Types;

namespace HapetFrontend.Ast
{
	public class AstExpression : AstStatement
	{
		/// <summary>
		/// The type of the value that I get from an expr
		/// </summary>
		public HapetType OutType { get; set; }

		public AstExpression(ILocation Location = null) : base(Location)
		{
		}
	}
}
