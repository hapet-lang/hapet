using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Xml.Linq;
using HapetFrontend.Scoping;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareMetadataDelegates(AstStatement stmt)
        {
            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            if (stmt is AstDelegateDecl del)
            {
                // have to do it here!!!
                del.Type.OutType = DelegateType.GetDelegateType(del, del.Scope);

                PostPrepareDelegateInference(del, inInfo, ref outInfo);

                // not yet created Invokes
                if (del.Functions.Count == 0)
                {
                    var structScope = new Scope($"{del.Name.Name}_scope", del.Scope);
                    del.SubScope = structScope;

                    AddInvokeDeclarationToDelegate(del);
                }
            }
        }

        private void AddInvokeDeclarationToDelegate(AstDelegateDecl del)
        {
            List<AstArgumentExpr> args = new List<AstArgumentExpr>();
            foreach (var p in del.Parameters)
                args.Add(new AstArgumentExpr(p.Name.GetCopy()));

            AstBlockExpr body = new AstBlockExpr(new List<AstStatement>());
            AstCallExpr call = new AstCallExpr(null, new AstIdExpr("this"), args);
            if (del.Returns.OutType is VoidType)
                body.Statements.Add(call);
            else
                body.Statements.Add(new AstReturnStmt(call));

            AstFuncDecl func = new AstFuncDecl(
                del.Parameters.Select(x => x.GetDeepCopy() as AstParamDecl).ToList(),
                del.Returns.GetDeepCopy() as AstExpression,
                body,
                new AstIdExpr("Invoke"),
                "");
            func.ContainingParent = del;
            func.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwPublic, del.Beginning));
            del.Functions.Add(func);

            FuncPrepareAfterAll(func, del);
            SetScopeAndParent(func, del, del.SubScope);
            PostPrepareFunctionScoping(func);
            PostPrepareStatementUpToCurrentStep(func);
        }
    }
}
