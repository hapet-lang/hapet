using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;

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
                // skip static lambdas and nested
                if ((d is AstLambdaExpr l && l.SpecialKeys.Contains(TokenType.KwStatic)) ||
                    (d is AstFuncDecl f && f.SpecialKeys.Contains(TokenType.KwStatic)))
                    continue;

                // get parent function that contains the lambda or nested
                var parentFunc = d.FindContainingFunction();

                // skip pure generics
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(parentFunc))
                    continue;

                if (sorted.TryGetValue(parentFunc, out List<AstStatement> value))
                    value.Add(d);
                else
                    sorted[parentFunc] = new List<AstStatement>() { d };
            }

            int currentSyntheticClass = 0;
            foreach (var (parentFunc, decls) in sorted)
            {
                // search for used outer vars in lambda
                List<AstDeclaration> usedDecls = new List<AstDeclaration>();
                List<AstFuncDecl> functionsToGenerate = new List<AstFuncDecl>();

                // variable that will hold instance of synthetic class
                AstNestedExpr variableName = new AstNestedExpr(new AstIdExpr("__syntheticVar", parentFunc.Location), null, parentFunc.Location);

                int currentLambda = 0;
                foreach (var d in decls)
                {
                    if (d is AstFuncDecl funcDecl)
                    {
                        functionsToGenerate.Add(funcDecl.GetDeepCopy() as AstFuncDecl);

                        // remove from parent
                        parentFunc.Body.Statements.Remove(funcDecl);
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
                        var newFunc = lambdaExpr.CreateFuncDecl($"Lambda{currentLambda}");
                        functionsToGenerate.Add(newFunc);
                        // replace var usages inside the func
                        ReplaceVarUsagesInBody(newFunc.Body, depDecls, parentFunc.ContainingParent.Type.OutType);

                        // replace it with function from synthetic class
                        AstNestedExpr funcAccess = new AstNestedExpr(new AstIdExpr($"Lambda{currentLambda}", lambdaExpr.Location), 
                            variableName.GetDeepCopy() as AstNestedExpr, lambdaExpr.Location);
                        lambdaExpr.NormalParent.ReplaceChild(lambdaExpr, funcAccess);
                    }
                    currentLambda++;
                }

                // distinct and make 'this' param to be var
                usedDecls = usedDecls.Distinct().ToList();
                // search for 'this' param
                var thisParam = usedDecls.FirstOrDefault(x => x.Type.OutType == parentFunc.ContainingParent.Type.OutType && x.Name.Name == "this");
                if (thisParam != null && thisParam is AstParamDecl)
                {
                    usedDecls.Add(new AstVarDecl(
                        thisParam.Type.GetDeepCopy() as AstExpression, 
                        new AstIdExpr("__thisParam", location: thisParam.Location), 
                        location: thisParam.Location
                        ));
                    usedDecls.Remove(thisParam);
                }

                // creating a new class
                List<AstDeclaration> classDecls = new List<AstDeclaration>();
                classDecls.AddRange(usedDecls.Select(x => x.GetDeepCopy() as AstDeclaration));
                classDecls.AddRange(functionsToGenerate);
                AstClassDecl sytheticClass = new AstClassDecl(new AstIdExpr($"__SyntheticClass{currentSyntheticClass}", parentFunc.Location),
                    classDecls, location: parentFunc.Location)
                {
                    IsSyntheticStatement = true,
                };

                // suppress stor attr
                sytheticClass.Attributes.Add(new AstAttributeStmt(
                    new AstNestedExpr(new AstIdExpr("System.SuppressStaticCtorCallAttribute", sytheticClass.Location), null, sytheticClass.Location),
                    new List<AstArgumentExpr>(), sytheticClass.Location)
                {
                    IsSyntheticStatement = true,
                });

                // prepare new class
                _postPreparer.SetScopeAndParent(sytheticClass, parentFunc.ContainingParent);
                _postPreparer.PostPrepareDeclScoping(sytheticClass);
                // pp up to the current metadata step
                _postPreparer.PostPrepareStatementUpToCurrentStep(false, sytheticClass);

                currentSyntheticClass++;
            }
        }
    }
}
 