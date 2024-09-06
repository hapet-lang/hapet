using HapetFrontend.Ast.Declarations;
using HapetFrontend.Entities;

namespace HapetFrontend.Parsing.PostPrepare
{
	public static class PostPrepareProgramFile
	{
		public static void PostPrepare(ProgramFile file)
		{
			foreach (var stmt in file.Statements)
			{
				if (stmt is AstClassDecl classDecl)
				{
					PostPrepareClass(classDecl);
				}
			}
		}

		private static void PostPrepareClass(AstClassDecl classDecl)
		{
			foreach (var decl in classDecl.Declarations)
			{
				decl.Scope = new Scoping.Scope($"{classDecl.Name.Name}_scope", classDecl.Scope);
				decl.Parent = classDecl;

				if (decl is AstFuncDecl funcDecl)
				{
					funcDecl.ContainingClass = classDecl;
				}				
			}
		}

		// TODO: recursively go through all of the statments and set Scope and Parent
	}
}
