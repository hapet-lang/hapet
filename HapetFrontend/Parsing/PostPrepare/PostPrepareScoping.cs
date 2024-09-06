using HapetFrontend.Ast.Declarations;

namespace HapetFrontend.Parsing.PostPrepare
{
	public partial class PostPrepare
	{
		private void PostPrepareScoping()
		{
			foreach (var (path, file) in _compiler.GetFiles())
			{
				foreach (var stmt in file.Statements)
				{
					if (stmt is AstClassDecl classDecl)
					{
						PostPrepareClassScoping(classDecl);
					}
				}
			}
		}

		private void PostPrepareClassScoping(AstClassDecl classDecl)
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
