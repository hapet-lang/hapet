using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;

namespace HapetFrontend.Parsing.PostPrepare
{
	public partial class PostPrepare
	{
		private void PostPrepareClassMethods()
		{
			foreach (var (path, file) in _compiler.GetFiles())
			{
				_currentSourceFile = file;
				foreach (var stmt in file.Statements)
				{
					if (stmt is AstClassDecl classDecl)
					{
						PostPrepareClassMethodsInternal(classDecl);
					}
				}
			}
		}

		private void PostPrepareClassMethodsInternal(AstClassDecl classDecl)
		{
			foreach (var decl in classDecl.Declarations)
			{
				if (decl is AstFuncDecl funcDecl && 
					!funcDecl.SpecialKeys.Contains(TokenType.KwStatic))
				{
					// creating the class instance 'this' param
					AstExpression paramType = new AstPointerExpr(new AstIdExpr(classDecl.Name.Name), null, false);
					AstIdExpr paramName = new AstIdExpr("this");
					AstParamDecl thisParam = new AstParamDecl(paramType, paramName);
					// adding the param as the func first param
					funcDecl.Parameters.Insert(0, thisParam);
				}
			}
		}
	}
}
