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
		ReturnType VisitParameter(AstParameter par, DataType data = default);

		ReturnType VisitDirective(AstDirective direc, DataType data = default);

		ReturnType VisitIdExpr(AstIdExpr expr, DataType data = default);
		ReturnType VisitUsingExpr(AstUsingExpr expr, DataType data = default);
		ReturnType VisitEmptyExpr(AstEmptyExpr expr, DataType data = default);
		ReturnType VisitBlockExpr(AstBlockExpr expr, DataType data = default);
		ReturnType VisitClassTypeExpr(AstClassTypeExpr expr, DataType data = default);
		ReturnType VisitTupleExpr(AstTupleExpr expr, DataType data = default);
		ReturnType VisitBinaryExpr(AstBinaryExpr expr, DataType data = default);
		ReturnType VisitRangeExpr(AstRangeExpr expr, DataType data = default);
		ReturnType VisitUnaryExpr(AstUnaryExpr expr, DataType data = default);
		ReturnType VisitArgumentExpr(AstArgument expr, DataType data = default);
		ReturnType VisitFuncExpr(AstFuncExpr expr, DataType data = default);
		ReturnType VisitAddressOfExpr(AstAddressOfExpr expr, DataType data = default);
		ReturnType VisitDerefExpr(AstDereferenceExpr expr, DataType data = default);
		ReturnType VisitCallExpr(AstCallExpr expr, DataType data = default);
		ReturnType VisitLambdaExpr(AstLambdaExpr expr, DataType data = default);
		ReturnType VisitArrayAccessExpr(AstArrayAccessExpr expr, DataType data = default);

		ReturnType VisitExprStmt(AstExprStmt stmt, DataType data = default);
		ReturnType VisitAttachStmt(AstAttachStmt stmt, DataType data = default);
		ReturnType VisitEmptyStmt(AstEmptyStmt stmt, DataType data = default);
		ReturnType VisitAssignmentStmt(AstAssignment stmt, DataType data = default);
		ReturnType VisitDirectiveStmt(AstDirectiveStmt stmt, DataType data = default);

		ReturnType VisitConstantDecl(AstConstantDecl decl, DataType data = default);
		ReturnType VisitVariableDecl(AstVariableDecl decl, DataType data = default);
	}

	public abstract class VisitorBase<ReturnType, DataType> : IVisitor<ReturnType, DataType>
	{
		public virtual ReturnType VisitParameter(AstParameter par, DataType data = default) => default;

		public virtual ReturnType VisitDirective(AstDirective direc, DataType data = default) => default;

		public virtual ReturnType VisitIdExpr(AstIdExpr expr, DataType data = default) => default;
		public virtual ReturnType VisitUsingExpr(AstUsingExpr expr, DataType data = default) => default;
		public virtual ReturnType VisitEmptyExpr(AstEmptyExpr expr, DataType data = default) => default;
		public virtual ReturnType VisitBlockExpr(AstBlockExpr expr, DataType data = default) => default;
		public virtual ReturnType VisitClassTypeExpr(AstClassTypeExpr expr, DataType data = default) => default;
		public virtual ReturnType VisitTupleExpr(AstTupleExpr expr, DataType data = default) => default;
		public virtual ReturnType VisitBinaryExpr(AstBinaryExpr expr, DataType data = default) => default;
		public virtual ReturnType VisitRangeExpr(AstRangeExpr expr, DataType data = default) => default;
		public virtual ReturnType VisitUnaryExpr(AstUnaryExpr expr, DataType data = default) => default;
		public virtual ReturnType VisitArgumentExpr(AstArgument expr, DataType data = default) => default;
		public virtual ReturnType VisitFuncExpr(AstFuncExpr expr, DataType data = default) => default;
		public virtual ReturnType VisitAddressOfExpr(AstAddressOfExpr expr, DataType data = default) => default;
		public virtual ReturnType VisitDerefExpr(AstDereferenceExpr expr, DataType data = default) => default;
		public virtual ReturnType VisitCallExpr(AstCallExpr expr, DataType data = default) => default;
		public virtual ReturnType VisitLambdaExpr(AstLambdaExpr expr, DataType data = default) => default;
		public virtual ReturnType VisitArrayAccessExpr(AstArrayAccessExpr expr, DataType data = default) => default;

		public virtual ReturnType VisitExprStmt(AstExprStmt stmt, DataType data = default) => default;
		public virtual ReturnType VisitAttachStmt(AstAttachStmt stmt, DataType data = default) => default;
		public virtual ReturnType VisitEmptyStmt(AstEmptyStmt stmt, DataType data = default) => default;
		public virtual ReturnType VisitAssignmentStmt(AstAssignment stmt, DataType data = default) => default;
		public virtual ReturnType VisitDirectiveStmt(AstDirectiveStmt stmt, DataType data = default) => default;

		public virtual ReturnType VisitConstantDecl(AstConstantDecl decl, DataType data = default) => default;
		public virtual ReturnType VisitVariableDecl(AstVariableDecl decl, DataType data = default) => default;
	}
}
