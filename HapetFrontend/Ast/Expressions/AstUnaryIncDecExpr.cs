namespace HapetFrontend.Ast.Expressions
{
    public class AstUnaryIncDecExpr : AstUnaryExpr
    {
        /// <summary>
        /// 'true' if --Anime or ++Anime
        /// 'false' if Anime-- or Anime--
        /// </summary>
        public bool IsPrefix { get; set; }

        public AstUnaryIncDecExpr(string op, AstExpression sub, ILocation location = null) : base(op, sub, location)
        {

        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstUnaryIncDecExpr(
                Operator,
                SubExpr.GetDeepCopy() as AstExpression,
                Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                ActualOperator = ActualOperator,
                IsCompileTimeValue = IsCompileTimeValue,
                IsPrefix = IsPrefix,
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
