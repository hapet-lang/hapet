using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using System;

namespace HapetFrontend.Parsing.PostPrepare
{
	public partial class PostPrepare
	{
		private void PostPrepareTypeInference()
		{
			PostPrepareInternalShiteInference();
			foreach (var (path, file) in _compiler.GetFiles())
			{
				_currentSourceFile = file;
				foreach (var stmt in file.Statements)
				{
					if (stmt is AstClassDecl classDecl)
					{
						PostPrepareClassInference(classDecl);
					}
					else if (stmt is AstStructDecl structDecl)
					{
						PostPrepareStructInference(structDecl);
					}
				}
			}
		}

		private void PostPrepareInternalShiteInference()
		{
			PostPrepareStructInference(AstStringExpr.StringStruct);
			PostPrepareStructInference(AstArrayExpr.ArrayStruct);
		}

		private void PostPrepareClassInference(AstClassDecl classDecl)
		{
			_currentClass = classDecl;

			/// fields should be already inferred in <see cref="PostPrepareMetadataTypes"/> and <see cref="PostPrepareMetadataTypeFields"/>
			foreach (var decl in classDecl.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl))
			{
				PostPrepareFunctionInference(decl);
			}

			/// some shite is already inferrenced in <see cref="PostPrepareMetadataTypeFields"/>
			foreach (var decl in classDecl.Declarations.Where(x => x is AstPropertyDecl).Select(x => x as AstPropertyDecl))
			{
				if (decl.GetBlock != null)
				{
					PostPrepareExprInference(decl.GetBlock);
				}
				if (decl.SetBlock != null)
				{
					PostPrepareExprInference(decl.SetBlock);
				}
			}
		}

		private void PostPrepareStructInference(AstStructDecl structDecl)
		{
			/// should be already inferred in <see cref="PostPrepareMetadataTypes"/> and <see cref="PostPrepareMetadataTypeFields"/>
		}

		private void PostPrepareFunctionInference(AstFuncDecl funcDecl, bool forMetadata = false)
		{
			_currentFunction = funcDecl;

			// if the function inference is for metadata - infer everything except body
			// if not - infer only body because func decl already infered from metadata :)
			if (forMetadata)
			{
				// inferencing attrs
				foreach (var a in funcDecl.Attributes)
				{
					PostPrepareExprInference(a);

					// TODO: many checks here (like fields and so on):
				}

				// inferencing parameters 
				foreach (var p in funcDecl.Parameters)
				{
					PostPrepareParamInference(p);
				}

				// if the containing class is empty - it is external func
				if (funcDecl.ContainingClass != null)
				{
					// renaming func name from 'Anime' to 'Anime(int, float)'
					string newName = $"{funcDecl.ContainingClass.Name.Name}::{funcDecl.Name.Name}{funcDecl.Parameters.GetParamsString()}";
					// if it is public func - it should be visible in the scope in which func's class is
					funcDecl.ContainingClass.SubScope.DefineDeclSymbol(newName, funcDecl);
					funcDecl.Name = funcDecl.Name.GetCopy(newName);
				}

				// inferencing return type 
				{
					PostPrepareExprInference(funcDecl.Returns);
				}
			}
			else
			{
				// inferring only body
				if (funcDecl.Body != null)
				{
					PostPrepareBlockInference(funcDecl.Body);
				}
			}
		}

		private void PostPrepareVarInference(AstVarDecl varDecl)
		{
			PostPrepareExprInference(varDecl.Type);

			if (varDecl.Type.OutType is ClassType)
			{
				// the var is actually a pointer to the class
				var astPtr = new AstPointerExpr(varDecl.Type, false, varDecl.Type.Location);
				astPtr.Scope = varDecl.Type.Scope;
				varDecl.Type = astPtr;
				PostPrepareExprInference(varDecl.Type);
			}

			if (varDecl.Initializer != null)
			{
				if (varDecl.Initializer is AstDefaultExpr)
				{
					// get the default value for the type (no need to infer)
					varDecl.Initializer = AstDefaultExpr.GetDefaultValueForType(varDecl.Type.OutType, varDecl.Initializer);
					if (varDecl.Initializer == null)
						_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, varDecl, "Default value for the type was not found");
				}
				else
				{
					// if it is not a default
					PostPrepareExprInference(varDecl.Initializer);
				}
				PostPrepareVariableAssign(varDecl);
			}
		}

		private void PostPrepareParamInference(AstParamDecl paramDecl)
		{
			PostPrepareExprInference(paramDecl.Type);
			if (paramDecl.DefaultValue != null)
				PostPrepareExprInference(paramDecl.DefaultValue);
		}

		private void PostPrepareExprInference(AstStatement expr)
		{
			switch (expr)
			{
				// special case at least for 'for' loop
				// when 'for (int i = 0;...)' where 'int i' 
				// would not be handled by blockExpr
				case AstVarDecl varDecl:
					PostPrepareVarInference(varDecl);
					break;

				case AstBlockExpr blockExpr:
					PostPrepareBlockInference(blockExpr);
					break;
				case AstUnaryExpr unExpr:
					PostPrepareUnaryExprInference(unExpr);
					break;
				case AstBinaryExpr binExpr:
					PostPrepareBinaryExprInference(binExpr);
					break;
				case AstPointerExpr pointerExpr:
					PostPreparePointerExprInference(pointerExpr);
					break;
				case AstAddressOfExpr addrExpr:
					PostPrepareAddressOfExprInference(addrExpr);
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
				case AstNestedExpr nestExpr:
					PostPrepareNestedExprInference(nestExpr, out bool _);
					break;
				case AstDefaultExpr defaultExpr:
					_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, defaultExpr, "(Compiler exception) The default had to be infered previously by caller");
					break;
				case AstArrayExpr arrayExpr:
					PostPrepareArrayExprInference(arrayExpr);
					break;
				case AstArrayCreateExpr arrayCreateExpr:
					PostPrepareArrayCreateExprInference(arrayCreateExpr);
					break;
				case AstArrayAccessExpr arrayAccExpr:
					PostPrepareArrayAccessExprInference(arrayAccExpr);
					break;

				// statements
				case AstAssignStmt assignStmt:
					_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, assignStmt, "(Compiler exception) The statement has to be handled by block expr");
					// PostPrepareAssignStmtInference(assignStmt, out bool _);
					break;
				case AstForStmt forStmt:
					PostPrepareForStmtInference(forStmt);
					break;
				case AstWhileStmt whileStmt:
					PostPrepareWhileStmtInference(whileStmt);
					break;
				case AstIfStmt ifStmt:
					PostPrepareIfStmtInference(ifStmt);
					break;
				case AstSwitchStmt switchStmt:
					PostPrepareSwitchStmtInference(switchStmt);
					break;
				case AstCaseStmt caseStmt:
					PostPrepareCaseStmtInference(caseStmt);
					break;
				case AstBreakContStmt _:
					break;
				case AstReturnStmt returnStmt:
					PostPrepareReturnStmtInference(returnStmt);
					break;
				case AstAttributeStmt attrStmt:
					PostPrepareAttributeStmtInference(attrStmt);
					break;
				// TODO: check other expressions

				default:
					{
						// TODO: anything to do here?
						break;
					}
			}
		}

		private void PostPrepareBlockInference(AstBlockExpr blockExpr)
		{
			// list of all replacements that should be done
			// so all Propa assigns would be replaced with func calls
			Dictionary<AstAssignStmt, AstCallExpr> repls = new Dictionary<AstAssignStmt, AstCallExpr>();
			// go all over the statements
			foreach (var stmt in blockExpr.Statements)
			{
				if (stmt == null)
					continue;

				// we need to handle the statements to replaces props with calls
				if (stmt is AstAssignStmt asgn)
				{
					PostPrepareAssignStmtInference(asgn, out bool itWasPropa);
					if (itWasPropa)
					{
						AstIdExpr propaName = (asgn.Target.RightPart as AstIdExpr);
						// WARN: we need these two same errors!!!
						if (asgn.Target.LeftPart.OutType is not PointerType ptrT)
						{
							_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, asgn.Target.LeftPart, $"Type of the expr expected to be a pointer to a class");
							return;
						}
						if (ptrT.TargetType is not ClassType clsT)
						{
							_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, asgn.Target.LeftPart, $"Type of the expr expected to be a pointer to a class");
							return;
						}
						// creating a call with name 'RootNamespace.TheClass::set_Prop(RootNamespace.TheClass*:PropType)'
						var fncCall = new AstCallExpr(asgn.Target.LeftPart, propaName.GetCopy($"{clsT}::set_{propaName.Name}({asgn.Target.LeftPart.OutType}:{propaName.OutType})"), new List<AstArgumentExpr>() { new AstArgumentExpr(asgn.Value) }, asgn);
						SetScopeAndParent(fncCall, asgn.Target.NormalParent, asgn.Target.Scope);
						PostPrepareCallExprInference(fncCall);
						repls.Add(asgn, fncCall);
					}
				}
				else
				{
					PostPrepareExprInference(stmt);
				}
			}

			// begin all replacements
			foreach (var pair in repls)
			{
				// replace the assign statement
				int assignIndex = blockExpr.Statements.IndexOf(pair.Key);
				blockExpr.Statements.Remove(pair.Key);
				blockExpr.Statements.Insert(assignIndex, pair.Value);
			}
		}

		private void PostPrepareUnaryExprInference(AstUnaryExpr unExpr)
		{
			// TODO: check for the right size for an existance value (compiletime evaluated) and do some shite (set unExpr OutValue)
			PostPrepareExprInference(unExpr.SubExpr as AstExpression);
			var operators = unExpr.Scope.GetUnaryOperators(unExpr.Operator, (unExpr.SubExpr as AstExpression).OutType);
			if (operators.Count == 0)
			{
				_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, unExpr, $"Undefined operator {unExpr.Operator} for type {(unExpr.SubExpr as AstExpression).OutType}");
			}
			else if (operators.Count > 1)
			{
				// TODO: tell em where are the operators defined
				_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, unExpr, $"Too many operators {unExpr.Operator} defined for type {(unExpr.SubExpr as AstExpression).OutType}");
			}
			else
			{
				unExpr.ActualOperator = operators[0];
				unExpr.OutType = unExpr.ActualOperator.ResultType;
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
				_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, binExpr, $"Undefined operator {binExpr.Operator} for types {(binExpr.Left as AstExpression).OutType} and {(binExpr.Right as AstExpression).OutType}");
			}
			else if (operators.Count > 1)
			{
				// TODO: tell em where are the operators defined
				_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, binExpr, $"Too many operators {binExpr.Operator} defined for types {(binExpr.Left as AstExpression).OutType} and {(binExpr.Right as AstExpression).OutType}");
			}
			else
			{
				binExpr.ActualOperator = operators[0];
				binExpr.OutType = binExpr.ActualOperator.ResultType;

				// making some type casts
				var leftExpr = (binExpr.Left as AstExpression);
				var rightExpr = (binExpr.Right as AstExpression);

				// creating cast to result type if it is not a bool expr
				if (leftExpr.OutType != binExpr.OutType && binExpr.OutType is not BoolType)
				{
					// cast if they are not the same haha
					binExpr.Left = PostPrepareExpressionWithType(binExpr.OutType, leftExpr);
				}
				// creating cast to result type if it is not a bool expr
				if (rightExpr.OutType != binExpr.OutType && binExpr.OutType is not BoolType)
				{
					// cast if they are not the same haha
					binExpr.Right = PostPrepareExpressionWithType(binExpr.OutType, rightExpr);
				}

				// creating cast to result type if it is a bool expr and left and right are not the same types
				if (rightExpr.OutType != leftExpr.OutType && binExpr.OutType is BoolType)
				{
					// cast if they are not the same haha
					HapetType castingType = HapetType.GetPreferredTypeOf(leftExpr.OutType, rightExpr.OutType, out bool tookLeft);
					// if the left type was taken then change the right expr
					if (tookLeft)
						binExpr.Right = PostPrepareExpressionWithType(castingType, rightExpr);
					else
						binExpr.Left = PostPrepareExpressionWithType(castingType, leftExpr);
				}
			}
		}

		private void PostPreparePointerExprInference(AstPointerExpr pointerExpr)
		{
			// prepare the right side
			PostPrepareExprInference(pointerExpr.SubExpression);
			// create a new pointer type from the right side and set the type to itself
			pointerExpr.OutType = PointerType.GetPointerType(pointerExpr.SubExpression.OutType);
		}

		private void PostPrepareAddressOfExprInference(AstAddressOfExpr addrExpr)
		{
			// prepare the right side
			PostPrepareExprInference(addrExpr.SubExpression);
			// create a new reference type from the right side and set the type to itself
			addrExpr.OutType = ReferenceType.GetRefType(addrExpr.SubExpression.OutType);
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
			string name = idExpr.Name;

			var smbl = idExpr.Scope.GetSymbol(name);
			if (smbl is DeclSymbol typed)
			{
				idExpr.OutType = typed.Decl.Type.OutType;
				return;
			}

			// searching for the name with current class name
			// works only for functions
			string nameWithClass = $"{_currentClass.Name.Name}::{name}";
			var smblInLocalClass = idExpr.Scope.GetSymbol(nameWithClass);
			if (smblInLocalClass is DeclSymbol typed2)
			{
				idExpr.Name = nameWithClass;
				idExpr.OutType = typed2.Decl.Type.OutType;
				return;
			}

			// it is a func
			if (name.Contains("::"))
			{
				// for example 'System.Attribute::Attrbute_ctor(...)'
				string[] nameAndFunc = name.Split("::");
				if (nameAndFunc.Length != 2)
				{
					// TODO: error 
					return;
				}

				// recursively infer left part of func call
				AstIdExpr leftPartId = new AstIdExpr(nameAndFunc[0]);
				leftPartId.Scope = idExpr.Scope;
				PostPrepareIdentifierInference(leftPartId);
				// it has to be a class (or mb struct)
				if (leftPartId.OutType is not ClassType clsTp)
				{
					// TODO: error 
					return;
				}

				var fullFuncName = $"{idExpr.Name}::{nameAndFunc[1]}";
				var funcInAnotherClass = clsTp.Declaration.SubScope.GetSymbol(fullFuncName);
				if (funcInAnotherClass is DeclSymbol typed4)
				{
					idExpr.Name = fullFuncName;
					idExpr.OutType = typed4.Decl.Type.OutType;
					return;
				}
			}

			// searching for the name with namespace
			// works only for types/objects
			string nameWithNamespace = $"{_currentSourceFile.Namespace}.{name}";
			var smblInLocalFile = idExpr.Scope.GetSymbol(nameWithNamespace);
			if (smblInLocalFile is DeclSymbol typed3)
			{
				idExpr.Name = nameWithNamespace;
				idExpr.OutType = typed3.Decl.Type.OutType;
				return;
			}

			// check if it is smth like 'System.Attribute' where 'System' is ns and 'Attribute' is a class
			if (name.Split('.').Length > 1)
			{
				string[] splitted = name.Split('.');
				var leftPart = string.Join('.', splitted.SkipLast(1));
				var rightPart = splitted.Last();

				// getting a symbol from namespace
				var includedSmbl = idExpr.Scope.GetSymbolInNamespace(leftPart, rightPart);
				if (includedSmbl is DeclSymbol typed4)
				{
					// do not change name because it already contains namespace
					idExpr.OutType = typed4.Decl.Type.OutType;
					return;
				}
			}

			// go all over the usings
			foreach (var usng in _currentSourceFile.Usings)
			{
				// getting ns string
				var ns = usng.FlattenNamespace;

				// check if it is smth like 'Runtime.InteropServices.DllImportAttribute'
				// where 'Runtime.InteropServices' is PART! of ns and 'DllImportAttribute' is a class
				if (name.Split('.').Length > 1)
				{
					string[] splitted = name.Split('.');
					var leftPart = string.Join('.', splitted.SkipLast(1));
					var rightPart = splitted.Last();

					// getting a symbol from namespace
					var includedSmbl = idExpr.Scope.GetSymbolInNamespace($"{ns}.{leftPart}", rightPart);
					if (includedSmbl is DeclSymbol typed4)
					{
						// do not change name because it already contains namespace
						idExpr.OutType = typed4.Decl.Type.OutType;
						return;
					}
				}

				// try just get the name from using namespace
				string fullNameWithNs = $"{ns}.{name}";
				var usedSmbl = idExpr.Scope.GetSymbolInNamespace(ns, name);
				if (usedSmbl is DeclSymbol typed5)
				{
					idExpr.Name = fullNameWithNs;
					idExpr.OutType = typed5.Decl.Type.OutType;
					return;
				}
			}

			

			// TODO: check in 'usings' via similar way as upper

			// TODO: really give them a error? or mb there is smth harder?
			_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, "The type could not be inferred...");
		}

		private void PostPrepareCallExprInference(AstCallExpr callExpr)
		{
			// usually when in the same class
			if (callExpr.TypeOrObjectName != null)
			{
				// resolve the object on which func is called
				PostPrepareExprInference(callExpr.TypeOrObjectName);
			}

			// resolve args
			foreach (var a in callExpr.Arguments)
			{
				PostPrepareExprInference(a);
			}

			// we need to manually check if the function is an external. 
			// if it is not - try to search it like an internal
			var smbl = callExpr.FuncName.Scope.GetSymbol(callExpr.FuncName.Name);
			if (smbl is DeclSymbol declTyped)
			{
				callExpr.FuncName.OutType = declTyped.Decl.Type.OutType;
			}
			else
			{
				string newName = string.Empty;
				// renaming func call name from 'Anime' to 'Anime(int, float)' WITH OBJECT AS FIRST PARAM
				if (callExpr.TypeOrObjectName == null)
				{
					// if the type/object name is not presented - the function is in the same class
					// but we need to know is it static or not
					newName = $"{_currentClass.Name.Name}::{callExpr.FuncName.Name}{callExpr.Arguments.GetArgsString()}";
					var smbl2 = callExpr.FuncName.Scope.GetSymbol(newName);
					if (smbl2 is DeclSymbol)
					{
						// static func defined in local class
					}
					else
					{
						// if it is a non static func defined in local class
						newName = $"{_currentClass.Name.Name}::{callExpr.FuncName.Name}{callExpr.Arguments.GetArgsString(callExpr.TypeOrObjectName.OutType)}";
					}
				}
				else if (callExpr.TypeOrObjectName.OutType is PointerType ptrTp && ptrTp.TargetType is ClassType clsTp)
				{
					// if we are calling like 'a.Anime()' where 'a' is an object
					// we need to rename the func name call like that:
					newName = $"{clsTp.Declaration.Name.Name}::{callExpr.FuncName.Name}{callExpr.Arguments.GetArgsString(callExpr.TypeOrObjectName.OutType)}";
				}
				else if (callExpr.TypeOrObjectName.OutType is ClassType clsTpStatic)
				{
					// if we are calling like 'A.Anime()' where 'A' is a class
					// we need to rename the func name call like that:
					newName = $"{clsTpStatic.Declaration.Name.Name}::{callExpr.FuncName.Name}{callExpr.Arguments.GetArgsString()}";
				}
				else
				{
					// error here: the function call could not be infered
					_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr, $"The function call could not be inferred");
				}

				callExpr.FuncName = callExpr.FuncName.GetCopy(newName);
				PostPrepareIdentifierInference(callExpr.FuncName);
			}

			// setting parameters
			var sym = callExpr.Scope.GetSymbol(callExpr.FuncName.Name);
			if (sym is DeclSymbol typed && typed.Decl is AstFuncDecl funcDecl)
			{
				// checking if it is a static func
				callExpr.StaticCall = funcDecl.SpecialKeys.Contains(TokenType.KwStatic);
			}
			else
			{
				// error here
				_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr, $"The function could not be found in the scope");
			}

			// setting call expr out type
			if (callExpr.FuncName.OutType is FunctionType funcType)
			{
				// call expr type is the same as func return type
				callExpr.OutType = funcType.Declaration.Returns.OutType;
			}
			else
			{
				// error here
				_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr, $"The calling thing has to be a function");
			}
		}

		private void PostPrepareCastExprInference(AstCastExpr castExpr)
		{
			PostPrepareExprInference(castExpr.SubExpression as AstExpression);
			PostPrepareExprInference(castExpr.TypeExpr as AstExpression);
			castExpr.OutType = (castExpr.TypeExpr as AstExpression).OutType;
		}

		private void PostPrepareNestedExprInference(AstNestedExpr nestExpr, out bool itWasPropa, bool propaSet = false)
		{
			if (nestExpr.LeftPart == null)
			{
				PostPrepareExprInference(nestExpr.RightPart);
				nestExpr.OutType = nestExpr.RightPart.OutType;
			}
			else
			{
				Scope leftSideScope = null;
				bool foundNs = false;
				InternalNormalizeLeftPartIfItIsANamespaceWithType(nestExpr, ref foundNs);
				PostPrepareExprInference(nestExpr.LeftPart);
				if (nestExpr.LeftPart.OutType is PointerType ptr && ptr.TargetType is ClassType classT)
					leftSideScope = classT.Declaration.SubScope;
				else if (nestExpr.LeftPart.OutType is StructType structt)
					leftSideScope = structt.Declaration.SubScope;
				else if (nestExpr.LeftPart.OutType is StringType)
					leftSideScope = AstStringExpr.StringStruct.SubScope;
				else if (nestExpr.LeftPart.OutType is ArrayType)
					leftSideScope = AstArrayExpr.ArrayStruct.SubScope;
				// TODO: structs and other

				if (leftSideScope == null)
				{
					_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, nestExpr.LeftPart, "The type of the expression has to be a class or a struct");
					itWasPropa = false;
					return;
				}

				// here could only be an AstIdExpr because AstCallExpr and AstExpression would be in 'if' block upper
				if (nestExpr.RightPart is not AstIdExpr idExpr)
				{
					_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, nestExpr.RightPart, "The expressions has to be an identifier");
					itWasPropa = false;
					return;
				}

				// searching for the symbol in the class/struct
				var smbl = leftSideScope.GetSymbol(idExpr.Name);
				if (smbl is DeclSymbol typed)
				{
					idExpr.OutType = typed.Decl.Type.OutType;
					nestExpr.OutType = idExpr.OutType;

					// if the ast is an access to a property
					if (typed.Decl is AstPropertyDecl)
					{
						// if getting property to set smth
						if (propaSet)
						{
							itWasPropa = true;
							return;
						}
						else
						{
							// if getting propa to get
							var fncCall = new AstCallExpr(nestExpr.LeftPart, idExpr.GetCopy($"get_{idExpr}"), null, nestExpr);
							SetScopeAndParent(fncCall, nestExpr.RightPart.NormalParent, nestExpr.RightPart.Scope);
							nestExpr.LeftPart = null;
							nestExpr.RightPart = fncCall;
							PostPrepareCallExprInference(fncCall);
						}
					}
				}
				else
				{
					_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, $"The type could not be infered in {leftSideScope} scope...");
				}
			}
			itWasPropa = false;
		}

		// :)
		/// <summary>
		/// This shite is used to join namespace with type (if exist) to a one AstIdExpr as a right part
		/// If we have AstNested like 'System.Runtime.InteropServices.DllImportAttribute.DllName'
		/// I would like to have 'System.Runtime.InteropServices.DllImportAttribute' as one AstId
		/// Because it is just a type
		/// </summary>
		/// <param name="nestExpr">The shite</param>
		private void InternalNormalizeLeftPartIfItIsANamespaceWithType(AstNestedExpr nestExpr, ref bool found)
		{
			string flatten = nestExpr.TryFlatten(null, null);
			if (string.IsNullOrWhiteSpace(flatten))
				return; // no need to normalize this shite :)

			if (nestExpr.LeftPart == null)
				return;

			InternalNormalizeLeftPartIfItIsANamespaceWithType(nestExpr.LeftPart, ref found);

			// check is it namespace
			string leftString = (nestExpr.LeftPart.RightPart as AstIdExpr).Name;
			bool foundNs = nestExpr.Scope.IsStringNamespaceOrPart(leftString);
			// go all over the usings
			foreach (var usng in _currentSourceFile.Usings)
			{
				// getting ns string
				var ns = usng.FlattenNamespace;
				if (nestExpr.Scope.IsStringNamespaceOrPart($"{ns}.{leftString}"))
				{
					foundNs = true;
					break;
				}
			}

			// check is it namespace
			if (foundNs)
			{
				// if it is a namespace - join with current right side and try again
				nestExpr.RightPart = (nestExpr.RightPart as AstIdExpr).GetCopy($"{leftString}.{(nestExpr.RightPart as AstIdExpr).Name}");
			}
			else
			{
				if (!found)
				{
					// if it is not a namespace - then probably type is done
					nestExpr.LeftPart.LeftPart = null;
					nestExpr.LeftPart.RightPart.Location = nestExpr.LeftPart.Location;
					found = true;
				}
			}
		}

		private void PostPrepareArrayExprInference(AstArrayExpr arrayExpr)
		{
			PostPrepareExprInference(arrayExpr.SubExpression);
			arrayExpr.OutType = ArrayType.GetArrayType(arrayExpr.SubExpression.OutType);
		}

		private void PostPrepareArrayCreateExprInference(AstArrayCreateExpr arrayExpr)
		{
			foreach (var sz in arrayExpr.SizeExprs)
			{
				PostPrepareExprInference(sz);
			}
			// TODO: you can check if the size is available at compile time and create the array on stack

			PostPrepareExprInference(arrayExpr.TypeName);

			// create an expecting elements type to be
			HapetType expectingElementType = arrayExpr.TypeName.OutType;
			int sizeAmount = arrayExpr.SizeExprs.Count;
			// preparing for ndim arrays
			while (sizeAmount > 1)
			{
				expectingElementType = ArrayType.GetArrayType(expectingElementType);
				sizeAmount--;
			}

			// infer elements
			for (int i = 0; i < arrayExpr.Elements.Count; ++i)
			{
				var e = arrayExpr.Elements[i];
				PostPrepareExprInference(e);
				// try to use implicit cast if it can be used
				arrayExpr.Elements[i] = PostPrepareExpressionWithType(expectingElementType, e);
			}

			// preparing the array
			PostPrepareFullArray(arrayExpr);
		}

		private void PostPrepareArrayAccessExprInference(AstArrayAccessExpr arrayAccExpr)
		{
			PostPrepareExprInference(arrayAccExpr.ParameterExpr);
			PostPrepareExprInference(arrayAccExpr.ObjectName);

			HapetType outType = null;
			if (arrayAccExpr.ObjectName.OutType is ArrayType arrayType)
				outType = arrayType.TargetType;
			else if (arrayAccExpr.ObjectName.OutType is StringType)
				outType = CharType.DefaultType; // TODO: mb non default could be here? idk :)
			else if (arrayAccExpr.ObjectName.OutType is PointerType ptrType)
				outType = ptrType.TargetType;
			else
			{
				// error because expected an array 
				_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, arrayAccExpr.ObjectName, $"Array/String type expected to be indexed");
			}
			arrayAccExpr.OutType = outType;
		}

		// statements
		private void PostPrepareAssignStmtInference(AstAssignStmt assignStmt, out bool itWasPropa)
		{
			// propaSet is true only here
			PostPrepareNestedExprInference(assignStmt.Target, out itWasPropa, true);

			if (assignStmt.Value != null)
			{
				if (assignStmt.Value is AstDefaultExpr)
				{
					// get the default value for the type (no need to infer)
					assignStmt.Value = AstDefaultExpr.GetDefaultValueForType(assignStmt.Target.OutType, assignStmt.Value);
					if (assignStmt.Value == null)
						_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, assignStmt, "Default value for the type was not found");
				}
				else
				{
					// if it is not a default
					PostPrepareExprInference(assignStmt.Value);
				}
				PostPrepareVariableAssign(assignStmt);
			}
		}

		private void PostPrepareForStmtInference(AstForStmt forStmt)
		{
			if (forStmt.FirstParam != null)
				PostPrepareExprInference(forStmt.FirstParam);
			if (forStmt.SecondParam != null)
			{
				PostPrepareExprInference(forStmt.SecondParam);

				// error if it is not a bool type because it has to be
				if (forStmt.SecondParam.OutType is not BoolType)
				{
					_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, forStmt.SecondParam, "Type of the expression has to be boolean type");
				}
			}
			if (forStmt.ThirdParam != null)
				PostPrepareExprInference(forStmt.ThirdParam);

			PostPrepareExprInference(forStmt.Body);
		}

		private void PostPrepareWhileStmtInference(AstWhileStmt whileStmt)
		{
			PostPrepareExprInference(whileStmt.ConditionParam);

			// error if it is not a bool type because it has to be
			if (whileStmt.ConditionParam.OutType is not BoolType)
			{
				_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, whileStmt.ConditionParam, "Type of the expression has to be boolean type");
			}

			PostPrepareExprInference(whileStmt.Body);
		}

		private void PostPrepareIfStmtInference(AstIfStmt ifStmt)
		{
			PostPrepareExprInference(ifStmt.Condition);

			// error if it is not a bool type because it has to be
			if (ifStmt.Condition.OutType is not BoolType)
			{
				_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, ifStmt.Condition, "Type of the expression has to be boolean type");
			}

			PostPrepareExprInference(ifStmt.BodyTrue);
			if (ifStmt.BodyFalse != null)
				PostPrepareExprInference(ifStmt.BodyFalse);
		}

		private void PostPrepareSwitchStmtInference(AstSwitchStmt switchStmt)
		{
			PostPrepareExprInference(switchStmt.SubExpression);

			// used to check that there are no more than 1 default case
			bool thereWasADefaultCase = false;

			foreach (var cc in switchStmt.Cases)
			{
				PostPrepareExprInference(cc);

				// calc default cases. if there are more than 1 - error
				if (cc.DefaultCase)
				{
					if (thereWasADefaultCase)
						_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, cc.Pattern, "Only one 'default' case is allowed");
					thereWasADefaultCase = true;
					continue; // do not check for pattern in default expr...
				}

				// trying to implicitly cast cast value into switch sub expr
				cc.Pattern = PostPrepareExpressionWithType(switchStmt.SubExpression.OutType, cc.Pattern);

				// check that the value is a const 
				if (cc.Pattern.OutValue == null)
				{
					_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, cc.Pattern, "Only constant values allowed in 'case' statements");
				}
			}
		}

		private void PostPrepareCaseStmtInference(AstCaseStmt caseStmt)
		{
			if (!caseStmt.DefaultCase)
				PostPrepareExprInference(caseStmt.Pattern);

			if (!caseStmt.FallingCase)
				PostPrepareExprInference(caseStmt.Body);
		}

		private void PostPrepareReturnStmtInference(AstReturnStmt returnStmt)
		{
			if (returnStmt.ReturnExpression != null)
			{
				PostPrepareExprInference(returnStmt.ReturnExpression);
				// casting to func return type
				returnStmt.ReturnExpression = PostPrepareExpressionWithType(_currentFunction.Returns.OutType, returnStmt.ReturnExpression);
			}
			else if (returnStmt.ReturnExpression == null && _currentFunction.Returns.OutType is not VoidType)
			{
				_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, returnStmt, $"Empty 'return' statement in function that has to return {_currentFunction.Returns.OutType}");
			}
		}

		private void PostPrepareAttributeStmtInference(AstAttributeStmt attrStmt)
		{
			// purified type string with namespace in it!
			// we need this so when saving the attributes into metafile 
			// we would know namespace of the attribute and so on.
			// (kostyl?)
			var newTypeAst = attrStmt.AttributeName.GetTypeAstId(_compiler.MessageHandler, _currentSourceFile);
			PostPrepareExprInference(newTypeAst);
			attrStmt.AttributeName.SetTypeAstId(newTypeAst);

			foreach (var a in attrStmt.Parameters)
			{
				PostPrepareExprInference(a);
				// all attr params has to be const values
				if (a.OutValue == null)
				{
					_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, a, $"Parameter value has to be compile time available");
				}
			}
		}
	}
}
