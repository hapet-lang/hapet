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

        private object _outValue;
        /// <summary>
        /// The value that returned from expr (if it is compile time value like literals or consts)
        /// </summary>
        public object OutValue
        {
            get => _outValue;
            set
            {
                _outValue = value;
                IsCompileTimeValue = _outValue != null;
            }
        }

        /// <summary>
        /// Is the expression could be computed while compiling 
        /// </summary>
        public bool IsCompileTimeValue { get; protected set; } = false;

        public AstExpression(ILocation Location = null) : base(Location)
        {
        }

        public override string ToString()
        {
            // TODO: so the return type would be printed normally
            return base.ToString();
        }
    }
}
