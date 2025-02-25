using HapetFrontend.Scoping;

namespace HapetFrontend.Ast.Expressions
{
    public class AstIdGenericExpr : AstIdExpr
    {
        public override string AAAName => nameof(AstIdGenericExpr);

        /// <summary>
        /// Types from shite like:
        /// 'Dictionary<string, MyNamespace.Anime322>' ...
        /// </summary>
        public List<AstNestedExpr> GenericRealTypes { get; } = new List<AstNestedExpr>();

        public AstIdGenericExpr(string name, List<AstNestedExpr> genericRealTypes, ILocation location = null) : base(name, location)
        {
            GenericRealTypes.AddRange(genericRealTypes);
        }

        public static AstIdGenericExpr FromAstIdExpr(AstIdExpr astIdExpr, List<AstNestedExpr> genericRealTypes = null)
        {
            genericRealTypes ??= new List<AstNestedExpr>();

            var gen = new AstIdGenericExpr(astIdExpr.Name, genericRealTypes, astIdExpr.Location)
            {
                FindSymbol = astIdExpr.FindSymbol,
                IsCompileTimeValue = astIdExpr.IsCompileTimeValue,
                OutType = astIdExpr.OutType,
                OutValue = astIdExpr.OutValue,
                Parent = astIdExpr.Parent,
                Scope = astIdExpr.Scope,
                SourceFile = astIdExpr.SourceFile,
                Suffix = astIdExpr.Suffix,
            };
            return gen;
        }
    }
}
