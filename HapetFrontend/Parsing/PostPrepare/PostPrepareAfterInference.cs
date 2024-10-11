using HapetFrontend.Ast.Declarations;

namespace HapetFrontend.Parsing.PostPrepare
{
	public partial class PostPrepare
	{
		public void PostPrepareAfterInference()
		{
			foreach (var (path, file) in _compiler.GetFiles())
			{
				_currentSourceFile = file;
				foreach (var stmt in file.Statements)
				{
					if (stmt is AstClassDecl classDecl)
					{
						
					}
				}
			}
		}
	}
}
