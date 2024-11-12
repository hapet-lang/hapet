using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using System.Xml.Linq;

namespace HapetFrontend.Ast.Statements
{
	public class AstAttributeStmt : AstStatement
	{
		/// <summary>
		/// The name of the attribute
		/// </summary>
		public AstNestedExpr AttributeName { get; set; }

		/// <summary>
		/// Parameters of the attribute
		/// </summary>
		public List<AstExpression> Parameters { get; set; }

		public AstAttributeStmt(AstNestedExpr attrName, List<AstExpression> parameters, ILocation Location = null) : base(Location)
		{
			AttributeName = attrName;
			Parameters = parameters;
		}

		internal AttributeJson GetJson()
		{
			return new AttributeJson()
			{
				Name = AttributeName.TryFlatten(null, null),
				Values = Parameters.Select(x => x.OutValue).ToList()
			};
		}
	}

	public class AttributeJson
	{
		public string Name { get; set; }
		public List<object> Values { get; set; }

		public AstAttributeStmt GetAst()
		{
			List<AstExpression> pars = new List<AstExpression>();
			foreach (var v in Values)
			{
				if (v is int v1)
				{
					pars.Add(new AstNumberExpr(NumberData.FromInt(v1)));
				}
				else if (v is double v2)
				{
					pars.Add(new AstNumberExpr(NumberData.FromDouble(v2)));
				}
				else if (v is string str)
				{
					pars.Add(new AstStringExpr(str));
				}
				else
				{
					// TODO: anything else?
				}
			}
			return new AstAttributeStmt(Parser.ParseType(Name) as AstNestedExpr, pars);
		}
	}
}
