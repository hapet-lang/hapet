using HapetFrontend.Ast.Declarations;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstArrayCreateExpr : AstExpression, ICloneable
    {
        /// <summary>
        /// The type of which array is created
        /// </summary>
        public AstExpression TypeName { get; set; }
        /// <summary>
        /// The size expressions 
        /// This is a list because of ndim arrays
        /// </summary>
        public List<AstExpression> SizeExprs { get; set; }
        /// <summary>
        /// Init values of the array
        /// </summary>
        public List<AstExpression> Elements { get; set; }

        /// <summary>
        /// The 'new' expr could be used with 'unsafe' kw so allocation will be
        /// done using 'Marshal.Malloc' but not 'Gc.Malloc'
        /// </summary>
        public bool IsUnsafeNew { get; set; }

        /// <summary>
        /// 'true' if the array has to be allocated on stack
        /// </summary>
        public bool IsStackAlloc { get; set; }

        public override string AAAName => nameof(AstArrayCreateExpr);

        public AstArrayCreateExpr(AstExpression type, List<AstExpression> sizeExprs, List<AstExpression> elements, ILocation location = null) : base(location)
        {
            TypeName = type;
            SizeExprs = sizeExprs;
            Elements = elements;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstArrayCreateExpr(
                TypeName.GetDeepCopy() as AstExpression,
                SizeExprs.Select(x => x.GetDeepCopy() as AstExpression).ToList(),
                Elements.Select(x => x.GetDeepCopy() as AstExpression).ToList(),
                Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                IsUnsafeNew = IsUnsafeNew,
                IsStackAlloc = IsStackAlloc,
                Scope = Scope,
                SourceFile = SourceFile,
                TupleNameList = TupleNameList,
            };
            return copy;
        }

        public object Clone()
        {
            return new AstArrayCreateExpr(TypeName, new List<AstExpression>(SizeExprs), new List<AstExpression>(Elements), Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                OutType = OutType,
                Parent = Parent,
                Scope = Scope,
                SourceFile = SourceFile,
                OutValue = OutValue,
                IsUnsafeNew = IsUnsafeNew,
                IsStackAlloc = IsStackAlloc,
                IsCompileTimeValue = IsCompileTimeValue
            };
        }
    }
}
