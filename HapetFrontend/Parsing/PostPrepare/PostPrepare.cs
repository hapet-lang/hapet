using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Types;

namespace HapetFrontend.Parsing.PostPrepare
{
	public partial class PostPrepare
	{
		private readonly Compiler _compiler;

		/// <summary>
		/// File that is currently preparing
		/// </summary>
		private ProgramFile _currentSourceFile;

		/// <summary>
		/// The class decl that is currently preparing
		/// </summary>
		private AstClassDecl _currentClass;

		/// <summary>
		/// The function decl that is currently preparing
		/// </summary>
		private AstFuncDecl _currentFunction;

		public PostPrepare(Compiler compiler)
		{
			_compiler = compiler;
		}

		public int StartPreparation()
		{
			PostPrepareClassMethods();
			PostPrepareScoping();

			// generate metadata file
			int result = PostPrepareMetadata();
			if (result != 0)
				return result;

            PostPrepareTypeInference();

			SearchForMainFunction();
			CallAllStaticCtors();
			return 0;
		}

		private void SearchForMainFunction()
		{
			foreach (var clsDecl in _serializeClassesMetadata)
			{
				foreach (var decl in clsDecl.Declarations)
				{
					if (decl is not AstFuncDecl)
						continue;

					var funcDecl = decl as AstFuncDecl;
					if (funcDecl.Name.Name.EndsWith("Main(string[])") &&
						funcDecl.Returns.OutType == IntType.GetIntType(4, true) &&
						funcDecl.Parameters.Count == 1)
					{
						_compiler.MainFunction = funcDecl;
					}
				}
			}
		}

		private void CallAllStaticCtors()
		{
			if (_compiler.MainFunction == null)
				return;

			var unique = _allUsedClassesInProgram.Distinct();
			foreach (var cls in unique)
			{
				// check that the class has suppress stor call attr
				// and skip the class without calling it's stor
				string suppressAttrName = "System.SuppressStaticCtorCallAttribute";
				var suppressAttr = cls.Attributes.FirstOrDefault(x => x.AttributeName.TryFlatten(_compiler.MessageHandler, _currentSourceFile) == suppressAttrName);
				if (suppressAttr != null)
					continue;

				// creating stor call ast
				string funcName = $"{cls.Name.Name.Split('.').Last()}_stor";
				var call = new AstCallExpr(new AstNestedExpr(cls.Name.GetCopy(), null), new AstIdExpr(funcName));
				SetScopeAndParent(call, _compiler.MainFunction.Body, _compiler.MainFunction.Body.SubScope);
				PostPrepareExprScoping(call);
				PostPrepareExprInference(call);

				// TODO: sort the static ctors calls by hierarchy
				_compiler.MainFunction.Body.Statements.Insert(0, call);
			}
		}
	}
}
