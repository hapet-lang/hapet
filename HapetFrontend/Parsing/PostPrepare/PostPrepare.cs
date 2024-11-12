using HapetFrontend.Ast.Declarations;
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

		/// <summary>
		/// Used to supress all warnings while post preparing
		/// This is needed when including other projects metadata into current one
		/// SO their warnings won't be shown
		/// </summary>
		public bool SupressWarnings { get; set; }

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
	}
}
