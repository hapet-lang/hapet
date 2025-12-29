using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
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
                    currentLambda++;
                }

                // creating a new class
                List<AstDeclaration> classDecls = new List<AstDeclaration>();
                classDecls.AddRange(usedDecls.Select(x => x.GetDeepCopy() as AstDeclaration));
                classDecls.AddRange(functionsToGenerate);
                AstClassDecl sytheticClass = new AstClassDecl(new AstIdExpr($"__SyntheticClass{currentSyntheticClass}", parentFunc.Location), 
                    classDecls, location: parentFunc.Location);

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
 