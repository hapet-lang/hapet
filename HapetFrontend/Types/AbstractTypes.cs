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

        public override AstStatement GetAst()
        {
            return new AstNestedExpr(new AstIdExpr(ToString()), null);
        }

        private VarType()
        {
        }

        public override string ToString() => $"var";
    }
}
