using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Scoping;
using System;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        // TODO: refactor
        private bool PostPrepareMetadataGenerics(AstStatement stmt)
        {
            if (stmt is AstClassDecl cls)
            {
                // we need only generics
                if (!cls.HasGenericTypes)
                    return false;

                // has generic types but already is an implementation 
                if (cls.IsImplOfGeneric)
                    return false;

                List<AstClassDecl> virtualTypes = new List<AstClassDecl>();
                // making virtual types for generics like T
                foreach (var t in cls.GenericNames)
                {
                    // getting constains for the generic type
                    List<AstNestedExpr> constrains = cls.GenericConstrains.TryGetValue(t, out var val) ? val : new List<AstNestedExpr>();

                    // we need to create a temp class declaration 
                    // and define it inside class scope
                    var vt = CreateTypeDeclarationForGeneric(cls, t, constrains);
                    virtualTypes.Add(vt);
                }

                // making a class like List<T> where T is a virtual type
                var nestedList = virtualTypes.Select(x => new AstNestedExpr(x.Name, null, x.Name)).ToList();
                string realName = GetGenericRealName(cls, nestedList);
                var realCls = GetRealTypeFromGeneric(cls, nestedList, realName);

                // define it in the same scope
                var realDclDecl = new DeclSymbol(realName, realCls);
                cls.Scope.DefineSymbol(realDclDecl);

                // remove from inferencing
                AllClassesMetadata.Remove(cls);

                return true;
            }
            else if (stmt is AstFuncDecl func)
            {
                // we need only generics
                if (!func.HasGenericTypes)
                    return false;

                // has generic types but already is an implementation 
                if (func.IsImplOfGeneric)
                    return false;

                List<AstClassDecl> virtualTypes = new List<AstClassDecl>();
                // making virtual types for generics like T
                foreach (var t in func.GenericNames)
                {
                    // getting constains for the generic type
                    List<AstNestedExpr> constrains = func.GenericConstrains.TryGetValue(t, out var val) ? val : new List<AstNestedExpr>();

                    // we need to create a temp class declaration 
                    // and define it inside class scope
                    var vt = CreateTypeDeclarationForGeneric(func, t, constrains);
                    virtualTypes.Add(vt);
                }

                // making a class like List<T> where T is a virtual type
                var nestedList = virtualTypes.Select(x => new AstNestedExpr(x.Name, null, x.Name)).ToList();
                var realName = GetGenericRealName(func, nestedList);
                var realFunc = GetRealTypeFromGeneric(func, nestedList, realName);

                // remove from inferencing
                AllFunctionsMetadata.Remove(func);

                return true;
            }

            return false;
        }
    }
}
