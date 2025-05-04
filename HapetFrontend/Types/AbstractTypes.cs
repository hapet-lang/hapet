using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using Newtonsoft.Json;
using System.Xml.Linq;

namespace HapetFrontend.Types
{
    public abstract class AbstractType : HapetType
    {
        protected AbstractType() : base(0, 0) { }
    }

    /// <summary>
    /// This is like 'var a = ...'
    /// </summary>
    public class VarType : AbstractType
    {
        public static VarType Instance { get; } = new VarType();

        public override string TypeName => "var";

        public override AstExpression GetAst()
        {
            return new AstNestedExpr(new AstIdExpr(ToString()), null);
        }

        private VarType()
        {
        }

        public override string ToString() => $"var";
    }

    /// <summary>
    /// The type is used to describe T-like type with its constrains
    /// </summary>
    public class GenericType : AbstractType
    {
        public AstIdExpr Name { get; set; }
        public List<AstNestedExpr> Constrains { get; set; }

        public override string TypeName => "generic";

        public override AstExpression GetAst()
        {
            return new AstNestedExpr(Name, null);
        }

        private GenericType(AstIdExpr name, List<AstNestedExpr> constrains)
        {
            Name = name;
            Constrains = constrains;
        }

        public override string ToString() => Name.Name;
    }
}
