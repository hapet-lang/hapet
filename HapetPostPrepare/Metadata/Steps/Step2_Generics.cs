using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Enums;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;
using System.Data;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private bool PostPrepareMetadataGenerics(AstStatement stmt)
        {
            if (stmt is not AstDeclaration decl)
                return false;

            // we need only generics
            if (!decl.HasGenericTypes)
                return false;

            // we need only PURE generics
            if (decl.IsImplOfGeneric)
                return false;

            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            // gens that do not have generic constrains
            var hasNoGenerics = GetFilteredConstrains((decl.Name as AstIdGenericExpr).GenericRealTypes, decl, false);
            // we need to set types to ids
            foreach (var (g, currContrains) in hasNoGenerics)
            {
                var originalGeneric = g;
                var currGeneric = (originalGeneric as AstNestedExpr).RightPart as AstIdExpr;
                if (currGeneric == null) Debug.Assert(false, "Should not happen");

                // getting the genericType
                var genericDecl = GetGenericDeclaration(currContrains, currGeneric, decl, inInfo, ref outInfo);

                originalGeneric.OutType = genericDecl.Type.OutType;
                currGeneric.OutType = originalGeneric.OutType;
                // cringe kostyl
                currGeneric.FindSymbol = new DeclSymbol(genericDecl.Name, genericDecl);

                // append to parent stack
                _currentParentStack.AppendCurrentGenericIdMapping(currGeneric.Name, genericDecl.Type.OutType as GenericType);
            }

            // gens that have generic constrains
            var hasGenerics = GetFilteredConstrains((decl.Name as AstIdGenericExpr).GenericRealTypes, decl, true);
            // generic contrains that contain generics could not be cached
            foreach (var (g, currContrains) in hasGenerics)
            {
                var originalGeneric = g;
                var currGeneric = (originalGeneric as AstNestedExpr).RightPart as AstIdExpr;
                if (currGeneric == null) Debug.Assert(false, "Should not happen");

                // at first - create them all
                var genericDecl = CreateGenericDeclaration(currContrains, currGeneric, decl);

                originalGeneric.OutType = genericDecl.Type.OutType;
                currGeneric.OutType = originalGeneric.OutType;
                // cringe kostyl
                currGeneric.FindSymbol = new DeclSymbol(genericDecl.Name, genericDecl);

                // append to parent stack
                _currentParentStack.AppendCurrentGenericIdMapping(currGeneric.Name, genericDecl.Type.OutType as GenericType);
            }
            var saved = inInfo.SkipGenericConstrainsCheckWhenInstancing;
            inInfo.SkipGenericConstrainsCheckWhenInstancing = true;
            foreach (var (g, currContrains) in hasGenerics)
            {
                var genericDecl = (g.OutType as GenericType).Declaration;

                PrepareNonGenericConstrains(currContrains, inInfo, ref outInfo);
                AddObjectClassConstainIfNeeded(currContrains, decl, inInfo, ref outInfo);
                // preparing current constrains and then checking
                PrepareGenericConstrains(currContrains, inInfo, ref outInfo);
                // check that constrains are ok
                CheckIfConstrainsAreOk(currContrains);
                // post prepare
                PostPrepareGenericDeclConstrains(genericDecl, inInfo, ref outInfo);
                // add it to preparation pipe
                AllGenericsMetadata.Add(genericDecl);
            }
            inInfo.SkipGenericConstrainsCheckWhenInstancing = saved;
            foreach (var (g, currContrains) in hasGenerics)
            {
                var genericDecl = (g.OutType as GenericType).Declaration;
                foreach (var c in currContrains)
                {
                    if (c.ConstrainType == GenericConstrainType.CustomType && (c.Expr.UnrollToRightPart<AstIdExpr>() is AstIdGenericExpr idExprG))
                    {
                        var orig = (idExprG.FindSymbol as DeclSymbol).Decl.OriginalGenericDecl;
                        PostPrepareMetadataGenerics(orig);
                        // check for constrains. if something goes wrong - it will error inside the function
                        CheckIfTheTypesAreAllowedForConstrains(orig, idExprG.GenericRealTypes);
                    }
                }
            }

            // remove all from parent stack
            foreach (var g in (decl.Name as AstIdGenericExpr).GenericRealTypes)
            {
                var originalGeneric = g;
                var currGeneric = (originalGeneric as AstNestedExpr).RightPart as AstIdExpr;
                if (currGeneric == null) Debug.Assert(false, "Should not happen");
                // append to parent stack
                _currentParentStack.RemoveCurrentGenericIdMapping(currGeneric.Name);
            }

            // add here
            _allPureGenericTypes.Add(decl);
            return true;
        }

        /// <summary>
        /// 'true' if there are constrains like:
        /// where T: ICringe<U>
        /// </summary>
        /// <param name="constrains"></param>
        /// <returns></returns>
        private bool HasGenericConstrains(List<AstConstrainStmt> constrains)
        {
            return constrains.Any(x => x.ConstrainType == GenericConstrainType.CustomType && (x.Expr.UnrollToRightPart<AstIdExpr>() is AstIdGenericExpr));
        }

        /// <summary>
        /// Returns enumerable of generics that has or has not generic constrains in them
        /// </summary>
        /// <param name="gens"></param>
        /// <param name="decl"></param>
        /// <param name="needGeneric"></param>
        /// <returns></returns>
        private IEnumerable<(AstExpression, List<AstConstrainStmt>)> GetFilteredConstrains(List<AstExpression> gens, AstDeclaration decl, bool needGeneric = false)
        {
            foreach (var x in gens)
            {
                if (x is not AstNestedExpr nst22 || nst22.RightPart is not AstIdExpr idE)
                    continue;
                var currContrains = decl.GenericConstrains.FirstOrDefault(x => x.Key.Name == idE.Name).Value;
                currContrains ??= new List<AstConstrainStmt>();
                if (needGeneric)
                {
                    if (HasGenericConstrains(currContrains))
                    {
                        yield return (x, currContrains);
                    }
                }
                else if (!HasGenericConstrains(currContrains))
                    yield return (x, currContrains);
            }
        }
    }
}
