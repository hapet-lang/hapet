using System.Collections.Generic;

namespace HapetFrontend.Ast.Expressions
{
    /// <summary>
    /// Used for cringe like:
    /// var a, b, c = FuncThatReturnsTuple();
    /// </summary>
    public class AstIdTupledExpr : AstIdExpr
    {
        public override string AAAName => nameof(AstIdGenericExpr);

        /// <summary>
        /// To handle real names
        /// </summary>
        public List<AstIdExpr> RealNames { get; } = new List<AstIdExpr>();

        public AstIdTupledExpr(List<AstIdExpr> names, ILocation location = null) : base(string.Empty, location)
        {
            RealNames.AddRange(names);
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstIdTupledExpr(
                RealNames.Select(x => x.GetDeepCopy() as AstIdExpr).ToList(),
                Location)
            {
                FindSymbol = FindSymbol,
                Suffix = Suffix,
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }

        public override AstIdTupledExpr GetCopy(string name = "")
        {
            var newId = new AstIdTupledExpr(RealNames, Location)
            {
                Suffix = this.Suffix,
                Parent = this.Parent,
                Scope = this.Scope,
                IsCompileTimeValue = this.IsCompileTimeValue,
                OutType = this.OutType,
                OutValue = this.OutValue,
                SourceFile = this.SourceFile,
            };
            return newId;
        }
    }
}
