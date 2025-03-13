using HapetFrontend.Ast.Expressions;
using HapetFrontend.Scoping;
using HapetFrontend.Types;

namespace HapetFrontend.Ast
{
    public abstract class AstExpression : AstStatement
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

        public override string AAAName => nameof(AstExpression);

        public AstExpression(ILocation location = null) : base(location)
        {
        }

        public override string ToString()
        {
            return HapetType.AsString(OutType);
        }
    }
}
