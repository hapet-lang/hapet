using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Scoping;

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
				PostPrepareTypeExprInference(p.Type, funcDecl.Scope);
			}

			// inferencing return type 
			{
				PostPrepareTypeExprInference(funcDecl.Returns, funcDecl.Scope);
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
					PostPrepareTypeExprInference(varDecl.Type, blockExpr.Scope);
				}
				// todo: some check like if it is another block and etc.
			}
		}

		private void PostPrepareTypeExprInference(AstExpression expr, Scope scopeToSearch = null)
		{
			if (expr is AstTupleExpr tpl)
			{
				// TODO: resolve tuple shite
			}
			else if (expr is AstIdExpr idExpr)
			{
				PostPrepareIdentifierInference(idExpr, scopeToSearch);
			}
			// TODO: check for 'var'
		}

		private void PostPrepareIdentifierInference(AstIdExpr idExpr, Scope scopeToSearch = null)
		{
			if (scopeToSearch == null)
			{
				if (idExpr.Parent is not AstStatement)
					_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, idExpr, "Parent ast node of current has to be a statement");
				scopeToSearch = (idExpr.Parent as AstStatement).Scope;
			}
			
			var smbl = scopeToSearch.GetSymbol(idExpr.Name);
			if (smbl is ITypedSymbol typed)
			{
				idExpr.OutType = typed.Type;
			}
			else
			{
				// TODO: really give them a error? or mb there is smth harder?
				_compiler.ErrorHandler.ReportError(_currentSourceFile.Text, idExpr, "The type could not be infered...");
			}
		}

		// TODO: recursively go through all of the statments and set Scope and Parent
	}
}
