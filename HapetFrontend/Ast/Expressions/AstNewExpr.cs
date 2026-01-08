using HapetFrontend.Ast.Declarations;
using HapetFrontend.Scoping;
using System.Text;
using System.Xml.Linq;

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

        /// <summary>
        /// Used in LSP
        /// </summary>
        public ILocation UnsafeTokenLocation { get; set; }

        public override string AAAName => nameof(AstNewExpr);

        public AstNewExpr(AstNestedExpr typeName, List<AstArgumentExpr> arguments = null, ILocation location = null)
            : base(location)
        {
            arguments ??= new List<AstArgumentExpr>();

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
                IsSyntheticStatement = IsSyntheticStatement,
                ConstructorSymbol = ConstructorSymbol,
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                IsUnsafeNew = IsUnsafeNew,
                Scope = Scope,
                SourceFile = SourceFile,
                TupleNameList = TupleNameList,
                UnsafeTokenLocation = UnsafeTokenLocation,
            };
            return copy;
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            if (TypeName == oldChild)
                TypeName = newChild as AstNestedExpr;
            else if (Arguments.Contains(oldChild))
            {
                int index = Arguments.IndexOf(oldChild as AstArgumentExpr);
                Arguments[index] = newChild as AstArgumentExpr;
            }
        }
    }
}
