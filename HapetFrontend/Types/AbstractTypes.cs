using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Types
{
    public abstract class AbstractType : HapetType
    {
        protected AbstractType() : base(0, 0) { }
    }

    /// <summary>
    /// This is like 'var a = ...'
    /// </summary>
    public class VarType : AbstractType
    {
        public static VarType Instance { get; } = new VarType();

        public override string TypeName => "var";

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstNestedExpr(new AstIdExpr(ToString())
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            }, null)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        private VarType()
        {
        }

        public override string ToString() => $"var";
    }

    /// <summary>
    /// The type is used to describe T-like type with its constrains
    /// </summary>
    public class GenericType : AbstractType
    {
        public AstGenericDecl Declaration { get; }

        public static GenericType LiteralType { get; } = new GenericType(null);

        public override string TypeName => "generic";

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstNestedExpr(Declaration.Name.GetCopy(), null)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        public GenericType(AstGenericDecl decl)
        {
            Declaration = decl;
        }

        public override string ToString() => Declaration.Name.Name;

        public static bool AreTypesTheSameIncludingGenerics(HapetType t1, HapetType t2)
        {
            if (t1 is ArrayType at1 && t2 is ArrayType at2)
            {
                return AreTypesTheSameIncludingGenerics(at1.TargetType, at2.TargetType);
            }
            else if (t1 is PointerType pt1 && t2 is PointerType pt2)
            {
                return AreTypesTheSameIncludingGenerics(pt1.TargetType, pt2.TargetType);
            }
            return t1 == t2;
        }
    }

    public class NullType : AbstractType
    {
        public HapetType TargetType { get; }

        public static NullType LiteralType { get; } = new NullType(null);

        public override string TypeName => "null";

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstNestedExpr(new AstIdExpr("null", iniExpr?.Location)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
            }, null)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        public NullType(HapetType targetType)
        {
            TargetType = targetType;
        }

        public override string ToString() => "null";
    }
}
