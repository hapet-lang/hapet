using HapetFrontend.Ast.Declarations;
using HapetFrontend.Scoping;
using System.Xml.Linq;

namespace HapetFrontend.Ast.Expressions
{
    public class AstCallExpr : AstExpression
    {
        /// <summary>
        /// The type (for static funcs) or object (for non static) where the func is located
        /// Could be another expr like AstCast when:
        /// (anime as AnimeCls).FuncToCall();
        /// </summary>
        public AstExpression TypeOrObjectName { get; set; }

        /// <summary>
        /// If the call is of static func
        /// </summary>
        public bool StaticCall { get; set; }

        /// <summary>
        /// The func name
        /// </summary>
        public AstIdExpr FuncName { get; set; }

        /// <summary>
        /// The arguments to be passed into func
        /// </summary>
        public List<AstArgumentExpr> Arguments { get; set; }

        /// <summary>
        /// 'true' if the call should be handled as an external call
        /// </summary>
        public bool IsSpecialExternalCall { get; set; }

        public override string AAAName => nameof(AstCallExpr);

        public AstCallExpr(AstNestedExpr typeOrObjectName, AstIdExpr funcName, List<AstArgumentExpr> arguments = null, ILocation location = null)
            : base(location)
        {
            this.TypeOrObjectName = typeOrObjectName;
            this.FuncName = funcName;
            this.Arguments = arguments ?? new List<AstArgumentExpr>();
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstCallExpr(
                TypeOrObjectName?.GetDeepCopy() as AstNestedExpr,
                FuncName.GetDeepCopy() as AstIdExpr,
                Arguments.Select(x => x.GetDeepCopy() as AstArgumentExpr).ToList(),
                Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                StaticCall = StaticCall,
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
                TupleNameList = TupleNameList,
            };
            return copy;
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            if (TypeOrObjectName == oldChild)
                TypeOrObjectName = newChild as AstExpression;
            else if (FuncName == oldChild)
                FuncName = newChild as AstIdExpr;
            else if (Arguments.Contains(oldChild))
            {
                int index = Arguments.IndexOf(oldChild as AstArgumentExpr);
                Arguments[index] = newChild as AstArgumentExpr;
            }
        }
    }
}
