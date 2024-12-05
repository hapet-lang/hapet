using HapetFrontend.Types;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
	public class AstDefaultExpr : AstExpression
	{
		[DebuggerStepThrough]
		public AstDefaultExpr(ILocation Location = null) : base(Location)
		{
		}

		public static AstExpression GetDefaultValueForType(HapetType tp, AstExpression orig)
		{
			AstExpression outExpr;
			switch (tp)
			{
				case StringType:
					outExpr = new AstStringExpr("", null, orig);
					break;
				case IntType:
					outExpr = new AstNumberExpr(NumberData.FromInt(0), null, orig);
					break;
				case FloatType:
					outExpr = new AstNumberExpr(NumberData.FromDouble(0), null, orig);
					break;
				case CharType:
					outExpr = new AstCharExpr("", orig);
					break;
				case BoolType:
					outExpr = new AstBoolExpr(false, orig);
					break;
				case ClassType:
				case PointerType:
					outExpr = new AstNullExpr(tp, orig);
					break;
				// TODO: other shite
				default:
					outExpr = null;
					break;
			}
			if (outExpr != null && orig != null)
				outExpr.Scope = orig.Scope;
			return outExpr;
		}
	}
}
