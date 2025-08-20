using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Ast;
using HapetFrontend.Parsing;
using HapetFrontend.Types;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
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
                body.Statements.Add(new AstReturnStmt(new AstNestedExpr(call, null)));

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

        private void AddInvokeDeclarationToEvent(AstClassDecl cls, bool declareOnly)
        {
            // TODO: WARN: skip for now - probably no need to skip generic events ?
            if (declareOnly)
                return;
            if (cls.Name is not AstIdGenericExpr genId)
                return;
            if (genId.GenericRealTypes.Count != 1 || genId.GenericRealTypes[0].OutType is not DelegateType dT)
                return;

            var del = dT.TargetDeclaration;
            List<AstArgumentExpr> args = new List<AstArgumentExpr>();
            foreach (var p in del.Parameters)
                args.Add(new AstArgumentExpr(p.Name.GetCopy()));

            AstBlockExpr body = new AstBlockExpr(new List<AstStatement>());
            var count = new AstVarDecl(new AstNestedExpr(new AstIdExpr("int"), null), new AstIdExpr("count"), 
                new AstNestedExpr(new AstIdExpr("Count"), new AstNestedExpr(new AstIdExpr("_delegates"), new AstNestedExpr(new AstIdExpr("this"), null))));
            body.Statements.Add(count);

            var first = new AstVarDecl(new AstNestedExpr(new AstIdExpr("int"), null), new AstIdExpr("i"), new AstNumberExpr(NumberData.FromObject(0)));
            var second = new AstBinaryExpr("<", new AstNestedExpr(new AstIdExpr("i"), null), new AstNestedExpr(new AstIdExpr("count"), null));
            var third = new AstUnaryIncDecExpr("++", new AstNestedExpr(new AstIdExpr("i"), null)) { IsPrefix = true };

            AstBlockExpr forLoopBody = new AstBlockExpr(new List<AstStatement>());
            var currDelegate = new AstVarDecl(new AstNestedExpr(new AstIdExpr("var"), null), new AstIdExpr("currDel"),
                new AstNestedExpr(new AstArrayAccessExpr(new AstNestedExpr(new AstIdExpr("_delegates"), new AstNestedExpr(new AstIdExpr("this"), null)), 
                new AstNestedExpr(new AstIdExpr("i"), null)), null));
            forLoopBody.Statements.Add(currDelegate);
            var callDelegate = new AstCallExpr(new AstNestedExpr(new AstIdExpr("currDel"), null), new AstIdExpr("Invoke"), args);
            forLoopBody.Statements.Add(callDelegate);
            AstForStmt forLoop = new AstForStmt(first, second, third, forLoopBody);
            body.Statements.Add(forLoop);

            AstFuncDecl func = new AstFuncDecl(
                del.Parameters.Select(x => x.GetDeepCopy() as AstParamDecl).ToList(),
                del.Returns.GetDeepCopy() as AstExpression,
                body,
                new AstIdExpr("Invoke"),
                "");
            func.ContainingParent = cls;
            func.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwPublic, del.Beginning));
            cls.Declarations.Add(func);

            FuncPrepareAfterAll(func, cls);
            SetScopeAndParent(func, cls, cls.SubScope);

            // pp is done in genericInference
        }
    }
}
