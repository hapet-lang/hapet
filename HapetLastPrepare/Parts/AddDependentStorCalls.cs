using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Extensions;
using HapetPostPrepare.Entities;

namespace HapetLastPrepare
{
    public partial class LastPrepare
    {
        private void AddDependentStorCalls()
        {
            // go over all stors and add dependent stor calls in them
            foreach (var f in _postPreparer.AllFunctionsMetadata.Where(x => x.ClassFunctionType == HapetFrontend.Enums.ClassFunctionType.StaticCtor && !x.IsImported))
            {
                // skip cringe shite
                if (_postPreparer.IsDeclShouldBeSkippedFromStorManipulations(f.ContainingParent))
                    continue;

                List<AstDeclaration> depDecls = new List<AstDeclaration>();
                CheckUsedDeclsDecl(f, depDecls);

                // where to call
                var blockWhereToCall = (f.Body.Statements[0] as AstIfStmt).BodyTrue;

                // making list off all calls
                List<AstCallExpr> storCalls = new List<AstCallExpr>();
                foreach (var d in depDecls.Distinct().Where(x => x is AstClassDecl || x is AstStructDecl))
                {
                    // skip cringe shite
                    if (_postPreparer.IsDeclShouldBeSkippedFromStorManipulations(d))
                        continue;

                    // just handles
                    HapetPostPrepare.Entities.InInfo inInfo = HapetPostPrepare.Entities.InInfo.Default;
                    HapetPostPrepare.Entities.OutInfo outInfo = HapetPostPrepare.Entities.OutInfo.Default;

                    // creating stor call ast
                    string funcName = $"{d.Name.Name.GetClassNameWithoutNamespace()}_stor";
                    var call = new AstCallExpr(new AstNestedExpr(d.Name.GetCopy(), null), new AstIdExpr(funcName));
                    _postPreparer.SetScopeAndParent(call, blockWhereToCall, blockWhereToCall.SubScope);
                    _postPreparer.PostPrepareExprScoping(call);
                    _postPreparer.PostPrepareExprInference(call, inInfo, ref outInfo);
                    storCalls.Add(call);
                }

                // adding these calls after stor_var assign
                blockWhereToCall.Statements.InsertRange(1, storCalls);
            }
        }
    }
}
