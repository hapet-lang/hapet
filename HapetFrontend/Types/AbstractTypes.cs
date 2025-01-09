using HapetFrontend.Ast.Declarations;
using Newtonsoft.Json;

namespace HapetFrontend.Types
{
    public abstract class AbstractType : HapetType
    {
        protected AbstractType() : base(0, 0) { }
    }

    /// <summary>
    /// This is like 'this.a = ...' in a class
    /// </summary>
    public class ThisType : AbstractType
    {
        public HapetType ClassType { get; }

        public override string TypeName => "this";

        public ThisType(HapetType classType)
        {
            this.ClassType = classType;
        }

        public override string ToString()
        {
            return "this";
        }
    }

    /// <summary>
    /// This is like 'var a = ...'
    /// </summary>
    public class VarType : AbstractType
    {
        public static VarType Instance { get; } = new VarType();

        public override string TypeName => "var";

        private VarType()
        {
        }

        public override string ToString() => $"var";
    }
}
