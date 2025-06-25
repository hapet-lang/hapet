using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast;
using HapetFrontend.Types;
using HapetFrontend.Scoping;
using HapetFrontend.Ast.Expressions;
using HapetPostPrepare.Entities;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        /// <summary>
        /// Probably used only for proper error messaging - to not error 
        /// when there are multiple generics
        /// </summary>
        /// <returns></returns>
        public bool IsParentNormalOrPureGeneric()
        {
            var nearestDecl = _currentParentStack.GetNearestParentClassOrStruct();
            if (nearestDecl == null)
                return true; // allow?

            if (nearestDecl.Name is not AstIdGenericExpr)
                return true; // normal

            if (nearestDecl.HasGenericTypes && !nearestDecl.IsImplOfGeneric)
                return true; // pure generic

            return false;
        }

        #region Array handling cringe
        public ArrayType GetArrayType(AstExpression targetType, AstExpression subExpr)
        {
            var inInfo = InInfo.Default;
            var outInfo = OutInfo.Default;

            List<AstExpression> genTypes;
            if (targetType.OutType == GenericType.LiteralType)
                genTypes = new List<AstExpression>() 
                { 
                    new AstNestedExpr(new AstIdExpr("T", subExpr) 
                    { 
                        Scope = subExpr.Scope,
                        SourceFile = subExpr.SourceFile,
                    }, null, subExpr) 
                    {
                        Scope = subExpr.Scope,
                        SourceFile = subExpr.SourceFile,
                    } 
                };
            else
            {
                var typeAst = targetType;
                var nst = new AstNestedExpr(typeAst, null, subExpr)
                {
                    Scope = subExpr.Scope,
                    SourceFile = subExpr.SourceFile,
                };
                if (typeAst is AstNestedExpr nstTarget)
                {
                    // fix nested if target is also nested
                    nst.RightPart = nstTarget.RightPart;
                    nst.LeftPart = nstTarget.LeftPart;
                }

                genTypes = new List<AstExpression>() { nst };
            }
                
            var idExpr = new AstIdGenericExpr("System.Array", genTypes, subExpr) 
            {
                Scope = subExpr.Scope,
                SourceFile = subExpr.SourceFile,
            };
            PostPrepareIdentifierInference(idExpr, inInfo, ref outInfo);
            return idExpr.OutType as ArrayType;
        }

        public StringType GetStringType(AstExpression subExpr)
        {
            var inInfo = InInfo.Default;
            var outInfo = OutInfo.Default;

            var idExpr = new AstIdExpr("System.String", subExpr)
            {
                Scope = subExpr.Scope,
                SourceFile = subExpr.SourceFile,
            };
            PostPrepareIdentifierInference(idExpr, inInfo, ref outInfo);
            return idExpr.OutType as StringType;
        }
        #endregion
    }
}
