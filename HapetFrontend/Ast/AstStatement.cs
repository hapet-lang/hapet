using HapetFrontend.Ast.Declarations;
using HapetFrontend.Entities;
using HapetFrontend.Scoping;
using Newtonsoft.Json;

namespace HapetFrontend.Ast
{
    public interface IAstNode
    {
        IAstNode Parent { get; }
    }

    public abstract class AstStatement : ILocation, IAstNode
    {
        public ILocation Location { get; set; }
        [JsonIgnore]
        public TokenLocation Beginning => Location?.Beginning;
        [JsonIgnore]
        public TokenLocation Ending => Location?.Ending;

        /// <summary>
        /// In which scope it could be accessable
        /// </summary>
        [JsonIgnore]
        public Scope Scope { get; set; }

        /// <summary>
        /// The file in which the statement is located
        /// </summary>
        [JsonIgnore]
        public ProgramFile SourceFile { get; set; }

        /// <summary>
        /// Parent ast node
        /// </summary>
        [JsonIgnore]
        public IAstNode Parent { get; set; }
        /// <summary>
        /// Parent ast node as AstStatement
        /// </summary>
        [JsonIgnore]
        public AstStatement NormalParent => Parent as AstStatement;

        public AstStatement(ILocation Location = null)
        {
            this.Location = Location;
        }

        #region Helper functions
        public AstFuncDecl FindContainingFunction()
        {
            var currentParent = NormalParent;
            while (currentParent != null)
            {
                if (currentParent is AstFuncDecl funcDecl)
                {
                    return funcDecl;
                }
                currentParent = currentParent.NormalParent;
            }
            return null;
        }
        #endregion
    }
}
