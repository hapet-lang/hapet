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
                CheckUsedDeclsDecl(f, depDecls, true);

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

                    _postPreparer._currentSourceFile = d.SourceFile;
                    // creating stor call ast
                    string funcName = $"{d.Name.Name.GetClassNameWithoutNamespace()}_stor";
                    // no need for namespace if the decl is nested
                    var call = new AstCallExpr(new AstNestedExpr(d.Name.GetCopy(d.IsNestedDecl ? "" : d.NameWithNs), null), new AstIdExpr(funcName));
                    _postPreparer.SetScopeAndParent(call, blockWhereToCall, blockWhereToCall.SubScope);
                    _postPreparer.PostPrepareExprScoping(call);
                    // allow stor calls from here
                    var saved = inInfo.AllowAccessToEveryShite;
                    inInfo.AllowAccessToEveryShite = true;
                    _postPreparer.PostPrepareExprInference(call, inInfo, ref outInfo);
                    inInfo.AllowAccessToEveryShite = saved;
                    storCalls.Add(call);
                }

                // adding these calls after stor_var assign
                blockWhereToCall.Statements.InsertRange(1, storCalls);
            }
        }
    }
}
