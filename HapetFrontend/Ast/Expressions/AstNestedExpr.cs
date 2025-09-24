using HapetFrontend.Ast.Declarations;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;

namespace HapetFrontend.Ast.Expressions
{
    public class AstNestedExpr : AstExpression
    {
        /// <summary>
        /// This is the left part of an id expr like the 'a.mm.anime.Test'
        /// where 'Test' would be the <see cref="RightPart"/> and 'a.mm.anime' would be the <see cref="LeftPart"/> 
        /// with its parsed names.
        /// or
        /// '(chlen as Pivo).Cringe;'
        /// </summary>
        public AstNestedExpr LeftPart { get; set; }

        /// <summary>
        /// The right part of the expression
        /// Could only be <see cref="AstCallExpr"/> or <see cref="AstIdExpr"/> or <see cref="AstPointerExpr"/> or real pure <see cref="AstExpression"/>
        /// </summary>
        public AstExpression RightPart { get; set; }

        public override string AAAName => nameof(AstNestedExpr);

        public AstNestedExpr(AstExpression rightPart, AstNestedExpr leftPart, ILocation location = null) : base(location)
        {
            this.RightPart = rightPart;
            this.LeftPart = leftPart;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstNestedExpr(
                RightPart.GetDeepCopy() as AstExpression,
                LeftPart?.GetDeepCopy() as AstNestedExpr,
                Location)
            {
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
                TupleNameList = TupleNameList,
            };
            return copy;
        }

        /// <summary>
        /// The function tries to flatten NestedExpr to smth like 'anm.dawt.Arrw'
        /// </summary>
        /// <param name="messageHandler">Message handler</param>
        /// <param name="file">The file that is currently preparing (to get text for error)</param>
        /// <returns>Flatten string</returns>
        public string TryFlatten(IMessageHandler messageHandler, ProgramFile file, bool forCodegen = false)
        {
            if (RightPart is not AstIdExpr idExpr)
            {
                if (messageHandler != null)
                    messageHandler.ReportMessage(file, RightPart, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                return string.Empty;
            }

            var idName = forCodegen ? GenericsHelper.GetCodegenGenericName(idExpr, messageHandler) : GenericsHelper.GetNameFromAst(idExpr, messageHandler);
            if (LeftPart == null)
                return idName;

            return $"{LeftPart.TryFlatten(messageHandler, file)}.{idName}";
        }

        public T UnrollToRightPart<T>()
        {
            if (RightPart is T)
            {
                return (T)(object)RightPart;
            }
            else if (RightPart is AstNestedExpr nestNest)
            {
                return nestNest.UnrollToRightPart<T>();
            }
            else
            {
                return default;
            }
        }

        public void AddToTheEnd(AstNestedExpr newNst)
        {
            if (LeftPart == null)
                LeftPart = newNst;
            else
                LeftPart.AddToTheEnd(newNst);
        }
    }
}
