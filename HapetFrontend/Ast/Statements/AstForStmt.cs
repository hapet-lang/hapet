using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Statements
{
	public class AstForStmt : AstStatement
	{
		/// <summary>
		/// The first param of for loop. Could be <see cref="AstVarDecl"/>, pure <see cref="AstExpression"/> or just a <see cref="null"/>
		/// </summary>
		public AstStatement FirstParam { get; set; }

		/// <summary>
		/// The second param of for loop. Could be pure <see cref="AstExpression"/> or just a <see cref="null"/>.
		/// Has to return <see cref="BoolType"/>
		/// </summary>
		public AstExpression SecondParam { get; set; }

		/// <summary>
		/// The third param of for loop. Could be pure <see cref="AstExpression"/> or just a <see cref="null"/>.
		/// </summary>
		public AstExpression ThirdParam { get; set; }

		/// <summary>
		/// The body of for loop
		/// </summary>
		public AstBlockExpr Body { get; set; }

		public AstForStmt(AstStatement first, AstExpression second, AstExpression third, AstBlockExpr body, ILocation location = null) : base(location)
		{
			FirstParam = first;
			SecondParam = second;
			ThirdParam = third;
			Body = body;
		}
	}
}
