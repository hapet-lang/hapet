using HapetFrontend.Ast.Declarations;
using HapetFrontend.Scoping;

namespace HapetFrontend.Ast.Expressions
{
    public class AstIdGenericExpr : AstIdExpr
    {
        public override string AAAName => nameof(AstIdGenericExpr);

        /// <summary>
        /// Types from shite like:
        /// 'Dictionary<string, MyNamespace.Anime322>' ...
        /// or
        /// 'Dictionary<(int, string), AnimeCls>' ...
        /// </summary>
        public List<AstExpression> GenericRealTypes { get; } = new List<AstExpression>();

        public AstIdGenericExpr(string name, List<AstExpression> genericRealTypes, ILocation location = null) : base(name, location)
        {
            GenericRealTypes.AddRange(genericRealTypes);
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstIdGenericExpr(
                Name,
                GenericRealTypes.Select(x => x.GetDeepCopy() as AstExpression).ToList(),
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

        public static AstIdGenericExpr FromAstIdExpr(AstIdExpr astIdExpr, List<AstExpression> genericRealTypes = null)
        {
            genericRealTypes ??= new List<AstExpression>();

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

        public override AstIdGenericExpr GetCopy(string name = "")
        {
            string newName = string.IsNullOrWhiteSpace(name) ? Name : name;
            var newId = new AstIdGenericExpr(
                newName, 
                GenericRealTypes.Select(x => x.GetDeepCopy() as AstExpression).ToList(), 
                Location)
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

        public override AstIdExpr GetPureIdExpr()
        {
            return new AstIdExpr(Name, Location)
            {
                Suffix = this.Suffix,
                Parent = this.Parent,
                Scope = this.Scope,
                OutType = this.OutType,
                OutValue = this.OutValue,
                SourceFile = this.SourceFile,
            };
        }
    }
}
