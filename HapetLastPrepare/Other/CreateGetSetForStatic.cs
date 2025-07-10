using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
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
                    new AstReturnStmt(varDecl.Name.GetCopy(), varDecl.Name.Location)
                }, varDecl.Name.Location), new AstIdExpr($"get_{varDecl.Name.Name}", varDecl.Name.Location), location: varDecl.Name.Location);

            fGet.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwStatic, varDecl.Location.Beginning));
            if (SpecialKeysHelper.HasSpecialKeyType(varDecl, 1, out int ind))
                fGet.SpecialKeys.Add(varDecl.SpecialKeys[ind]);
            fGet.IsImported = varDecl.IsImported;
            fGet.IsDeclarationUsed = true;
            fGet.IsStaticVarFunction = true;
            fGet.ContainingParent = varDecl.ContainingParent;

            varDecl.ContainingParent.GetDeclarations().Add(fGet);
            _postPreparer.SetScopeAndParent(fGet, varDecl);
            _postPreparer.PostPrepareDeclScoping(fGet);
            _postPreparer.PostPrepareStatementUpToCurrentStep(fGet);

            AstFuncDecl fSet = null;
            // generate set func only for static - not const
            if (varDecl.SpecialKeys.Contains(TokenType.KwStatic))
            {
                fSet = new AstFuncDecl(new List<AstParamDecl>()
                {
                    new AstParamDecl(varDecl.Type.GetDeepCopy() as AstExpression, new AstIdExpr("value", varDecl.Name.Location), location: varDecl.Name.Location)
                },
                new AstNestedExpr(new AstIdExpr("void", varDecl.Name.Location), null, varDecl.Name.Location),
                new AstBlockExpr(new List<AstStatement>()
                {
                    new AstAssignStmt(new AstNestedExpr(varDecl.Name.GetCopy(), null, varDecl.Name.Location), new AstIdExpr("value", varDecl.Name.Location), varDecl.Name.Location),
                    new AstReturnStmt(null, varDecl.Name.Location)
                }, varDecl.Name.Location), new AstIdExpr($"set_{varDecl.Name.Name}", varDecl.Name.Location), location: varDecl.Name.Location);

                fSet.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwStatic, varDecl.Location.Beginning));
                if (SpecialKeysHelper.HasSpecialKeyType(varDecl, 1, out int ind2))
                    fSet.SpecialKeys.Add(varDecl.SpecialKeys[ind2]);
                fSet.IsImported = varDecl.IsImported;
                fSet.IsDeclarationUsed = true;
                fSet.IsStaticVarFunction = true;
                fSet.ContainingParent = varDecl.ContainingParent;

                varDecl.ContainingParent.GetDeclarations().Add(fSet);
                _postPreparer.SetScopeAndParent(fSet, varDecl);
                _postPreparer.PostPrepareDeclScoping(fSet);
                _postPreparer.PostPrepareStatementUpToCurrentStep(fSet);
            }

            varDecl.GetSetMethodsForStatic = (fGet, fSet);
        }
    }
}
