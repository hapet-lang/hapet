using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Ast;
using HapetFrontend.Errors;
using HapetFrontend.Scoping;
using HapetFrontend.Types;

namespace HapetLastPrepare
{
    public partial class LastPrepare
    {
        public void CheckUsedDecls()
        {
            if (_compiler.CurrentProjectSettings.TargetFormat == HapetFrontend.TargetFormat.Library)
                return;

            CheckUsedDeclsDecl(_compiler.MainFunction);
        }

        private void CheckUsedDeclsDecl(AstDeclaration decl)
        {
            if (decl.IsDeclarationUsed)
                return;
            decl.IsDeclarationUsed = true;

            if (decl is AstClassDecl classDecl)
            {
                CheckUsedDeclsClass(classDecl);
            }
            else if (decl is AstStructDecl structDecl)
            {
                CheckUsedDeclsStruct(structDecl);
            }
            else if (decl is AstDelegateDecl delegateDecl)
            {
                CheckUsedDeclsDelegate(delegateDecl);
            }
            else if (decl is AstFuncDecl funcDecl)
            {
                CheckUsedDeclsFunction(funcDecl);
            }
            else if (decl is AstPropertyDecl propDecl)
            {
                if (propDecl.GetBlock != null)
                {
                    CheckUsedDeclsBlockExpr(propDecl.GetBlock);
                }
                if (propDecl.SetBlock != null)
                {
                    CheckUsedDeclsBlockExpr(propDecl.SetBlock);
                }

                if (propDecl is AstIndexerDecl indDecl)
                {
                    CheckUsedDeclsParam(indDecl.IndexerParameter);
                }

                CheckUsedDeclsVar(propDecl);
            }
            else if (decl is AstVarDecl varDecl)
            {
                CheckUsedDeclsVar(varDecl);
            }
            else if (decl is AstParamDecl paramDecl)
            {
                CheckUsedDeclsParam(paramDecl);
            }
        }

        public void CheckUsedDeclsClass(AstClassDecl decl)
        {
        }

        public void CheckUsedDeclsStruct(AstStructDecl decl)
        {
        }

        public void CheckUsedDeclsDelegate(AstDelegateDecl decl)
        {
            foreach (var p in decl.Parameters)
            {
                CheckUsedDeclsDecl(p);
            }
            CheckUsedDeclsExpr(decl.Returns);
        }

        public void CheckUsedDeclsFunction(AstFuncDecl decl)
        {
            foreach (var p in decl.Parameters)
            {
                CheckUsedDeclsDecl(p);
            }
            CheckUsedDeclsExpr(decl.Returns);

            if (decl.Body != null)
            {
                CheckUsedDeclsBlockExpr(decl.Body);
            }
        }

        public void CheckUsedDeclsVar(AstVarDecl decl)
        {
            CheckUsedDeclsExpr(decl.Type);

            if (decl.Initializer != null)
            {
                CheckUsedDeclsExpr(decl.Initializer);
            }
        }

        public void CheckUsedDeclsParam(AstParamDecl decl)
        {
            CheckUsedDeclsExpr(decl.Type);

            if (decl.DefaultValue != null)
            {
                CheckUsedDeclsExpr(decl.DefaultValue);
            }
        }

        public void CheckUsedDeclsExpr(AstStatement stmt)
        {
            // skip nulls
            if (stmt == null)
                return;

            switch (stmt)
            {
                // special case at least for 'for' loop
                // when 'for (int i = 0;...)' where 'int i' 
                // would not be handled by blockExpr
                case AstVarDecl varDecl:
                    CheckUsedDeclsDecl(varDecl);
                    break;

                case AstBlockExpr blockExpr:
                    CheckUsedDeclsBlockExpr(blockExpr);
                    break;
                case AstUnaryExpr unExpr:
                    CheckUsedDeclsUnaryExpr(unExpr);
                    break;
                case AstBinaryExpr binExpr:
                    CheckUsedDeclsBinaryExpr(binExpr);
                    break;
                case AstPointerExpr pointerExpr:
                    CheckUsedDeclsPointerExpr(pointerExpr);
                    break;
                case AstAddressOfExpr addrExpr:
                    CheckUsedDeclsAddressOfExpr(addrExpr);
                    break;
                case AstNewExpr newExpr:
                    CheckUsedDeclsNewExpr(newExpr);
                    break;
                case AstArgumentExpr argumentExpr:
                    CheckUsedDeclsArgumentExpr(argumentExpr);
                    break;
                case AstIdGenericExpr genExpr:
                    CheckUsedDeclsIdGenericExpr(genExpr);
                    break;
                case AstIdExpr idExpr:
                    CheckUsedDeclsIdExpr(idExpr);
                    break;
                case AstCallExpr callExpr:
                    CheckUsedDeclsCallExpr(callExpr);
                    break;
                case AstCastExpr castExpr:
                    CheckUsedDeclsCastExpr(castExpr);
                    break;
                case AstNestedExpr nestExpr:
                    CheckUsedDeclsNestedExpr(nestExpr);
                    break;
                case AstDefaultExpr defaultExpr:
                    CheckUsedDeclsDefaultExpr(defaultExpr);
                    break;
                case AstDefaultGenericExpr _: // no need
                    break;
                case AstEmptyStructExpr _: // no need
                    break;
                case AstArrayExpr arrayExpr:
                    CheckUsedDeclsArrayExpr(arrayExpr);
                    break;
                case AstArrayCreateExpr arrayCreateExpr:
                    CheckUsedDeclsArrayCreateExpr(arrayCreateExpr);
                    break;
                case AstArrayAccessExpr arrayAccExpr:
                    CheckUsedDeclsArrayAccessExpr(arrayAccExpr);
                    break;
                case AstTernaryExpr ternaryExpr:
                    CheckUsedDeclsTernaryExpr(ternaryExpr);
                    break;
                case AstCheckedExpr checkedExpr:
                    CheckUsedDeclsCheckedExpr(checkedExpr);
                    break;
                case AstSATOfExpr satExpr:
                    CheckUsedDeclsSATExpr(satExpr);
                    break;
                case AstEmptyExpr:
                    break;

                // statements
                case AstAssignStmt assignStmt:
                    CheckUsedDeclsAssignStmt(assignStmt);
                    break;
                case AstForStmt forStmt:
                    CheckUsedDeclsForStmt(forStmt);
                    break;
                case AstWhileStmt whileStmt:
                    CheckUsedDeclsWhileStmt(whileStmt);
                    break;
                case AstIfStmt ifStmt:
                    CheckUsedDeclsIfStmt(ifStmt);
                    break;
                case AstSwitchStmt switchStmt:
                    CheckUsedDeclsSwitchStmt(switchStmt);
                    break;
                case AstCaseStmt caseStmt:
                    CheckUsedDeclsCaseStmt(caseStmt);
                    break;
                case AstBreakContStmt:
                    // nothing to do
                    break;
                case AstReturnStmt returnStmt:
                    CheckUsedDeclsReturnStmt(returnStmt);
                    break;
                case AstAttributeStmt attrStmt:
                    CheckUsedDeclsAttributeStmt(attrStmt);
                    break;
                case AstBaseCtorStmt baseStmt:
                    CheckUsedDeclsBaseCtorStmt(baseStmt);
                    break;
                case AstConstrainStmt constrainStmt:
                    CheckUsedDeclsConstrainStmt(constrainStmt);
                    break;

                // skip literals
                case AstNumberExpr:
                case AstStringExpr:
                case AstBoolExpr:
                case AstCharExpr:
                case AstNullExpr:
                    break;

                default:
                    {
                        _compiler.MessageHandler.ReportMessage(_postPreparer._currentSourceFile.Text, stmt, [stmt.AAAName], ErrorCode.Get(CTEN.StmtNotImplemented));
                        break;
                    }
            }
        }

        private void CheckUsedDeclsBlockExpr(AstBlockExpr expr)
        {
            foreach (var stmt in expr.Statements)
            {
                if (stmt == null)
                    continue;
                if (stmt is AstFuncDecl)
                    continue; // skip nested funcs
                CheckUsedDeclsExpr(stmt);
            }
        }

        private void CheckUsedDeclsUnaryExpr(AstUnaryExpr expr)
        {
            CheckUsedDeclsExpr(expr.SubExpr);
        }

        private void CheckUsedDeclsBinaryExpr(AstBinaryExpr expr)
        {
            CheckUsedDeclsExpr(expr.Left);
            CheckUsedDeclsExpr(expr.Right);
        }

        private void CheckUsedDeclsPointerExpr(AstPointerExpr expr)
        {
            CheckUsedDeclsExpr(expr.SubExpression);
        }

        private void CheckUsedDeclsAddressOfExpr(AstAddressOfExpr expr)
        {
            CheckUsedDeclsExpr(expr.SubExpression);
        }

        private void CheckUsedDeclsNewExpr(AstNewExpr expr)
        {
            CheckUsedDeclsExpr(expr.TypeName);
            foreach (var a in expr.Arguments)
            {
                CheckUsedDeclsArgumentExpr(a);
            }
        }

        private void CheckUsedDeclsArgumentExpr(AstArgumentExpr expr)
        {
            CheckUsedDeclsExpr(expr.Expr);
        }

        private void CheckUsedDeclsIdGenericExpr(AstIdGenericExpr expr)
        {
            for (int i = 0; i < expr.GenericRealTypes.Count; ++i)
            {
                CheckUsedDeclsExpr(expr.GenericRealTypes[i]);
            }

            CheckUsedDeclsDecl((expr.FindSymbol as DeclSymbol).Decl);
        }

        private void CheckUsedDeclsIdExpr(AstIdExpr expr)
        {
            CheckUsedDeclsDecl((expr.FindSymbol as DeclSymbol).Decl);
        }

        private void CheckUsedDeclsCallExpr(AstCallExpr expr)
        {
            if (expr.TypeOrObjectName != null)
            {
                CheckUsedDeclsExpr(expr.TypeOrObjectName);
            }

            foreach (var a in expr.Arguments)
            {
                CheckUsedDeclsArgumentExpr(a);
            }
        }

        private void CheckUsedDeclsCastExpr(AstCastExpr expr)
        {
            CheckUsedDeclsExpr(expr.TypeExpr);
            CheckUsedDeclsExpr(expr.SubExpression);
        }

        private void CheckUsedDeclsNestedExpr(AstNestedExpr expr)
        {
            CheckUsedDeclsExpr(expr.LeftPart);
            CheckUsedDeclsExpr(expr.RightPart);
        }

        private void CheckUsedDeclsDefaultExpr(AstDefaultExpr expr)
        {
            if (expr.TypeForDefault != null)
                CheckUsedDeclsExpr(expr.TypeForDefault);
        }

        private void CheckUsedDeclsArrayExpr(AstArrayExpr expr)
        {
            CheckUsedDeclsExpr(expr.SubExpression);
        }

        private void CheckUsedDeclsArrayCreateExpr(AstArrayCreateExpr expr)
        {
            CheckUsedDeclsExpr(expr.TypeName);
            foreach (var s in expr.SizeExprs)
            {
                CheckUsedDeclsExpr(s);
            }
            foreach (var e in expr.Elements)
            {
                CheckUsedDeclsExpr(e);
            }
        }

        private void CheckUsedDeclsArrayAccessExpr(AstArrayAccessExpr expr)
        {
            CheckUsedDeclsExpr(expr.ObjectName);
            CheckUsedDeclsExpr(expr.ParameterExpr);
        }

        private void CheckUsedDeclsTernaryExpr(AstTernaryExpr expr)
        {
            CheckUsedDeclsExpr(expr.Condition);
            CheckUsedDeclsExpr(expr.TrueExpr);
            CheckUsedDeclsExpr(expr.FalseExpr);
        }

        private void CheckUsedDeclsCheckedExpr(AstCheckedExpr expr)
        {
            CheckUsedDeclsExpr(expr.SubExpression);
        }

        private void CheckUsedDeclsSATExpr(AstSATOfExpr expr)
        {
            if (expr.TargetType != null)
                CheckUsedDeclsExpr(expr.TargetType);
        }

        private void CheckUsedDeclsAssignStmt(AstAssignStmt stmt)
        {
            CheckUsedDeclsNestedExpr(stmt.Target);
            CheckUsedDeclsExpr(stmt.Value);
        }

        private void CheckUsedDeclsForStmt(AstForStmt stmt)
        {
            CheckUsedDeclsExpr(stmt.FirstArgument);
            CheckUsedDeclsExpr(stmt.SecondArgument);
            CheckUsedDeclsExpr(stmt.ThirdArgument);

            CheckUsedDeclsBlockExpr(stmt.Body);
        }

        private void CheckUsedDeclsWhileStmt(AstWhileStmt stmt)
        {
            CheckUsedDeclsExpr(stmt.Condition);

            CheckUsedDeclsBlockExpr(stmt.Body);
        }

        private void CheckUsedDeclsIfStmt(AstIfStmt stmt)
        {
            CheckUsedDeclsExpr(stmt.Condition);

            CheckUsedDeclsBlockExpr(stmt.BodyTrue);
            if (stmt.BodyFalse != null)
                CheckUsedDeclsBlockExpr(stmt.BodyFalse);
        }

        private void CheckUsedDeclsSwitchStmt(AstSwitchStmt stmt)
        {
            CheckUsedDeclsExpr(stmt.SubExpression);

            foreach (var c in stmt.Cases)
            {
                CheckUsedDeclsExpr(c);
            }
        }

        private void CheckUsedDeclsCaseStmt(AstCaseStmt stmt)
        {
            CheckUsedDeclsExpr(stmt.Pattern);

            CheckUsedDeclsExpr(stmt.Body);
        }

        private void CheckUsedDeclsReturnStmt(AstReturnStmt stmt)
        {
            CheckUsedDeclsExpr(stmt.ReturnExpression);
        }

        private void CheckUsedDeclsAttributeStmt(AstAttributeStmt stmt)
        {
            CheckUsedDeclsExpr(stmt.AttributeName);
            foreach (var s in stmt.Arguments)
                CheckUsedDeclsArgumentExpr(s);
        }

        private void CheckUsedDeclsBaseCtorStmt(AstBaseCtorStmt stmt)
        {
            CheckUsedDeclsExpr(stmt.ThisArgument);
            foreach (var a in stmt.Arguments)
            {
                CheckUsedDeclsExpr(a);
            }
        }

        private void CheckUsedDeclsConstrainStmt(AstConstrainStmt stmt)
        {
            if (stmt.Expr != null)
                CheckUsedDeclsExpr(stmt.Expr);
        }
    }
}
