using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using Newtonsoft.Json.Linq;
using System.Numerics;
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
            List<object> pars = new List<object>();
            foreach (var v in Parameters.Select(x => x.OutValue))
            {
                if (v is NumberData nd)
                {
                    if (nd.Type == Enums.NumberType.Float)
                        pars.Add(nd.DoubleValue);
                    else
                        pars.Add(nd.IntValue);
                }
                else
                {
                    pars.Add(v);
                }
            }
            return new AttributeJson()
            {
                Name = AttributeName.TryFlatten(null, null),
                Values = pars,
            };
        }
    }

    public class AttributeJson
    {
        public string Name { get; set; }
        public List<object> Values { get; set; }

        public AstAttributeStmt GetAst(Compiler compiler)
        {
            List<AstExpression> pars = new List<AstExpression>();
            foreach (var v in Values)
            {
                switch (v)
                {
                    case int:
                        pars.Add(new AstNumberExpr(NumberData.FromInt((int)v)));
                        break;
                    case long:
                        pars.Add(new AstNumberExpr(NumberData.FromInt((long)v)));
                        break;
                    case float:
                        pars.Add(new AstNumberExpr(NumberData.FromDouble((float)v)));
                        break;
                    case double:
                        pars.Add(new AstNumberExpr(NumberData.FromDouble((double)v)));
                        break;
                    case string s:
                        pars.Add(new AstStringExpr(s));
                        break;
                    default:
                        // TODO: anything else?
                        break;
                }
            }
            return new AstAttributeStmt(Parser.ParseType(Name, compiler) as AstNestedExpr, pars);
        }
    }
}
