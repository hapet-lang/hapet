using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Scoping;
using HapetFrontend.Types;

namespace HapetFrontend.Parsing.PostPrepare
{
	public partial class PostPrepare
	{
		private void PostPrepareTypeInference()
		{
			foreach (var (path, file) in _compiler.GetFiles())
			{
				_currentSourceFile = file;
				foreach (var stmt in file.Statements)
				{
					if (stmt is AstClassDecl classDecl)
					{
						PostPrepareClassInference(classDecl);
					}
				}
			}
		}

		private void PostPrepareClassInference(AstClassDecl classDecl)
		{
			foreach (var decl in classDecl.Declarations)
			{
				if (decl is AstFuncDecl funcDecl)
				{
					PostPrepareFunctionInference(funcDecl);
				}
			}
		}

		private void PostPrepareFunctionInference(AstFuncDecl funcDecl)
		{
			// inferencing parameters 
			foreach (var p in funcDecl.Parameters)
			{
				PostPrepareExprInference(p.Type);
				if (p.DefaultValue != null)
					PostPrepareExprInference(p.DefaultValue);
			}

			// inferencing return type 
			{
				PostPrepareExprInference(funcDecl.Returns);
			}

			if (funcDecl.Body != null)
			{
				PostPrepareBlockInference(funcDecl.Body);
			}
		}

		private void PostPrepareBlockInference(AstBlockExpr blockExpr)
		{
			foreach (var stmt in blockExpr.Statements)
			{
				if (stmt is AstVarDecl varDecl)
				{
					PostPrepareExprInference(varDecl.Type);
					if (varDecl.Initializer != null)
						PostPrepareExprInference(varDecl.Initializer);
				}
				else if (stmt is AstReturnStmt returnStmt)
				{
					if (returnStmt.ReturnExpression != null)
						PostPrepareExprInference(returnStmt.ReturnExpression);
				}
				else if (stmt is AstExpression expr)
				{
					PostPrepareExprInference(expr);
				}
				// todo: some check like if it is another block and etc.
			}
		}

		private void PostPrepareExprInference(AstExpression expr)
		{
			switch (expr)
			{
				case AstBinaryExpr binExpr: 
					PostPrepareBinaryExprInference(binExpr);
					break;
				case AstPointerExpr pointerExpr:
					PostPreparePointerExprInference(pointerExpr);
					break;
				case AstNewExpr newExpr:
					PostPrepareNewExprInference(newExpr);
					break;
				case AstArgumentExpr argumentExpr:
					PostPrepareArgumentExprInference(argumentExpr);
					break;
				case AstIdExpr idExpr:
					PostPrepareIdentifierInference(idExpr);
					return;
				case AstCallExpr callExpr:
					PostPrepareCallExprInference(callExpr);
					break;
				case AstCastExpr castExpr:
					PostPrepareCastExprInference(castExpr);
					break;
				// TODO: check other expressions

				default:
					{
						// TODO: anything to do here?
						break;
					}
			}
		}

		private void PostPrepareBinaryExprInference(AstBinaryExpr binExpr)
		{
			// resolve the actual operator in the current scope
			PostPrepareExprInference(binExpr.Left as AstExpression);
			PostPrepareExprInference(binExpr.Right as AstExpression);
			var operators = binExpr.Scope.GetBinaryOperators(binExpr.Operator, (binExpr.Left as AstExpression).OutType, (binExpr.Right as AstExpression).OutType);
			if (operators.Count == 0)
			{
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, binExpr, $"Indefined operator {binExpr.Operator} for types {(binExpr.Left as AstExpression).OutType} and {(binExpr.Right as AstExpression).OutType}");
			}
			else if (operators.Count > 1)
			{
				// TODO: tell em where are the operators defined
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, binExpr, $"Too many operators {binExpr.Operator} defined for types {(binExpr.Left as AstExpression).OutType} and {(binExpr.Right as AstExpression).OutType}");
			}
			else
			{
				binExpr.ActualOperator = operators[0];
				binExpr.OutType = binExpr.ActualOperator.ResultType;
			}
		}

		private void PostPreparePointerExprInference(AstPointerExpr pointerExpr)
		{
			// prepare the right side
			PostPrepareExprInference(pointerExpr.SubExpression);
			// create a new pointer type from the right side and set the type to itself
			pointerExpr.OutType = PointerType.GetPointerType(pointerExpr.SubExpression.OutType);
		}

		private void PostPrepareNewExprInference(AstNewExpr newExpr)
		{
			// prepare the right side
			PostPrepareExprInference(newExpr.TypeName);
			// the type of newExpr is the same as the type of its name expr
			newExpr.OutType = newExpr.TypeName.OutType;

			foreach (var a in newExpr.Arguments)
			{
				PostPrepareExprInference(a);
			}
		}

		private void PostPrepareArgumentExprInference(AstArgumentExpr argumentExpr)
		{
			PostPrepareExprInference(argumentExpr.Expr);

			if (argumentExpr.Name != null)
			{
				PostPrepareExprInference(argumentExpr.Name);
			}

			// the argument type is the same as its expr type
			argumentExpr.OutType = argumentExpr.Expr.OutType;
		}

		private void PostPrepareIdentifierInference(AstIdExpr idExpr)
		{
			var smbl = idExpr.Scope.GetSymbol(idExpr.Name);
			if (smbl is DeclSymbol typed)
			{
				idExpr.OutType = typed.Decl.Type.OutType;
			}
			else
			{
				// TODO: really give them a error? or mb there is smth harder?
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, idExpr, "The type could not be infered...");
			}
		}

		private void PostPrepareCallExprInference(AstCallExpr callExpr)
		{
			PostPrepareIdentifierInference(callExpr.FuncName);

			var sym = callExpr.Scope.GetSymbol(callExpr.TypeOrObjectName.Name);
			if (sym is DeclSymbol typed && typed.Decl is AstVarDecl)
			{
				// it is probably non static fun
				callExpr.StaticCall = false;
			}
			else if (sym is DeclSymbol typed2 && typed2.Decl is AstClassDecl)
			{
				// it is probably static func...
				callExpr.StaticCall = true;
			}
			else
			{
				// TODO: error here
			}

			PostPrepareIdentifierInference(callExpr.TypeOrObjectName);
			foreach (var a in callExpr.Arguments)
			{
				PostPrepareExprInference(a);
			}

			if (callExpr.FuncName.OutType is FunctionType funcType)
			{
				// call expr type is the same as func return type
				callExpr.OutType = funcType.Declaration.Returns.OutType;
			}
			else
			{
				// TODO: error here
			}
		}

		private void PostPrepareCastExprInference(AstCastExpr castExpr)
		{
			PostPrepareExprInference(castExpr.SubExpression as AstExpression);
			PostPrepareExprInference(castExpr.TypeExpr as AstExpression);
			castExpr.OutType = (castExpr.TypeExpr as AstExpression).OutType;
		}
	}
}
