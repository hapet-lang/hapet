using HapetFrontend.Ast;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Scoping;

namespace HapetFrontend.Entities
{
    public class ProgramFile
    {
        /// <summary>
        /// Filename without path parts
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The full folder name where file is located
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// To grab the text only once and store it here
        /// </summary>
        public string Text { get; }

        public Scope NamespaceScope { get; set; }

        public List<AstStatement> Statements { get; } = new List<AstStatement>();
        public List<AstUsingStmt> Usings { get; set; } = new List<AstUsingStmt>();

        public ProgramFile(string name, string text)
        {
            this.Name = name;
            this.Text = text;
        }

        public override string ToString()
        {
            return $"ProgramFile: {Name}";
        }
    }
}
