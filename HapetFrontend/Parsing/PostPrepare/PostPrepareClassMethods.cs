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
			// check that all decls in the class are also static
			if (classDecl.SpecialKeys.Contains(TokenType.KwStatic))
			{
				var foundNonStatic = classDecl.Declarations.FirstOrDefault(dd => !dd.SpecialKeys.Contains(TokenType.KwStatic) && !dd.SpecialKeys.Contains(TokenType.KwConst));
				if (foundNonStatic != null)
					_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, foundNonStatic, "The declaration has to be 'static' or 'const' because it is declared in a 'static' class");
			}

			// getting all functions in the class
			var allFuncs = classDecl.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl);

			// error if user created a func with the initializer name
			var propFuncs = allFuncs.Where(x => x.Name.Name.StartsWith($"get_") || x.Name.Name.StartsWith($"set_"));
			foreach (var fnc in propFuncs)
			{
				_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, fnc.Name, $"Functions with the name that starts with 'get_' or 'set_' are not allowed");
			}

			// getting all props in the class
			var allProps = classDecl.Declarations.Where(x => x is AstPropertyDecl).Select(x => x as AstPropertyDecl);
			var allFields = classDecl.Declarations.Where(x => x is AstVarDecl varD && x is not AstPropertyDecl).Select(x => x as AstVarDecl);
			foreach (var pp in allProps)
			{
				// check if there is already a field named like 'field_Prop'
				// error in this situation because we probably going to generate the field
				// also check if the prop is really going to gen field
				var theField = allFields.FirstOrDefault(x => x.Name.Name == $"field_{pp.Name.Name}");
				if (theField != null)
				{
					// also check if the prop is really going to gen field
					if (pp.GetBlock == null && pp.SetBlock == null)
						_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, theField, $"Please rename the field because a property named {pp.Name} is going to generate the field");
				}
			}

			// getting all fields and props and error if there are equal names
			var allFieldsAndProps = classDecl.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl).ToList();
			for (int i = 0; i < allFieldsAndProps.Count; ++i)
			{
				for (int j = i; j < allFieldsAndProps.Count; ++j)
				{
					if (j == i)
						continue;
					if (allFieldsAndProps[i].Name.Name == allFieldsAndProps[j].Name.Name)
					{
						// TODO: show previous field decl
						_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, allFieldsAndProps[j], $"Fields and properties cannot have the same names in a class");
					}
				}
			}

			// generate prop's fields and funcs
			/// removing props is done in <see cref="RemoveAllProperties"/>
			PostPrepareClassProperties(classDecl);
			// get funcs again after this :) sorry
			allFuncs = classDecl.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl);

			// error if user created a func with the initializer name
			var specialFuncs = allFuncs.Where(x => (x.Name.Name.EndsWith($"::{classDecl.Name.Name}_ini") || 
												    x.Name.Name.EndsWith($"::{classDecl.Name.Name}_ctor") ||
												    x.Name.Name.EndsWith($"::{classDecl.Name.Name}_stor") || // static ctor
												    x.Name.Name.EndsWith($"::{classDecl.Name.Name}_dtor")));
			foreach (var fnc in specialFuncs)
			{
				_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, fnc.Name, $"Function with the name is not allowed in the {classDecl.Name.Name} class");
			}

			// static ctor is always generated
			PostPrepareGenerateClassStaticConstructor(classDecl, allFuncs.Where(x => x.ClassFunctionType == Enums.ClassFunctionType.StaticCtor).ToList());
			// generating all the shite only if the class is not static
			if (!classDecl.SpecialKeys.Contains(TokenType.KwStatic))
			{
				PostPrepareGenerateClassInitializer(classDecl);
				// passing all the existing ctors
				PostPrepareGenerateClassConstructor(classDecl, allFuncs.Where(x => x.ClassFunctionType == Enums.ClassFunctionType.Ctor).ToList());
				PostPrepareGenerateClassDestructor(classDecl, allFuncs.Where(x => x.ClassFunctionType == Enums.ClassFunctionType.Dtor).ToList());
			}

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
			// the block with all field inits
			var iniBlock = GetFieldsToInitialize(classDecl, false);

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

				// TODO: do i need to insert smth here? probably need to extern 'free' and call it at the end
				//ct.Body.Statements.Insert(0, new AstCallExpr(
				//	new AstNestedExpr(new AstIdExpr("this"), null),
				//	new AstIdExpr($"{classDecl.Name.Name}_ini")));
			}
			else
			{
				_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, dtors[1], "Only one destructor could be declared in a class");
			}
		}

		private List<AstClassDecl> _allUsedClassesInProgram = new List<AstClassDecl>();
		private void PostPrepareGenerateClassStaticConstructor(AstClassDecl classDecl, List<AstFuncDecl> ctors)
		{
			// we need to add a static var to check that the stor was called
			string theVarName = $"__is_{_currentSourceFile.Namespace}.{classDecl.Name.Name}_stor_called";
			var theVar = new AstVarDecl(new AstNestedExpr(new AstIdExpr("bool"), null), new AstIdExpr(theVarName));
			theVar.SpecialKeys.Add(TokenType.KwStatic);
			theVar.SpecialKeys.Add(TokenType.KwUnreflected);
			classDecl.Declarations.Add(theVar);

			// creating the ini block for fields
			var iniBlock = GetFieldsToInitialize(classDecl, true);
			// set 'true' to the var
			var varAssign = new AstAssignStmt(new AstNestedExpr(new AstIdExpr(theVarName), null), new AstBoolExpr(true));
			iniBlock.Statements.Add(varAssign);
			AstIfStmt checkForInited = new AstIfStmt(new AstUnaryExpr("!", new AstIdExpr(theVarName)), iniBlock, null);

			if (ctors.Count == 0)
			{
				// there is no dtor. need to create one
				List<AstStatement> storBlockStatements = new List<AstStatement>();
				storBlockStatements.Add(checkForInited);

				// the block with 
				var storBlock = new AstBlockExpr(storBlockStatements);

				// the ctor func
				var storDecl = new AstFuncDecl(new List<AstParamDecl>(),
				new AstIdExpr("void"),
				storBlock,
				new AstIdExpr($"{classDecl.Name.Name}_stor"));
				storDecl.SpecialKeys.Add(TokenType.KwPublic); // stor is public
				storDecl.SpecialKeys.Add(TokenType.KwStatic); // stor is static
				storDecl.ClassFunctionType = Enums.ClassFunctionType.StaticCtor;
				storDecl.ContainingClass = classDecl;
				classDecl.Declarations.Add(storDecl);
			}
			else if (ctors.Count == 1)
			{
				var ctorFunc = ctors[0];
				ctorFunc.Name = ctorFunc.Name.GetCopy($"{ctorFunc.Name.Name}_stor");

				// stor can only have 'static' kw
				if (ctorFunc.SpecialKeys.Count > 1)
					_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, ctorFunc.Name, $"Static constructor can only have 'static' keyword. Other keywords will be ignored!", null, Entities.ReportType.Warning);

				// move all user code under 'if' stmt
				checkForInited.BodyTrue.Statements.AddRange(ctorFunc.Body.Statements);
				ctorFunc.Body.Statements.Clear();

				// add check into user defined stor
				ctorFunc.Body.Statements.Add(checkForInited);
			}
			else
			{
				_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, ctors[1], "Only one static constructor could be declared in a class");
			}
		}

		/// <summary>
		/// Function to unwrap all the props
		/// </summary>
		/// <param name="classDecl">The class with props</param>
		private void PostPrepareClassProperties(AstClassDecl classDecl)
		{
			List<AstDeclaration> declarationsToAdd = new List<AstDeclaration>();
			foreach (var prop in classDecl.Declarations.Where(x => x is AstPropertyDecl).Select(x => x as AstPropertyDecl))
			{
				if (prop.GetBlock == null && prop.SetBlock == null)
				{
					// need to create a field :(
					AstVarDecl propField = prop.GetField();
					declarationsToAdd.Add(propField);
				}
				if (prop.HasGet)
				{
					// need to create a 'get' method
					AstFuncDecl getFunc = prop.GetGetFunction();
					declarationsToAdd.Add(getFunc);
				}
				if (prop.HasSet)
				{
					// need to create a 'set' method
					AstFuncDecl setFunc = prop.GetSetFunction();
					declarationsToAdd.Add(setFunc);
				}
			}
			classDecl.Declarations.AddRange(declarationsToAdd);
		}

		private AstBlockExpr GetFieldsToInitialize(AstClassDecl classDecl, bool forStatic)
		{
			// gettings all field decls and init them
			var allVarDecls = classDecl.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl);
			// we need to get all props from class. why?
			// read comment below where it used
			var allProps = classDecl.Declarations.Where(x => x is AstPropertyDecl).Select(x => x as AstPropertyDecl);
			List<AstStatement> iniBlockStatements = new List<AstStatement>();
			foreach (AstVarDecl decl in allVarDecls)
			{
				// if the field is for property generated
				// we don't need to initialize property directly
				// we would initialize it using property 'set' method
				// that is also prepared below :)
				bool foundPropa = false;
				foreach (var pp in allProps)
				{
					// if the field has the proper name and (!) the property is really compiler generated
					if (decl.Name.Name == $"field_{pp.Name.Name}" && pp.GetBlock == null && pp.SetBlock == null)
					{
						foundPropa = true;
						break;
					}
				}
				if (foundPropa)
					continue;

				// for static we need to get only static fields/props
				if (forStatic)
				{
					// need to do this for statics
					if (!decl.SpecialKeys.Contains(TokenType.KwStatic))
						continue;
				}
				else
				{
					// no need to do this for consts and statics
					if (decl.SpecialKeys.Contains(TokenType.KwStatic) || decl.SpecialKeys.Contains(TokenType.KwConst))
						continue;
				}

				// creating field assing statement
				var objectName = forStatic ? null : new AstNestedExpr(new AstIdExpr("this"), null);
				var target = new AstNestedExpr(decl.Name.GetCopy(), objectName, decl);
				AstExpression fieldInitializer;
				if (decl.Initializer != null)
					fieldInitializer = decl.Initializer;
				else
					fieldInitializer = new AstDefaultExpr(decl);

				/// this is a kostyl that is described here <see cref="Parser.ParseClassDeclaration"/>
				if (fieldInitializer is AstBlockExpr blckExpr)
				{
					// skip last because the last one is the real value to be applied into variable 
					iniBlockStatements.AddRange(blckExpr.Statements.SkipLast(1));
					fieldInitializer = blckExpr.Statements.Last() as AstExpression; // TODO: checks here?
				}

				// TODO: !!! check that non-static functions are not used in field initializers!!!
				// creating the assign
				var assign = new AstAssignStmt(target, fieldInitializer, decl);
				iniBlockStatements.Add(assign);

				// we don't need the initializer anymore
				decl.Initializer = null;
			}
			// the block with all field inits
			return new AstBlockExpr(iniBlockStatements);
		}
	}
}
