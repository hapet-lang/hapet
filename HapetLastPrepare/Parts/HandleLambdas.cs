using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;

namespace HapetLastPrepare
{
    public partial class LastPrepare
    {
        private void HandleLambdas()
        {
            // TODO: handle here non-static nested/lambdas

            // at first - sort them by parent funcs
            Dictionary<AstFuncDecl, List<AstStatement>> sorted = new Dictionary<AstFuncDecl, List<AstStatement>>();
            foreach (var d in _compiler.LambdasAndNested)
            {
                // get parent function that contains the lambda or nested
                var parentFunc = d.FindContainingFunction();

                if (sorted.TryGetValue(parentFunc, out List<AstStatement> value))
                    value.Add(d);
                else
                    sorted[parentFunc] = new List<AstStatement>() { d };
            }
            foreach (var (parentFunc, decls) in sorted)
            {
                // search for used outer vars in lambda
                List<AstDeclaration> usedDecls = new List<AstDeclaration>();
                List<AstFuncDecl> functionsToGenerate = new List<AstFuncDecl>();

                int currentLambda = 0;
                foreach (var d in decls)
                {
                    if (d is AstFuncDecl funcDecl)
                    {
                        functionsToGenerate.Add(funcDecl.GetDeepCopy() as AstFuncDecl);
                    }
                    else if (d is AstLambdaExpr lambdaExpr)
                    {
                        // search all used decls
                        List<AstDeclaration> depDecls = new List<AstDeclaration>();
                        CheckUsedDeclsBlockExpr(lambdaExpr.Body, depDecls, false);
                        foreach (var decl in depDecls.Where(x => x is AstVarDecl || x is AstParamDecl))
                        {
                            // if the var is in func and NOT in lambda
                            if (!lambdaExpr.IsParentOf(decl) && parentFunc.IsParentOf(decl))
                            {
                                usedDecls.Add(decl);
                            }
                        }
                        // create a function to add to a new class
                        functionsToGenerate.Add(lambdaExpr.CreateFuncDecl($"Lambda{currentLambda}"));
                    }
                }
            }
        }
    }
}
 