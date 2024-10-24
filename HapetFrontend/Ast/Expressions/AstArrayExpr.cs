using HapetFrontend.Ast.Declarations;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Expressions
{
	public class AstArrayExpr : AstExpression
	{
		/// <summary>
		/// The expression on which the array is applied
		/// </summary>
		public AstExpression SubExpression { get; set; }

		public AstArrayExpr(AstExpression sub, ILocation Location = null) : base(Location)
		{
			SubExpression = sub;
		}

		private static AstStructDecl GenerateArrayStructExpr()
		{
			// creating the struct and its scope
			AstStructDecl arrStruct = new AstStructDecl(new AstIdExpr("array.type"), ""); // TODO: doc string
			// TODO: doc string
			AstVarDecl sizeField = new AstVarDecl(new AstNestedExpr(new AstIdExpr("int"), null), new AstIdExpr("Length"), new AstNumberExpr((NumberData)0), "");
			AstVarDecl bufField = new AstVarDecl(new AstNestedExpr(new AstPointerExpr(new AstIdExpr("byte")), null), new AstIdExpr("Buffer"), new AstNullExpr(), "");
			arrStruct.Declarations.Add(sizeField);
			arrStruct.Declarations.Add(bufField);
			return arrStruct;
		}

		// the array struct is always like that
		private static AstStructDecl _arrayStruct;
		public static AstStructDecl ArrayStruct
		{
			get
			{
				_arrayStruct ??= GenerateArrayStructExpr();
				return _arrayStruct;
			}
		}
	}
}
