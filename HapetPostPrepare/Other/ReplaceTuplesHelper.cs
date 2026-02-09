using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using System.Diagnostics;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        // make (int, string) -> ValueTuple<int, string> and so on
        public void ReplaceAllTuplesInDecls()
        {
            foreach (var (_, file) in _compiler.GetFiles())
            {
                ReplaceAllTuplesInFile(file);
            }
        }

        public void ReplaceAllTuplesInFile(ProgramFile file)
        {
            _currentSourceFile = file;

            foreach (var stmt in file.Statements)
            {
                ReplaceAllTuplesInDecl(stmt as AstDeclaration);
            }
        }

        private void ReplaceAllTuplesInDecl(AstDeclaration decl)
        {
            switch (decl)
            {
                case AstClassDecl clsDecl:
                    {
                        foreach (var inh in clsDecl.InheritedFrom)
                            ReplaceAllTuplesInStmt(inh);
                        foreach (var d in clsDecl.Declarations)
                            ReplaceAllTuplesInDecl(d);
                        break;
                    }
                case AstStructDecl strDecl:
                    {
                        foreach (var inh in strDecl.InheritedFrom)
                            ReplaceAllTuplesInStmt(inh);
                        foreach (var d in strDecl.Declarations)
                            ReplaceAllTuplesInDecl(d);
                        break;
                    }
                case AstDelegateDecl delDecl:
                    {
                        foreach (var d in delDecl.Parameters)
                            ReplaceAllTuplesInDecl(d);
                        ReplaceAllTuplesInStmt(delDecl.Returns);
                        break;
                    }
                case AstFuncDecl funcDecl:
                    {
                        foreach (var d in funcDecl.Parameters)
                            ReplaceAllTuplesInDecl(d);
                        ReplaceAllTuplesInStmt(funcDecl.Returns);

                        // body
                        if (funcDecl.Body != null)
                            foreach (var stmt in funcDecl.Body.Statements)
                                ReplaceAllTuplesInStmt(stmt);
                        break;
                    }
                case AstVarDecl varDecl:
                    {
                        ReplaceAllTuplesInStmt(varDecl.Type);
                        ReplaceAllTuplesInStmt(varDecl.Initializer);
                        break;
                    }
                case AstParamDecl parDecl:
                    {
                        ReplaceAllTuplesInStmt(parDecl.Type);
                        ReplaceAllTuplesInStmt(parDecl.DefaultValue);
                        break;
                    }
            }
        }

        private void ReplaceAllTuplesInStmt(AstStatement stmt)
        {
            if (stmt == null)
                return;

            switch (stmt)
            {
                // special case at least for 'for' loop
                // when 'for (int i = 0;...)' where 'int i' 
                // would not be handled by blockExpr
                case AstVarDecl varDecl:
                    ReplaceAllTuplesInDecl(varDecl);
                    break;
                // nested
                case AstFuncDecl fncDecl:
                    ReplaceAllTuplesInDecl(fncDecl);
                    break;

                case AstBlockExpr blockExpr:
                    ReplaceAllTuplesInBlock(blockExpr);
                    break;
                case AstUnaryExpr unExpr:
                    ReplaceAllTuplesInUnary(unExpr);
                    break;
                case AstBinaryExpr binExpr:
                    ReplaceAllTuplesInBinary(binExpr);
                    break;
                case AstPointerExpr pointerExpr:
                    ReplaceAllTuplesInPointer(pointerExpr);
                    break;
                case AstAddressOfExpr addrExpr:
                    ReplaceAllTuplesInAddressOf(addrExpr);
                    break;
                case AstNewExpr newExpr:
                    ReplaceAllTuplesInNew(newExpr);
                    break;
                case AstArgumentExpr argumentExpr:
                    ReplaceAllTuplesInArgument(argumentExpr);
                    break;
                case AstIdGenericExpr genExpr:
                    ReplaceAllTuplesInIdGeneric(genExpr);
                    break;
                case AstIdExpr idExpr:
                    ReplaceAllTuplesInId(idExpr);
                    break;
                case AstCallExpr callExpr:
                    ReplaceAllTuplesInCall(callExpr);
                    break;
                case AstCastExpr castExpr:
                    ReplaceAllTuplesInCast(castExpr);
                    break;
                case AstNestedExpr nestExpr:
                    ReplaceAllTuplesInNested(nestExpr);
                    break;
                case AstDefaultExpr defaultExpr:
                    ReplaceAllTuplesInDefault(defaultExpr);
                    break;
                case AstDefaultGenericExpr _: // no need
                    break;
                case AstEmptyStructExpr _: // no need
                    break;
                case AstArrayExpr arrayExpr:
                    ReplaceAllTuplesInArray(arrayExpr);
                    break;
                case AstArrayCreateExpr arrayCreateExpr:
                    ReplaceAllTuplesInArrayCreate(arrayCreateExpr);
                    break;
                case AstArrayAccessExpr arrayAccExpr:
                    ReplaceAllTuplesInArrayAccess(arrayAccExpr);
                    break;
                case AstTernaryExpr ternaryExpr:
                    ReplaceAllTuplesInTernary(ternaryExpr);
                    break;
                case AstCheckedExpr checkedExpr:
                    ReplaceAllTuplesInChecked(checkedExpr);
                    break;
                case AstSATOfExpr satExpr:
                    ReplaceAllTuplesInSAT(satExpr);
                    break;
                case AstEmptyExpr:
                    break;
                case AstLambdaExpr lambdaExpr:
                    ReplaceAllTuplesInLambda(lambdaExpr);
                    break;
                case AstNullableExpr nullableExpr:
                    ReplaceAllTuplesInNullable(nullableExpr);
                    break;

                // statements
                case AstAssignStmt assignStmt:
                    ReplaceAllTuplesInAssign(assignStmt);
                    break;
                case AstForStmt forStmt:
                    ReplaceAllTuplesInFor(forStmt);
                    break;
                case AstWhileStmt whileStmt:
                    ReplaceAllTuplesInWhile(whileStmt);
                    break;
                case AstDoWhileStmt doWhileStmt:
                    ReplaceAllTuplesInDoWhile(doWhileStmt);
                    break;
                case AstIfStmt ifStmt:
                    ReplaceAllTuplesInIf(ifStmt);
                    break;
                case AstSwitchStmt switchStmt:
                    ReplaceAllTuplesInSwitch(switchStmt);
                    break;
                case AstCaseStmt caseStmt:
                    ReplaceAllTuplesInCase(caseStmt);
                    break;
                case AstBreakContStmt:
                    // nothing to do
                    break;
                case AstReturnStmt returnStmt:
                    ReplaceAllTuplesInReturn(returnStmt);
                    break;
                case AstAttributeStmt attrStmt:
                    ReplaceAllTuplesInAttribute(attrStmt);
                    break;
                case AstBaseCtorStmt baseStmt:
                    ReplaceAllTuplesInBaseCtor(baseStmt);
                    break;
                case AstConstrainStmt constrainStmt:
                    ReplaceAllTuplesInConstrain(constrainStmt);
                    break;
                case AstThrowStmt throwStmt:
                    ReplaceAllTuplesInThrow(throwStmt);
                    break;
                case AstTryCatchStmt tryCatchStmt:
                    ReplaceAllTuplesInTryCatch(tryCatchStmt);
                    break;
                case AstCatchStmt catchStmt:
                    ReplaceAllTuplesInCatch(catchStmt);
                    break;
                case AstGotoStmt:
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
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, stmt, [stmt.AAAName], ErrorCode.Get(CTEN.StmtNotImplemented));
                        break;
                    }
            }
        }

        private void ReplaceAllTuplesInBlock(AstBlockExpr expr)
        {
            foreach (var s in expr.Statements)
                ReplaceAllTuplesInStmt(s);
        }

        private void ReplaceAllTuplesInUnary(AstUnaryExpr expr)
        {
            ReplaceAllTuplesInStmt(expr.SubExpr);
        }

        private void ReplaceAllTuplesInBinary(AstBinaryExpr expr)
        {
            ReplaceAllTuplesInStmt(expr.Left);
            ReplaceAllTuplesInStmt(expr.Right);
        }

        private void ReplaceAllTuplesInPointer(AstPointerExpr expr)
        {
            ReplaceAllTuplesInStmt(expr.SubExpression);
        }

        private void ReplaceAllTuplesInAddressOf(AstAddressOfExpr expr)
        {
            ReplaceAllTuplesInStmt(expr.SubExpression);
        }

        private void ReplaceAllTuplesInNew(AstNewExpr expr)
        {
            ReplaceAllTuplesInStmt(expr.TypeName);
            foreach (var a in expr.Arguments)
                ReplaceAllTuplesInStmt(a);
        }

        private void ReplaceAllTuplesInArgument(AstArgumentExpr expr)
        {
            ReplaceAllTuplesInStmt(expr.Expr);
        }

        private void ReplaceAllTuplesInIdGeneric(AstIdGenericExpr expr)
        {
            foreach (var a in expr.GenericRealTypes)
                ReplaceAllTuplesInStmt(a);
        }

        private void ReplaceAllTuplesInId(AstIdExpr expr)
        {
            ReplaceAllTuplesInStmt(expr.AdditionalData);
        }

        private void ReplaceAllTuplesInCall(AstCallExpr expr)
        {
            ReplaceAllTuplesInStmt(expr.FuncName);
            ReplaceAllTuplesInStmt(expr.TypeOrObjectName);
            foreach (var a in expr.Arguments)
                ReplaceAllTuplesInStmt(a);
        }

        private void ReplaceAllTuplesInCast(AstCastExpr expr)
        {
            ReplaceAllTuplesInStmt(expr.TypeExpr);
            ReplaceAllTuplesInStmt(expr.SubExpression);
        }

        private void ReplaceAllTuplesInNested(AstNestedExpr expr)
        {
            ReplaceAllTuplesInStmt(expr.LeftPart);

            if (expr.RightPart is AstTupleExpr tuple)
            {
                Debug.Assert(expr.LeftPart == null); // has to be null when tuple

                foreach (var t in tuple.Elements)
                    ReplaceAllTuplesInStmt(t);

                if (tuple.IsTypedTuple)
                {
                    var tpG = new AstIdGenericExpr("ValueTuple", tuple.Elements, tuple.Location);
                    var tp = new AstNestedExpr(new AstIdExpr("System", tuple.Location), null, tuple.Location);
                    expr.LeftPart = tp;
                    expr.RightPart = tpG;

                    expr.RightPart.TupleNameList = tuple.Names;
                    expr.TupleNameList = tuple.Names;
                }
                else
                {
                    var tpG = new AstIdGenericExpr("ValueTuple", new List<AstExpression>(), tuple.Location);
                    var tp = new AstNestedExpr(tpG, new AstNestedExpr(new AstIdExpr("System", tuple.Location), null, tuple.Location), tuple.Location);
                    var args = tuple.Elements.Select(x => new AstArgumentExpr(x, location: x.Location)).ToList();
                    var newTuple = new AstNewExpr(tp, args, tuple.Location) { IsTupleCreation = true };
                    expr.RightPart = newTuple;

                    expr.RightPart.TupleNameList = tuple.Names;
                    expr.TupleNameList = tuple.Names;
                }
            }
            else
            {
                ReplaceAllTuplesInStmt(expr.RightPart);
            }
        }

        private void ReplaceAllTuplesInDefault(AstDefaultExpr expr)
        {
            ReplaceAllTuplesInStmt(expr.TypeForDefault);
        }

        private void ReplaceAllTuplesInArray(AstArrayExpr expr)
        {
            ReplaceAllTuplesInStmt(expr.SubExpression);
        }

        private void ReplaceAllTuplesInArrayCreate(AstArrayCreateExpr expr)
        {
            ReplaceAllTuplesInStmt(expr.TypeName);
            foreach (var a in expr.Elements)
                ReplaceAllTuplesInStmt(a);
            foreach (var a in expr.SizeExprs)
                ReplaceAllTuplesInStmt(a);
        }

        private void ReplaceAllTuplesInArrayAccess(AstArrayAccessExpr expr)
        {
            ReplaceAllTuplesInStmt(expr.ObjectName);
            ReplaceAllTuplesInStmt(expr.ParameterExpr);
        }

        private void ReplaceAllTuplesInTernary(AstTernaryExpr expr)
        {
            ReplaceAllTuplesInStmt(expr.Condition);
            ReplaceAllTuplesInStmt(expr.TrueExpr);
            ReplaceAllTuplesInStmt(expr.FalseExpr);
        }

        private void ReplaceAllTuplesInChecked(AstCheckedExpr expr)
        {
            if (expr.IsStatement)
                ReplaceAllTuplesInStmt(expr.Body);
            else
                ReplaceAllTuplesInStmt(expr.SubExpression);
        }

        private void ReplaceAllTuplesInSAT(AstSATOfExpr expr)
        {
            ReplaceAllTuplesInStmt(expr.TargetType);
        }

        private void ReplaceAllTuplesInLambda(AstLambdaExpr expr)
        {
            foreach (var d in expr.Parameters)
                ReplaceAllTuplesInDecl(d);
            ReplaceAllTuplesInStmt(expr.Returns);

            // body
            if (expr.Body != null)
                foreach (var stmt in expr.Body.Statements)
                    ReplaceAllTuplesInStmt(stmt);
        }

        private void ReplaceAllTuplesInNullable(AstNullableExpr expr)
        {
            ReplaceAllTuplesInStmt(expr.SubExpression);
        }


        private void ReplaceAllTuplesInAssign(AstAssignStmt expr)
        {
            ReplaceAllTuplesInStmt(expr.Target);
            ReplaceAllTuplesInStmt(expr.Value);
        }

        private void ReplaceAllTuplesInFor(AstForStmt expr)
        {
            ReplaceAllTuplesInStmt(expr.FirstArgument);
            ReplaceAllTuplesInStmt(expr.SecondArgument);
            ReplaceAllTuplesInStmt(expr.ThirdArgument);
            ReplaceAllTuplesInStmt(expr.Body);
            ReplaceAllTuplesInStmt(expr.ForeachArgument);
        }

        private void ReplaceAllTuplesInWhile(AstWhileStmt expr)
        {
            ReplaceAllTuplesInStmt(expr.Condition);
            ReplaceAllTuplesInStmt(expr.Body);
        }

        private void ReplaceAllTuplesInDoWhile(AstDoWhileStmt expr)
        {
            ReplaceAllTuplesInStmt(expr.Condition);
            ReplaceAllTuplesInStmt(expr.Body);
        }

        private void ReplaceAllTuplesInIf(AstIfStmt expr)
        {
            ReplaceAllTuplesInStmt(expr.Condition);
            ReplaceAllTuplesInStmt(expr.BodyTrue);
            ReplaceAllTuplesInStmt(expr.BodyFalse);
        }

        private void ReplaceAllTuplesInSwitch(AstSwitchStmt expr)
        {
            ReplaceAllTuplesInStmt(expr.SubExpression);
            foreach (var a in expr.Cases)
                ReplaceAllTuplesInStmt(a);
        }

        private void ReplaceAllTuplesInCase(AstCaseStmt expr)
        {
            ReplaceAllTuplesInStmt(expr.Pattern);
            ReplaceAllTuplesInStmt(expr.Body);
        }

        private void ReplaceAllTuplesInReturn(AstReturnStmt expr)
        {
            ReplaceAllTuplesInStmt(expr.ReturnExpression);
            ReplaceAllTuplesInStmt(expr.WeakReturnStatement);
        }

        private void ReplaceAllTuplesInAttribute(AstAttributeStmt expr)
        {
            foreach (var a in expr.Arguments)
                ReplaceAllTuplesInStmt(a);
        }

        private void ReplaceAllTuplesInBaseCtor(AstBaseCtorStmt expr)
        {
            foreach (var a in expr.Arguments)
                ReplaceAllTuplesInStmt(a);
        }

        private void ReplaceAllTuplesInConstrain(AstConstrainStmt expr)
        {
            // probably nothing to do here
        }

        private void ReplaceAllTuplesInThrow(AstThrowStmt stmt)
        {
            ReplaceAllTuplesInStmt(stmt.ThrowExpression);
        }

        private void ReplaceAllTuplesInTryCatch(AstTryCatchStmt stmt)
        {
            ReplaceAllTuplesInStmt(stmt.TryBlock);
            ReplaceAllTuplesInStmt(stmt.FinallyBlock);
            foreach (var c in stmt.CatchBlocks)
                ReplaceAllTuplesInStmt(c);
        }

        private void ReplaceAllTuplesInCatch(AstCatchStmt stmt)
        {
            ReplaceAllTuplesInStmt(stmt.CatchBlock);
            ReplaceAllTuplesInDecl(stmt.CatchParam);
        }
    }
}
