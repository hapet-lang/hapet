using Frontend.Ast;
using Frontend.Ast.Declarations;
using Frontend.Ast.Expressions;

namespace Frontend.Visitors
{
	public interface IVisitorAcceptor
	{
		T Accept<T, D>(IVisitor<T, D> visitor, D data = default);
	}

	public interface IVisitor<ReturnType, DataType>
	{
		ReturnType VisitDirective(AstDirective direc, DataType data = default);

		ReturnType VisitIdExpr(AstIdExpr expr, DataType data = default);

		ReturnType VisitConstantDeclaration(AstConstantDeclaration decl, DataType data = default);
	}

	public abstract class VisitorBase<ReturnType, DataType> : IVisitor<ReturnType, DataType>
	{
		public virtual ReturnType VisitDirective(AstDirective direc, DataType data = default) => default;

		public virtual ReturnType VisitIdExpr(AstIdExpr expr, DataType data = default) => default;

		public virtual ReturnType VisitConstantDeclaration(AstConstantDeclaration decl, DataType data = default) => default;
	}
}
