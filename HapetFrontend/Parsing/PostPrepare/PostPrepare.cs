using HapetFrontend.Ast.Declarations;
using HapetFrontend.Types;

namespace HapetFrontend.Parsing.PostPrepare
{
	public partial class PostPrepare
	{
		private readonly Compiler _compiler;

		public PostPrepare(Compiler compiler)
		{
			_compiler = compiler;
		}

		public void StartPreparation()
		{
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
						if (funcDecl.Name.Name == "Main" &&
							funcDecl.Returns.OutType == IntType.GetIntType(4, true) &&
							funcDecl.Parameters.Count == 0)
						{
							_compiler.MainFunction = funcDecl;
						}
					}
				}
			}
		}
	}
}
