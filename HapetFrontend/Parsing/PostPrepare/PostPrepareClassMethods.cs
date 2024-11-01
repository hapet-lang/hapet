using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Types;

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
			var allFuncs = classDecl.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl);

			// error if user created a func with the initializer name
			var propFuncs = allFuncs.Where(x => x.Name.Name.StartsWith($"get_") || x.Name.Name.StartsWith($"set_"));
			foreach (var fnc in propFuncs)
			{
				_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, fnc.Name, $"Functions with the name that starts with 'get_' or 'set_' are not allowed");
			}

			// error if user created a func with the initializer name
			var specialFuncs = allFuncs.Where(x => (x.Name.Name.EndsWith($"::{classDecl.Name.Name}_ini") || 
												    x.Name.Name.EndsWith($"::{classDecl.Name.Name}_ctor") ||
												    x.Name.Name.EndsWith($"::{classDecl.Name.Name}_dtor")));
			foreach (var fnc in specialFuncs)
			{
				_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, fnc.Name, $"Function with the name is not allowed in the {classDecl.Name.Name} class");
			}

			PostPrepareGenerateClassInitializer(classDecl);
			// passing all the existing ctors
			PostPrepareGenerateClassConstructor(classDecl, allFuncs.Where(x => x.ClassFunctionType == Enums.ClassFunctionType.Ctor).ToList());
			PostPrepareGenerateClassDestructor(classDecl, allFuncs.Where(x => x.ClassFunctionType == Enums.ClassFunctionType.Dtor).ToList());

			// adding 'this' param as first
			foreach (var decl in classDecl.Declarations)
			{
				if (decl is not AstFuncDecl funcDecl)
					continue;

				// adding 'this' param to func params
				if (!funcDecl.SpecialKeys.Contains(TokenType.KwStatic))
				{
					// creating the class instance 'this' param
					AstExpression paramType = new AstPointerExpr(classDecl.Name.GetCopy(), false);
					AstIdExpr paramName = new AstIdExpr("this");
					AstParamDecl thisParam = new AstParamDecl(paramType, paramName);
					// adding the param as the func first param
					funcDecl.Parameters.Insert(0, thisParam);
				}

				// checking for 'return' existance at the end. if not - add
				if (funcDecl.Body != null && funcDecl.Body.Statements.LastOrDefault() is not AstReturnStmt)
				{
					funcDecl.Body.Statements.Add(new AstReturnStmt(null));
                }
			}
		}

		private void PostPrepareGenerateClassInitializer(AstClassDecl classDecl)
		{
			// gettings all field decls and init them
			var allVarDecls = classDecl.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl);
			List<AstStatement> iniBlockStatements = new List<AstStatement>();
			foreach (AstVarDecl decl in allVarDecls)
			{
				// creating field assing statement
				var target = new AstNestedExpr(decl.Name.GetCopy(), new AstNestedExpr(new AstIdExpr("this"), null), decl);
				AstExpression fieldInitializer;
				if (decl.Initializer != null)
					fieldInitializer = decl.Initializer;
				else
					fieldInitializer = new AstDefaultExpr(decl);
				var assign = new AstAssignStmt(target, fieldInitializer, decl);
				iniBlockStatements.Add(assign);

				// we don't need it anymore
				decl.Initializer = null;
            }
			// the block with all field inits
			var iniBlock = new AstBlockExpr(iniBlockStatements);

			// the ini func
			var iniDecl = new AstFuncDecl(new List<AstParamDecl>(),
			new AstIdExpr("void"),
			iniBlock,
			new AstIdExpr($"{classDecl.Name.Name}_ini"));
			iniDecl.SpecialKeys.Add(TokenType.KwUnreflected); // ini is private because it is called inside ctors
			iniDecl.ClassFunctionType = Enums.ClassFunctionType.Initializer;
			iniDecl.ContainingClass = classDecl;
			classDecl.Declarations.Insert(0, iniDecl);
		}

		private void PostPrepareGenerateClassConstructor(AstClassDecl classDecl, List<AstFuncDecl> ctors)
		{
			if (ctors.Count == 0)
			{
				// there is no ctor. need to create one
				List<AstStatement> ctorBlockStatements = new List<AstStatement>();
				// creating ini func call
				ctorBlockStatements.Add(new AstCallExpr(
					new AstNestedExpr(new AstIdExpr("this"), null),
					new AstIdExpr($"{classDecl.Name.Name}_ini")));
				// the block with call of ini func
				var ctorBlock = new AstBlockExpr(ctorBlockStatements);

				// the ctor func
				var ctorDecl = new AstFuncDecl(new List<AstParamDecl>(),
				new AstIdExpr("void"),
				ctorBlock,
				new AstIdExpr($"{classDecl.Name.Name}_ctor"));
				ctorDecl.SpecialKeys.Add(TokenType.KwPublic); // default ctor is public
				ctorDecl.ClassFunctionType = Enums.ClassFunctionType.Ctor;
				ctorDecl.ContainingClass = classDecl;
				classDecl.Declarations.Insert(1, ctorDecl); // the first one has to be ini func
			}
			else
			{
				foreach (var ct in ctors)
				{
					ct.Name = ct.Name.GetCopy($"{ct.Name.Name}_ctor");
					// insert ini func call at the beginning of the func body
					ct.Body.Statements.Insert(0, new AstCallExpr(
						new AstNestedExpr(new AstIdExpr("this"), null),
						new AstIdExpr($"{classDecl.Name.Name}_ini")));
				}
			}
		}

		private void PostPrepareGenerateClassDestructor(AstClassDecl classDecl, List<AstFuncDecl> dtors)
		{
			if (dtors.Count == 0)
			{
				// there is no dtor. need to create one
				List<AstStatement> dtorBlockStatements = new List<AstStatement>();

				// TODO: do i need to place here something?

				// the block with 
				var dtorBlock = new AstBlockExpr(dtorBlockStatements);

				// the ctor func
				var dtorDecl = new AstFuncDecl(new List<AstParamDecl>(),
				new AstIdExpr("void"),
				dtorBlock,
				new AstIdExpr($"{classDecl.Name.Name}_dtor"));
				dtorDecl.SpecialKeys.Add(TokenType.KwPublic); // default dtor is public
				dtorDecl.ClassFunctionType = Enums.ClassFunctionType.Dtor;
				dtorDecl.ContainingClass = classDecl;
				classDecl.Declarations.Add(dtorDecl);
			}
			else if (dtors.Count == 1)
			{
				var dtorFunc = dtors[0];
				dtorFunc.Name = dtorFunc.Name.GetCopy($"{dtorFunc.Name.Name}_dtor");

				// TODO: do i need to insert smth here? probably need to extern 'delete' and call it at the end
				//ct.Body.Statements.Insert(0, new AstCallExpr(
				//	new AstNestedExpr(new AstIdExpr("this"), null),
				//	new AstIdExpr($"{classDecl.Name.Name}_ini")));
			}
			else
			{
				_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, dtors[1], "Only one destructor could be declared in a class");
			}
		}
	}
}
