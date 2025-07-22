using System.Runtime.CompilerServices;
using System.Xml.Linq;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Entities;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
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

        /// <summary>
        /// This propa is only used when debugging to see which ast i see
        /// </summary>
        [JsonIgnore]
        public virtual string AAAName => nameof(AstStatement);

        private Guid Guid { get; set; } // just for debug

        public AstStatement(ILocation location = null)
        {
            this.Location = location;

            Guid = Guid.NewGuid();
        }

        public abstract AstStatement GetDeepCopy();

        public bool IsExactly<T>() where T : class
        {
            return this.GetType() == typeof(T);
        }

        public bool IsExactly(AstStatement type)
        {
            return this.GetType() == type.GetType();
        }

        #region Helper functions
        public void SetDataFromStmt(AstStatement stmt, bool outTypeAndValue = false)
        {
            Scope = stmt.Scope;
            if (outTypeAndValue && stmt is AstExpression expr && this is AstExpression thisExpr)
            {
                thisExpr.OutType = expr.OutType;
                thisExpr.OutValue = expr.OutValue;
            }
            Location = stmt.Location;
            SourceFile = stmt.SourceFile;
            Parent = stmt.Parent;
        }
        #endregion
    }
}
