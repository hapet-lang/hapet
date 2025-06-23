using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Extensions;
using HapetFrontend.Parsing;
using HapetFrontend.Types;

namespace HapetLastPrepare
{
    public partial class LastPrepare
    {
        private void CreateGetSetForStatic(AstVarDecl varDecl)
        {
            if (varDecl.ContainingParent is not AstClassDecl && varDecl.ContainingParent is not AstStructDecl)
                return;
            if (!varDecl.SpecialKeys.Contains(TokenType.KwStatic) && !varDecl.SpecialKeys.Contains(TokenType.KwConst))
                return;
            if (varDecl.ContainingParent?.IsImplOfGeneric ?? true)
                return;
            if (varDecl is AstPropertyDecl)
                return;

            AstFuncDecl fGet = new AstFuncDecl(new List<AstParamDecl>(),
                varDecl.Type.GetDeepCopy() as AstExpression,
                new AstBlockExpr(new List<AstStatement>()
                {
                    new AstReturnStmt(varDecl.Name.GetCopy())
                }), new AstIdExpr($"{varDecl.ContainingParent.Name.Name}::{varDecl.Name.Name}_get"));

            _postPreparer.SetScopeAndParent(fGet, varDecl);
            _postPreparer.PostPrepareDeclScoping(fGet);
            _postPreparer.PostPrepareStatementUpToCurrentStep(fGet);

            AstFuncDecl fSet = new AstFuncDecl(new List<AstParamDecl>() 
                { 
                    new AstParamDecl(varDecl.Type.GetDeepCopy() as AstExpression, new AstIdExpr("value"))
                },
                new AstIdExpr("void"),
                new AstBlockExpr(new List<AstStatement>()
                {
                    new AstAssignStmt(new AstNestedExpr(varDecl.Name.GetCopy(), null), new AstIdExpr("value")),
                    new AstReturnStmt(null)
                }), new AstIdExpr($"{varDecl.ContainingParent.Name.Name}::{varDecl.Name.Name}_set"));

            _postPreparer.SetScopeAndParent(fSet, varDecl);
            _postPreparer.PostPrepareDeclScoping(fSet);
            _postPreparer.PostPrepareStatementUpToCurrentStep(fSet);
        }
    }
}
