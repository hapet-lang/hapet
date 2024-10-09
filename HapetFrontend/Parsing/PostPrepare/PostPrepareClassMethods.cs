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
				PostPrepareGenerateExternalFuncs();

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
			var allFuncs = classDecl.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl);
			// TODO: error if user created a func with the initializer name
			// PostPrepareGenerateClassInitializer(classDecl);

			// adding 'this' param as first
			foreach (var decl in classDecl.Declarations)
			{
				if (decl is AstFuncDecl funcDecl && 
					!funcDecl.SpecialKeys.Contains(TokenType.KwStatic))
				{
					// creating the class instance 'this' param
					AstExpression paramType = new AstPointerExpr(new AstIdExpr(classDecl.Name.Name), false);
					AstIdExpr paramName = new AstIdExpr("this");
					AstParamDecl thisParam = new AstParamDecl(paramType, paramName);
					// adding the param as the func first param
					funcDecl.Parameters.Insert(0, thisParam);
				}
			}
		}

		private void PostPrepareGenerateExternalFuncs()
		{
			var mallocDecl = new AstFuncDecl(new List<AstParamDecl>()
			{
				new AstParamDecl(new AstIdExpr("int"), null),
			},
			new AstPointerExpr(new AstIdExpr("void")),
			null,
			new AstIdExpr("malloc"));
			mallocDecl.SpecialKeys.Add(TokenType.KwExtern);
			_currentSourceFile.Statements.Insert(0, mallocDecl);
			mallocDecl.Scope = _currentSourceFile.FileScope;
		}

		private void PostPrepareGenerateClassInitializer(AstClassDecl classDecl)
		{
			var iniDecl = new AstFuncDecl(new List<AstParamDecl>(),
			new AstPointerExpr(new AstIdExpr("void")),
			null,
			new AstIdExpr($"{classDecl.Name.Name}_ini"));
			iniDecl.SpecialKeys.Add(TokenType.KwPrivate); // ini is private because it is called inside ctors
			iniDecl.ClassFunctionTypes.Add(Enums.ClassFunctionType.Initializer);
			classDecl.Declarations.Insert(0, iniDecl);
		}
	}
}
