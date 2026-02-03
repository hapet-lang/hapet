using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetPostPrepare.Entities;

namespace HapetLastPrepare
{
    public partial class LastPrepare
    {
        private void HandleLambdas()
        {
            // TODO: handle here non-static nested/lambdas

            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

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
                AstIdExpr variableName = new AstIdExpr("__syntheticVar", parentFunc.Location);
                AstNestedExpr variableNameNested = (variableName.GetDeepCopy() as AstIdExpr).WrapToNested();

                int currentLambda = 0;
                foreach (var d in decls)
                {
                    if (d is AstFuncDecl funcDecl)
                    {
                        functionsToGenerate.Add(funcDecl.GetDeepCopy() as AstFuncDecl);

                        // TODO: replace usages in parent func

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
                            variableNameNested.GetDeepCopy() as AstNestedExpr, lambdaExpr.Location);
                        lambdaExpr.NormalParent.ReplaceChild(lambdaExpr, funcAccess);
                    }
                    currentLambda++;
                }

                // distinct and 
                usedDecls = usedDecls.Distinct().ToList();
                List<AstDeclaration> declsToAddToSynthetic = new List<AstDeclaration>(usedDecls);
                // replace all params with field decls
                foreach (var d in declsToAddToSynthetic.Where(x => x is AstParamDecl).ToList())
                {
                    string varName = d.Name.Name;

                    // make 'this' param to be var
                    if (d.Type.OutType == parentFunc.ContainingParent.Type.OutType && d.Name.Name == "this")
                        varName = "__thisParam";

                    declsToAddToSynthetic.Add(new AstVarDecl(
                        d.Type.GetDeepCopy() as AstExpression,
                        new AstIdExpr(varName, location: d.Name.Location),
                        location: d.Location
                        ));
                    declsToAddToSynthetic.Remove(d);
                }

                // creating a new class
                List<AstDeclaration> classDecls = new List<AstDeclaration>();
                classDecls.AddRange(declsToAddToSynthetic.Select(x => x.GetDeepCopy() as AstDeclaration));
                classDecls.AddRange(functionsToGenerate);
                AstClassDecl sytheticClass = new AstClassDecl(new AstIdExpr($"__SyntheticClass{currentSyntheticClass}", parentFunc.Location),
                    classDecls, location: parentFunc.Location)
                {
                    IsSyntheticStatement = true,
                };
                // set containing parent
                foreach (var decl in classDecls)
                    decl.ContainingParent = sytheticClass;

                // suppress stor attr
                sytheticClass.Attributes.Add(new AstAttributeStmt(
                    new AstNestedExpr(new AstIdExpr("System.SuppressStaticCtorCallAttribute", sytheticClass.Location), null, sytheticClass.Location),
                    new List<AstArgumentExpr>(), sytheticClass.Location)
                {
                    IsSyntheticStatement = true,
                });

                // prepare new class
                _postPreparer.PostPrepareDeclMethodsInternal(sytheticClass, parentFunc.ContainingParent.SourceFile);
                _postPreparer.SetScopeAndParent(sytheticClass, parentFunc.ContainingParent);
                _postPreparer.PostPrepareDeclScoping(sytheticClass);
                // pp up to the current metadata step
                _postPreparer.PostPrepareStatementUpToCurrentStep(false, sytheticClass);
                _postPreparer.PostPrepareInheritedShiteOnDecl(sytheticClass);

                // create instance of the synthetic class in parent func
                AstNewExpr instanceCreation = new AstNewExpr(
                    (sytheticClass.Name.GetDeepCopy() as AstIdExpr).WrapToNested(), 
                    location: sytheticClass.Location);
                AstVarDecl syntheticVar = new AstVarDecl(
                    (sytheticClass.Name.GetDeepCopy() as AstIdExpr).WrapToNested(),
                    variableName.GetDeepCopy() as AstIdExpr,
                    ini: instanceCreation,
                    location: sytheticClass.Location);
                // add the var to parent func block
                parentFunc.Body.Statements.Insert(0, syntheticVar);

                // generate statements to wrap params usages
                List<AstStatement> paramInitializations = new List<AstStatement>();
                foreach (var d in usedDecls.Where(x => x is AstParamDecl))
                {
                    string targetFieldName = d.Name.Name;

                    // add 'this' init if used
                    if (d.Type.OutType == parentFunc.ContainingParent.Type.OutType && d.Name.Name == "this")
                        targetFieldName = "__thisParam";

                    AstNestedExpr assignTarget = (variableName.GetDeepCopy() as AstIdExpr).WrapToNested();
                    assignTarget = new AstNestedExpr(new AstIdExpr(targetFieldName, location: assignTarget.Location), assignTarget, assignTarget.Location);
                    AstNestedExpr assignValue = new AstNestedExpr(d.Name.GetDeepCopy() as AstIdExpr, null, assignTarget.Location);
                    paramInitializations.Add(new AstAssignStmt(assignTarget, assignValue, location: assignTarget.Location));
                }
                // add inits to parent func block
                parentFunc.Body.Statements.InsertRange(1, paramInitializations);

                // replace local var usages
                ReplaceVarUsagesInParent(parentFunc.Body, usedDecls, variableName);
                // replace local var decls
                ReplaceVarDeclsInParent(parentFunc.Body, usedDecls, variableName);

                // reinference parent func body
                _postPreparer.PostPrepareFunctionScoping(parentFunc);
                // pp up to the current metadata step
                var saved = inInfo.ForMetadata;
                inInfo.ForMetadata = false;
                _postPreparer.PostPrepareFunctionInference(parentFunc, inInfo, ref outInfo);
                inInfo.ForMetadata = saved;


                currentSyntheticClass++;
            }
        }
    }
}
 