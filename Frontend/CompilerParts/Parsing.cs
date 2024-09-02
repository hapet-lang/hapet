using Frontend.Ast.Declarations;
using Frontend.Ast.Expressions;
using Frontend.Ast;
using Frontend.Errors;
using Frontend.Parsing;
using Frontend.Scoping;
using Frontend.Ast.Statements;

namespace Frontend
{
	public partial class Compiler
	{
		private Dictionary<string, Lexer> _loadingFiles = new Dictionary<string, Lexer>();

		private PtFile ParseFile(string fileName, string body, IErrorHandler eh, bool globalScope = false)
		{
			var lexer = body != null ? Lexer.FromString(body, eh, fileName) : Lexer.FromFile(fileName, eh);

			_loadingFiles.Add(fileName, lexer);

			if (lexer == null)
				return null;

			var parser = new Parser(lexer, eh);

			var fileScope = globalScope ?
				_globalScope :
				new Scope($"{Path.GetFileNameWithoutExtension(fileName)}.che", _globalScope);
			var file = new PtFile(fileName, lexer.Text, fileScope);

			bool isPublic = false;

			void HandleStatement(AstStatement s)
			{
				s.Scope = file.FileScope;
				if ((s is AstConstantDeclaration cd && cd.TypeExpr is AstClassTypeExpr) ||
					(s is AstConstantDeclaration cd2 && cd2.TypeExpr is AstStructTypeExpr) ||
					(s is AstConstantDeclaration cd3 && cd3.TypeExpr is AstEnumTypeExpr) ||
					(s is AstExprStmt es && es.Expr is AstUsingExpr) ||
					(s is AstAttachStmt ass && ass.Value is AstUsingExpr))
				{
					s.SourceFile = file;
					s.SetFlag(StmtFlags.GlobalScope);
					// s.SetFlag(StmtFlags.ExportScope, isPublic);
					file.Statements.Add(s);
				}
				// TODO: do i need this export shite?
				//else if (s is AstDirectiveStatement dir && dir.Directive.Name.Name == "file_scope")
				//{
				//	isPublic = false;
				//}
				//else if (s is AstDirectiveStatement dir2 && dir2.Directive.Name.Name == "export_scope")
				//{
				//	isPublic = true;
				//}
				// TODO: handle
				//else if (s is AstDirectiveStatement directive)
				//{
				//	HandleDirective(directive, eh, lexer, file);
				//}
				else if (s != null)
				{
					eh.ReportError(lexer.Text, s, "This type of statement is not allowed in global scope");
				}
			}

			while (true)
			{

				var s = parser.ParseStatement();
				if (s == null)
					break;

				HandleStatement(s);
			}

			_loadingFiles.Remove(fileName);
			return file;
		}
	}
}
