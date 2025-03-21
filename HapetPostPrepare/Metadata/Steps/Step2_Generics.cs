using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Helpers;
using HapetFrontend.Scoping;
using System;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private bool PostPrepareMetadataGenerics(AstStatement stmt, out AstDeclaration realDeclResult)
        {
            // null by default
            realDeclResult = null;

            if (stmt is not AstDeclaration decl)
                return false;

            // we need only generics
            if (!decl.HasGenericTypes)
                return false;

            // has generic types but already is an implementation 
            if (decl.IsImplOfGeneric)
                return false;

            List<AstClassDecl> virtualTypes = new List<AstClassDecl>();
            // making virtual types for generics like T
            foreach (var t in decl.GenericNames)
            {
                // getting constains for the generic type
                List<AstNestedExpr> constrains = decl.GenericConstrains.TryGetValue(t, out var val) ? val : new List<AstNestedExpr>();

                // we need to create a temp class declaration 
                // and define it inside class scope
                var vt = CreateTypeDeclarationForGeneric(decl, t, constrains);
                virtualTypes.Add(vt);
            }

            // making a class like List<T> where T is a virtual type
            var nestedList = virtualTypes.Select(x => new AstNestedExpr(x.Name, null, x.Name)).ToList();
            string realName = GenericsHelper.GetRealFromGenericName(decl, nestedList);
            var realDecl = GetRealTypeFromGeneric(decl, nestedList, realName);
            realDeclResult = realDecl;

            return true;
        }
    }
}
