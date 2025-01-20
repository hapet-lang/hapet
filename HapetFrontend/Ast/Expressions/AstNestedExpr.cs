using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Parsing;

namespace HapetFrontend.Ast.Expressions
{
    public class AstNestedExpr : AstExpression
    {
        /// <summary>
        /// This is the left part of an id expr like the 'a.mm.anime.Test'
        /// where 'Test' would be the <see cref="RightPart"/> and 'a.mm.anime' would be the <see cref="LeftPart"/> 
        /// with its parsed names
        /// </summary>
        public AstNestedExpr LeftPart { get; set; }

        /// <summary>
        /// The right part of the expression
        /// Could only be <see cref="AstCallExpr"/> or <see cref="AstIdExpr"/> or <see cref="AstPointerExpr"/> or real pure <see cref="AstExpression"/>
        /// </summary>
        public AstExpression RightPart { get; set; }

        public AstNestedExpr(AstExpression rightPart, AstNestedExpr leftPart, ILocation Location = null) : base(Location)
        {
            this.RightPart = rightPart;
            this.LeftPart = leftPart;
        }

        /// <summary>
        /// The function tries to flatten NestedExpr to smth like 'anm.dawt.Arrw'
        /// </summary>
        /// <param name="messageHandler">Message handler</param>
        /// <param name="file">The file that is currently preparing (to get text for error)</param>
        /// <returns>Flatten string</returns>
        public string TryFlatten(IMessageHandler messageHandler, ProgramFile file)
        {
            if (RightPart is not AstIdExpr idExpr)
            {
                if (messageHandler != null)
                    messageHandler.ReportMessage(file.Text, RightPart, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                return string.Empty;
            }

            if (LeftPart == null)
                return idExpr.Name;

            return $"{LeftPart.TryFlatten(messageHandler, file)}.{idExpr.Name}";
        }

        /// <summary>
        /// Types in program with namespaces are parsed into AstNested shite. 
        /// But for easier types inference we should convert AstNested into pure AstId
        /// And this approuch would help with metadata types (where we should save type with its namespace)
        /// </summary>
        /// <param name="messageHandler">Message handler</param>
        /// <param name="file">The file that is currently preparing (to get text for error)</param>
        /// <returns>Flatten string inside AstId</returns>
        public AstIdExpr GetTypeAstId(IMessageHandler messageHandler, ProgramFile file)
        {
            string currentFlatten = TryFlatten(messageHandler, file);
            AstIdExpr fullTypeAstId = new AstIdExpr(currentFlatten, Location);
            fullTypeAstId.Parent = Parent;
            fullTypeAstId.OutValue = OutValue;
            fullTypeAstId.OutType = OutType;
            fullTypeAstId.Scope = Scope;
            fullTypeAstId.SourceFile = SourceFile;
            return fullTypeAstId;
        }

        public void SetTypeAstId(AstIdExpr astId)
        {
            RightPart = astId;
            LeftPart = null;
            OutType = astId.OutType;
        }
    }
}
