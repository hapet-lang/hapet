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
        /// Full filepath 
        /// </summary>
        public Uri FilePath { get; set; }

        /// <summary>
        /// The full folder name where file is located
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// To grab the text only once and store it here
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Is the file imported/virtual from another assembly
        /// </summary>
        public bool IsImported { get; set; }

        public Scope NamespaceScope { get; set; }

        public List<AstStatement> Statements { get; } = new List<AstStatement>();
        public List<AstUsingStmt> Usings { get; set; } = new List<AstUsingStmt>();
        /// <summary>
        /// Handles all the #define's that could be accessed accross the whole file
        /// </summary>
        public List<AstDirectiveStmt> Defines { get; private set; } = new List<AstDirectiveStmt>();

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
