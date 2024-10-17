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
		/// The function decl that is currently preparing
		/// </summary>
		private AstFuncDecl _currentFunction;

		public PostPrepare(Compiler compiler)
		{
			_compiler = compiler;
		}

		public void StartPreparation()
		{
			PostPrepareClassMethods();
			PostPrepareScoping();
			PostPrepareTypeInference();

			SearchForMainFunction();
		}

		private void SearchForMainFunction()
		{
			foreach (var (path, file) in _compiler.GetFiles())
			{
				foreach (var stmt in file.Statements)
				{
					if (stmt is not AstClassDecl)
						continue;

					foreach (var decl in (stmt as AstClassDecl).Declarations)
					{
						if (decl is not AstFuncDecl)
							continue;

						// TODO: remake to "int Main(string[] args)"
						var funcDecl = decl as AstFuncDecl;
						if (funcDecl.Name.Name == "Main(string[])" &&
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
}
