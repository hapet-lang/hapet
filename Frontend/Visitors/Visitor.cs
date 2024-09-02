using Frontend.Ast;
using Frontend.Ast.Declarations;
using Frontend.Ast.Expressions;
using Frontend.Ast.Statements;

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
		ReturnType VisitUsingExpr(AstUsingExpr expr, DataType data = default);
		ReturnType VisitEmptyExpr(AstEmptyExpr expr, DataType data = default);
		ReturnType VisitBlockExpr(AstBlockExpr expr, DataType data = default);

		ReturnType VisitExprStmt(AstExprStmt stmt, DataType data = default);
		ReturnType VisitAttachStmt(AstAttachStmt stmt, DataType data = default);
		ReturnType VisitEmptyStmt(AstEmptyStmt stmt, DataType data = default);
		ReturnType VisitAssignmentStmt(AstAssignment stmt, DataType data = default);

		ReturnType VisitConstantDeclaration(AstConstantDeclaration decl, DataType data = default);
	}

	public abstract class VisitorBase<ReturnType, DataType> : IVisitor<ReturnType, DataType>
	{
		public virtual ReturnType VisitDirective(AstDirective direc, DataType data = default) => default;

		public virtual ReturnType VisitIdExpr(AstIdExpr expr, DataType data = default) => default;
		public virtual ReturnType VisitUsingExpr(AstUsingExpr expr, DataType data = default) => default;
		public virtual ReturnType VisitEmptyExpr(AstEmptyExpr expr, DataType data = default) => default;
		public virtual ReturnType VisitBlockExpr(AstBlockExpr expr, DataType data = default) => default;

		public virtual ReturnType VisitExprStmt(AstExprStmt stmt, DataType data = default) => default;
		public virtual ReturnType VisitAttachStmt(AstAttachStmt stmt, DataType data = default) => default;
		public virtual ReturnType VisitEmptyStmt(AstEmptyStmt stmt, DataType data = default) => default;
		public virtual ReturnType VisitAssignmentStmt(AstAssignment stmt, DataType data = default) => default;

		public virtual ReturnType VisitConstantDeclaration(AstConstantDeclaration decl, DataType data = default) => default;
	}
}
