using HapetFrontend.Ast.Declarations;
using HapetFrontend.Scoping;
using System.Text;

namespace HapetFrontend.Ast.Expressions
{
    public class AstNewExpr : AstExpression
    {
        /// <summary>
        /// The type that has to be created
        /// </summary>
        public AstNestedExpr TypeName { get; set; }

        /// <summary>
        /// The arguments to be passed into type constructor
        /// </summary>
        public List<AstArgumentExpr> Arguments { get; set; }

        /// <summary>
        /// Symbol of ctor for the new expr
        /// </summary>
        public DeclSymbol ConstructorSymbol { get; set; }

        /// <summary>
        /// For easier inference
        /// </summary>
        public bool IsTupleCreation { get; set; }

        /// <summary>
        /// The 'new' expr could be used with 'unsafe' kw so allocation will be
        /// done using 'Marshal.Malloc' but not 'Gc.Malloc'
        /// </summary>
        public bool IsUnsafeNew { get; set; }

        public override string AAAName => nameof(AstNewExpr);

        public AstNewExpr(AstNestedExpr typeName, List<AstArgumentExpr> arguments = null, ILocation location = null)
            : base(location)
        {
            this.TypeName = typeName;
            this.Arguments = arguments;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstNewExpr(
                TypeName.GetDeepCopy() as AstNestedExpr,
                Arguments.Select(x => x.GetDeepCopy() as AstArgumentExpr).ToList(),
                Location)
            {
                ConstructorSymbol = ConstructorSymbol,
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
                TupleNameList = TupleNameList,
            };
            return copy;
        }
    }
}
