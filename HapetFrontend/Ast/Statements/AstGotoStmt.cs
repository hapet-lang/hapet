using System.Diagnostics;

namespace HapetFrontend.Ast.Statements
{
    public class AstGotoStmt : AstStatement
    {
        /// <summary>
        /// Label into which we need to go
        /// </summary>
        public string GotoLabel { get; set; }

        public override string AAAName => nameof(AstGotoStmt);

        public AstGotoStmt(string label, ILocation location = null) : base(location)
        {
            GotoLabel = label;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstGotoStmt(
                GotoLabel,
                Location)
            {
                Scope = Scope,
                SourceFile = SourceFile,
            };
            return copy;
        }
    }
}
