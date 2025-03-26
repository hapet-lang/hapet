using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
    /// <summary>
    /// Used when there are two identifiers at once like (Random rand ...)
    /// </summary>
    internal class AstUnknownDecl : AstDeclaration
    {
        /// <summary>
        /// Name could be not only AstId but also a tuple like
        /// var (a, b) = SomeCringeFunc();
        /// So this var is not null when the name is tupled
        /// </summary>
        public AstTupleExpr TupledName { get; set; }

        public override string AAAName => nameof(AstUnknownDecl);

        public AstUnknownDecl(AstExpression type, AstIdExpr name, ILocation Location = null) : base(name, "", Location)
        {
            Type = type;
        }

        public override AstStatement GetDeepCopy()
        {
            throw new NotImplementedException($"{nameof(GetDeepCopy)} should not be called over {nameof(AstUnknownDecl)}");
        }
    }
}
