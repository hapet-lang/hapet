using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Ast;
using HapetFrontend.Errors;
using HapetFrontend.Types;
using HapetFrontend.Parsing;
using HapetFrontend.Extensions;
using HapetFrontend.Scoping;

namespace HapetLastPrepare
{
    public enum LambdaReplacerState
    {
        /// <summary>
        /// Replaces local var usages in lambdas and nested
        /// </summary>
        ReplaceVarUsagesInLambda = 0, 
        /// <summary>
        /// Replaces var decls in parent func with synthetic instance fields usages
        /// </summary>
        ReplaceVarDeclsInParent = 1,
        /// <summary>
        /// Replaces var usages in parent func with synthetic instance fields
        /// </summary>
        ReplaceVarUsagesInParent = 2,
        /// <summary>
        /// Replaces nested func usages in parent func with synthetic instance funcs
        /// </summary>
        ReplaceNestedUsagesInParent = 3,
    }

    public partial class LastPrepare
    {
        private LambdaReplacerState _currentReplacerState = LambdaReplacerState.ReplaceVarUsagesInLambda;

        private HapetType _currentReplacingParentThisType;
        private List<AstDeclaration> _currentReplacingDeclsToCheck;

        private AstIdExpr _syntheticInstanceVarName;

        private void ReplaceVarUsagesInBody(AstBlockExpr body, List<AstDeclaration> declsToCheck, HapetType currentReplacingParentThisType)
        {
            _currentReplacerState = LambdaReplacerState.ReplaceVarUsagesInLambda;

            _currentReplacingParentThisType = currentReplacingParentThisType;
            _currentReplacingDeclsToCheck = declsToCheck;
            ReplaceAllInBlockExpr(body);
        }

        private void ReplaceVarDeclsInParent(AstBlockExpr body, List<AstDeclaration> declsToCheck, AstIdExpr syntheticInstanceVarName)
        {
            _currentReplacerState = LambdaReplacerState.ReplaceVarDeclsInParent;

            _syntheticInstanceVarName = syntheticInstanceVarName;
            _currentReplacingDeclsToCheck = declsToCheck;
            ReplaceAllInBlockExpr(body);
        }

        private void ReplaceVarUsagesInParent(AstBlockExpr body, List<AstDeclaration> declsToCheck, AstIdExpr syntheticInstanceVarName)
        {
            _currentReplacerState = LambdaReplacerState.ReplaceVarUsagesInParent;

            _syntheticInstanceVarName = syntheticInstanceVarName;
            _currentReplacingDeclsToCheck = declsToCheck;
            ReplaceAllInBlockExpr(body);
        }

        private void ReplaceNestedUsagesInParent(AstBlockExpr body, AstDeclaration declToCheck, AstIdExpr syntheticInstanceVarName)
        {
            _currentReplacerState = LambdaReplacerState.ReplaceNestedUsagesInParent;

            _syntheticInstanceVarName = syntheticInstanceVarName;
            _currentReplacingDeclsToCheck = [declToCheck];
            ReplaceAllInBlockExpr(body);
        }

        private void ReplaceAllInVar(AstVarDecl varDecl)
        {
            // replacing var attrs
            foreach (var a in varDecl.Attributes)
            {
                ReplaceAllInExpr(a);
            }

            if (IsNeededToBeReplaced(varDecl.Type, out var val))
                varDecl.Type = val as AstExpression;
            else
                ReplaceAllInExpr(varDecl.Type);

            ReplaceAllInExpr(varDecl.Name);

            if (varDecl.Initializer != null)
            {
                ReplaceAllInExpr(varDecl.Initializer);
            }
        }

        private void ReplaceAllInParam(AstParamDecl paramDecl)
        {
            // replacing var attrs
            foreach (var a in paramDecl.Attributes)
            {
                ReplaceAllInExpr(a);
            }

            if (IsNeededToBeReplaced(paramDecl.Type, out var val))
                paramDecl.Type = val as AstExpression;
            else
                ReplaceAllInExpr(paramDecl.Type);

            if (paramDecl.DefaultValue != null)
            {
                ReplaceAllInExpr(paramDecl.DefaultValue);
            }
        }

        private void ReplaceAllInExpr(AstStatement expr)
        {
            if (expr == null)
                return;

            switch (expr)
            {
                // special case at least for 'for' loop
                // when 'for (int i = 0;...)' where 'int i' 
                // would not be handled by blockExpr
                case AstVarDecl varDecl:
                    ReplaceAllInVar(varDecl);
                    break;

                case AstBlockExpr blockExpr:
                    ReplaceAllInBlockExpr(blockExpr);
                    break;
                case AstUnaryExpr unExpr:
                    ReplaceAllInUnExpr(unExpr);
                    break;
                case AstBinaryExpr binExpr:
                    ReplaceAllInBinExpr(binExpr);
                    break;
                case AstPointerExpr pointerExpr:
                    ReplaceAllInPointerExpr(pointerExpr);
                    break;
                case AstAddressOfExpr addrExpr:
                    ReplaceAllInAddressOfExpr(addrExpr);
                    break;
                case AstNewExpr newExpr:
                    ReplaceAllInNewExpr(newExpr);
                    break;
                case AstArgumentExpr argumentExpr:
                    ReplaceAllInArgumentExpr(argumentExpr);
                    break;
                case AstIdGenericExpr genExpr:
                    ReplaceAllInGenIdExpr(genExpr);
                    break;
                case AstIdTupledExpr tupledExpr:
                    ReplaceAllInTupledIdExpr(tupledExpr);
                    break;
                case AstIdExpr idExpr:
                    ReplaceAllInIdExpr(idExpr);
                    break;
                case AstCallExpr callExpr:
                    ReplaceAllInCallExpr(callExpr);
                    break;
                case AstCastExpr castExpr:
                    ReplaceAllInCastExpr(castExpr);
                    break;
                case AstNestedExpr nestExpr:
                    ReplaceAllInNestedExpr(nestExpr);
                    break;
                case AstDefaultExpr defaultExpr:
                    ReplaceAllInDefaultExpr(defaultExpr);
                    break;
                case AstEmptyStructExpr: // no need
                    break;
                case AstArrayExpr arrayExpr:
                    ReplaceAllInArrayExpr(arrayExpr);
                    break;
                case AstArrayCreateExpr arrayCreateExpr:
                    ReplaceAllInArrayCreateExpr(arrayCreateExpr);
                    break;
                case AstArrayAccessExpr arrayAccExpr:
                    ReplaceAllInArrayAccessExpr(arrayAccExpr);
                    break;
                case AstTernaryExpr ternaryExpr:
                    ReplaceAllInTernaryExpr(ternaryExpr);
                    break;
                case AstCheckedExpr checkedExpr:
                    ReplaceAllInCheckedExpr(checkedExpr);
                    break;
                case AstSATOfExpr satExpr:
                    ReplaceAllInSATExpr(satExpr);
                    break;
                case AstNullableExpr nullableExpr:
                    ReplaceAllInNullableExpr(nullableExpr);
                    break;
                case AstEmptyExpr:
                    break;

                // statements
                case AstAssignStmt assignStmt:
                    ReplaceAllInAssignStmt(assignStmt);
                    break;
                case AstForStmt forStmt:
                    ReplaceAllInForStmt(forStmt);
                    break;
                case AstWhileStmt whileStmt:
                    ReplaceAllInWhileStmt(whileStmt);
                    break;
                case AstDoWhileStmt doWhileStmt:
                    ReplaceAllInDoWhileStmt(doWhileStmt);
                    break;
                case AstIfStmt ifStmt:
                    ReplaceAllInIfStmt(ifStmt);
                    break;
                case AstSwitchStmt switchStmt:
                    ReplaceAllInSwitchStmt(switchStmt);
                    break;
                case AstCaseStmt caseStmt:
                    ReplaceAllInCaseStmt(caseStmt);
                    break;
                case AstBreakContStmt:
                    // nothing to do
                    break;
                case AstReturnStmt returnStmt:
                    ReplaceAllInReturnStmt(returnStmt);
                    break;
                case AstAttributeStmt attrStmt:
                    ReplaceAllInAttributeStmt(attrStmt);
                    break;
                case AstBaseCtorStmt baseStmt:
                    ReplaceAllInBaseStmt(baseStmt);
                    break;
                case AstConstrainStmt constrainStmt:
                    ReplaceAllInConstainStmt(constrainStmt);
                    break;
                case AstThrowStmt throwStmt:
                    ReplaceAllInThrowStmt(throwStmt);
                    break;
                case AstTryCatchStmt tryCatchStmt:
                    ReplaceAllInTryCatchStmt(tryCatchStmt);
                    break;
                case AstCatchStmt сatchStmt:
                    ReplaceAllInCatchStmt(сatchStmt);
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
                        _compiler.MessageHandler.ReportMessage(_postPreparer._currentSourceFile, expr, [expr.AAAName], ErrorCode.Get(CTEN.StmtNotImplemented));
                        break;
                    }
            }
        }

        private void ReplaceAllInBlockExpr(AstBlockExpr blockExpr)
        {
            for (int i = 0; i < blockExpr.Statements.Count; ++i)
            {
                var stmt = blockExpr.Statements[i];
                if (stmt == null)
                    continue;
                // just skip them. they will be deleted
                if (stmt is AstFuncDecl)
                    continue;

                if (IsNeededToBeReplaced(stmt, out var val))
                    blockExpr.Statements[i] = val;
                else
                    ReplaceAllInExpr(stmt);
            }
        }

        private void ReplaceAllInUnExpr(AstUnaryExpr unExpr)
        {
            ReplaceAllInExpr(unExpr.SubExpr);
        }

        private void ReplaceAllInBinExpr(AstBinaryExpr binExpr)
        {
            ReplaceAllInExpr(binExpr.Left);
            ReplaceAllInExpr(binExpr.Right);
        }

        private void ReplaceAllInPointerExpr(AstPointerExpr pointerExpr)
        {
            if (IsNeededToBeReplaced(pointerExpr.SubExpression, out var val) && !pointerExpr.IsDereference)
                pointerExpr.SubExpression = val as AstExpression;
            else
                ReplaceAllInExpr(pointerExpr.SubExpression);
        }

        private void ReplaceAllInAddressOfExpr(AstAddressOfExpr addrExpr)
        {
            ReplaceAllInExpr(addrExpr.SubExpression);
        }

        private void ReplaceAllInNewExpr(AstNewExpr newExpr)
        {
            if (IsNeededToBeReplaced(newExpr.TypeName, out var val))
                newExpr.TypeName = val as AstNestedExpr;
            else
                ReplaceAllInExpr(newExpr.TypeName);

            foreach (var a in newExpr.Arguments)
            {
                ReplaceAllInExpr(a);
            }
        }

        private void ReplaceAllInArgumentExpr(AstArgumentExpr argumentExpr)
        {
            if (IsNeededToBeReplaced(argumentExpr.Expr, out var val))
                argumentExpr.Expr = val as AstExpression;
            else
                ReplaceAllInExpr(argumentExpr.Expr);

            if (argumentExpr.Name != null)
            {
                ReplaceAllInExpr(argumentExpr.Name);
            }
        }

        private void ReplaceAllInGenIdExpr(AstIdGenericExpr genExpr)
        {
            for (int i = 0; i < genExpr.GenericRealTypes.Count; ++i)
            {
                var currGt = genExpr.GenericRealTypes[i];
                if (IsNeededToBeReplaced(currGt, out var val))
                    genExpr.GenericRealTypes[i] = val as AstExpression;
                else
                    ReplaceAllInExpr(genExpr.GenericRealTypes[i]);
            }

            ReplaceAllInIdExpr(genExpr);
        }

        private void ReplaceAllInTupledIdExpr(AstIdTupledExpr tupledExpr)
        {
            throw new NotImplementedException();
        }

        private void ReplaceAllInIdExpr(AstIdExpr idExpr)
        {
            if (idExpr.AdditionalData == null)
                return;

            if (IsNeededToBeReplaced(idExpr.AdditionalData, out var val))
                idExpr.AdditionalData = val as AstNestedExpr;
            else
                ReplaceAllInExpr(idExpr.AdditionalData);
        }

        private void ReplaceAllInCallExpr(AstCallExpr callExpr)
        {
            // usually when in the same class
            if (callExpr.TypeOrObjectName != null && IsNeededToBeReplaced(callExpr.TypeOrObjectName, out var val))
                callExpr.TypeOrObjectName = val as AstExpression;
            else
                ReplaceAllInExpr(callExpr.TypeOrObjectName);

            ReplaceAllInExpr(callExpr.FuncName);
            foreach (var a in callExpr.Arguments)
            {
                ReplaceAllInExpr(a);
            }
        }

        private void ReplaceAllInCastExpr(AstCastExpr castExpr)
        {
            ReplaceAllInExpr(castExpr.SubExpression);

            // need to handle this shite here like that
            if (castExpr.TypeExpr is AstEmptyExpr)
            {
                castExpr.TypeExpr = null;
                return;
            }

            if (IsNeededToBeReplaced(castExpr.TypeExpr, out var val))
                castExpr.TypeExpr = val as AstExpression;
            else
                ReplaceAllInExpr(castExpr.TypeExpr);
        }

        private void ReplaceAllInNestedExpr(AstNestedExpr nestExpr)
        {
            if (IsNeededToBeReplaced(nestExpr.RightPart, out var val) && val is AstNestedExpr nstVal)
            {
                // we need to make replaces more carefully
                var savedLeft = nestExpr.LeftPart;
                nestExpr.RightPart = nstVal.RightPart;
                nestExpr.LeftPart = nstVal.LeftPart?.GetDeepCopy() as AstNestedExpr;
                nestExpr.LeftPart?.AddToTheEnd(savedLeft);
            }
            else
                ReplaceAllInExpr(nestExpr.RightPart);

            if (nestExpr.LeftPart != null)
            {
                if (IsNeededToBeReplaced(nestExpr.LeftPart, out var val2))
                    nestExpr.LeftPart = val2 as AstNestedExpr;
                else
                    ReplaceAllInExpr(nestExpr.LeftPart);
            }
        }

        private void ReplaceAllInDefaultExpr(AstDefaultExpr defaultExpr)
        {
            if (IsNeededToBeReplaced(defaultExpr.TypeForDefault, out var val))
                defaultExpr.TypeForDefault = val as AstExpression;
            else
                ReplaceAllInExpr(defaultExpr.TypeForDefault);
        }

        private void ReplaceAllInArrayExpr(AstArrayExpr arrayExpr)
        {
            if (IsNeededToBeReplaced(arrayExpr.SubExpression, out var val))
                arrayExpr.SubExpression = val as AstExpression;
            else
                ReplaceAllInExpr(arrayExpr.SubExpression);
        }

        private void ReplaceAllInArrayCreateExpr(AstArrayCreateExpr arrayExpr)
        {
            foreach (var sz in arrayExpr.SizeExprs)
            {
                ReplaceAllInExpr(sz);
            }

            if (IsNeededToBeReplaced(arrayExpr.TypeName, out var val))
                arrayExpr.TypeName = val as AstExpression;
            else
                ReplaceAllInExpr(arrayExpr.TypeName);

            foreach (var e in arrayExpr.Elements)
            {
                ReplaceAllInExpr(e);
            }
        }

        private void ReplaceAllInArrayAccessExpr(AstArrayAccessExpr arrayAccExpr)
        {
            ReplaceAllInExpr(arrayAccExpr.ParameterExpr);
            ReplaceAllInExpr(arrayAccExpr.ObjectName);
        }

        private void ReplaceAllInTernaryExpr(AstTernaryExpr ternaryExpr)
        {
            ReplaceAllInExpr(ternaryExpr.Condition);
            ReplaceAllInExpr(ternaryExpr.TrueExpr);
            ReplaceAllInExpr(ternaryExpr.FalseExpr);
        }

        private void ReplaceAllInCheckedExpr(AstCheckedExpr checkedExpr)
        {
            ReplaceAllInExpr(checkedExpr.SubExpression);
        }

        private void ReplaceAllInSATExpr(AstSATOfExpr satExpr)
        {
            ReplaceAllInNestedExpr(satExpr.TargetType);
        }

        private void ReplaceAllInNullableExpr(AstNullableExpr expr)
        {
            ReplaceAllInExpr(expr.SubExpression);
        }

        // statements
        private void ReplaceAllInAssignStmt(AstAssignStmt assignStmt)
        {
            ReplaceAllInExpr(assignStmt.Target);
            if (assignStmt.Value != null)
            {
                ReplaceAllInExpr(assignStmt.Value);
            }
        }

        private void ReplaceAllInForStmt(AstForStmt forStmt)
        {
            ReplaceAllInExpr(forStmt.Body);

            if (forStmt.FirstArgument != null)
            {
                ReplaceAllInExpr(forStmt.FirstArgument);
            }
            if (forStmt.SecondArgument != null)
            {
                ReplaceAllInExpr(forStmt.SecondArgument);
            }
            if (forStmt.ThirdArgument != null)
            {
                ReplaceAllInExpr(forStmt.ThirdArgument);
            }
            if (forStmt.ForeachArgument != null)
            {
                ReplaceAllInExpr(forStmt.ForeachArgument);
            }
        }

        private void ReplaceAllInWhileStmt(AstWhileStmt whileStmt)
        {
            ReplaceAllInExpr(whileStmt.Body);

            if (whileStmt.Condition != null)
            {
                ReplaceAllInExpr(whileStmt.Condition);
            }
        }

        private void ReplaceAllInDoWhileStmt(AstDoWhileStmt doWhileStmt)
        {
            ReplaceAllInExpr(doWhileStmt.Body);

            if (doWhileStmt.Condition != null)
            {
                ReplaceAllInExpr(doWhileStmt.Condition);
            }
        }

        private void ReplaceAllInIfStmt(AstIfStmt ifStmt)
        {
            ReplaceAllInExpr(ifStmt.BodyTrue);
            if (ifStmt.BodyFalse != null)
                ReplaceAllInExpr(ifStmt.BodyFalse);

            if (ifStmt.Condition != null)
            {
                ReplaceAllInExpr(ifStmt.Condition);
            }
        }

        private void ReplaceAllInSwitchStmt(AstSwitchStmt switchStmt)
        {
            ReplaceAllInExpr(switchStmt.SubExpression);

            foreach (var cc in switchStmt.Cases)
            {
                ReplaceAllInExpr(cc);
            }
        }

        private void ReplaceAllInCaseStmt(AstCaseStmt caseStmt)
        {
            if (!caseStmt.IsDefaultCase)
            {
                if (IsNeededToBeReplaced(caseStmt.Pattern, out var val))
                    caseStmt.Pattern = val as AstExpression;
                else
                    ReplaceAllInExpr(caseStmt.Pattern);
            }

            if (!caseStmt.IsFallingCase)
            {
                ReplaceAllInExpr(caseStmt.Body);
            }
        }

        private void ReplaceAllInReturnStmt(AstReturnStmt returnStmt)
        {
            if (returnStmt.ReturnExpression != null)
            {
                if (IsNeededToBeReplaced(returnStmt.ReturnExpression, out var val))
                    returnStmt.ReturnExpression = val as AstExpression;
                else
                    ReplaceAllInExpr(returnStmt.ReturnExpression);
            }
        }

        private void ReplaceAllInAttributeStmt(AstAttributeStmt attrStmt)
        {
            ReplaceAllInExpr(attrStmt.AttributeName);
            for (int i = 0; i < attrStmt.Arguments.Count; ++i)
            {
                ReplaceAllInExpr(attrStmt.Arguments[i]);
            }
        }

        private void ReplaceAllInBaseStmt(AstBaseCtorStmt baseCtor)
        {
            for (int i = 0; i < baseCtor.Arguments.Count; ++i)
            {
                ReplaceAllInExpr(baseCtor.Arguments[i]);
            }
        }

        private void ReplaceAllInConstainStmt(AstConstrainStmt stmt)
        {
            if (stmt.Expr != null)
                ReplaceAllInExpr(stmt.Expr);
            foreach (var a in stmt.AdditionalExprs)
                ReplaceAllInExpr(a);
        }

        private void ReplaceAllInThrowStmt(AstThrowStmt stmt)
        {
            if (stmt.ThrowExpression != null)
                ReplaceAllInExpr(stmt.ThrowExpression);
        }

        private void ReplaceAllInTryCatchStmt(AstTryCatchStmt stmt)
        {
            ReplaceAllInExpr(stmt.TryBlock);
            if (stmt.FinallyBlock != null)
                ReplaceAllInExpr(stmt.FinallyBlock);
            foreach (var a in stmt.CatchBlocks)
                ReplaceAllInExpr(a);
        }

        private void ReplaceAllInCatchStmt(AstCatchStmt stmt)
        {
            ReplaceAllInExpr(stmt.CatchBlock);
            if (stmt.CatchParam != null)
                ReplaceAllInParam(stmt.CatchParam);
        }

        private bool IsNeededToBeReplaced(AstStatement expr, out AstStatement value)
        {
            // if state 0
            if (_currentReplacerState == LambdaReplacerState.ReplaceVarUsagesInLambda)
            {
                // replace 'this' to '__thisParam'
                if (expr is AstIdExpr idExpr3 && idExpr3.Name == "this" && idExpr3.OutType == _currentReplacingParentThisType)
                {
                    idExpr3.Name = "__thisParam";
                    // no need to return it, just replace the string
                    value = null;
                    return false;
                }

                // need to create nested for static fields/props like 'var1' to 'SomeClass.var1'
                foreach (var v in _currentReplacingDeclsToCheck)
                {
                    if (v is not AstVarDecl currVar || !currVar.SpecialKeys.Contains(TokenType.KwStatic))
                        continue;

                    // if the is expr is the var - return nested
                    if (expr is AstIdExpr idE && idE.FindSymbol == currVar.Symbol && currVar.ContainingParent.Type.OutType == _currentReplacingParentThisType)
                    {
                        value = new AstNestedExpr(idE, new AstNestedExpr(currVar.ContainingParent.Name.GetDeepCopy() as AstIdExpr, null, expr.Location), expr.Location);
                        return true;
                    }
                }
            }
            // if state 1
            else if (_currentReplacerState == LambdaReplacerState.ReplaceVarDeclsInParent && expr is AstVarDecl varDecl)
            {
                // search for the var in used decls
                foreach (var searchV in _currentReplacingDeclsToCheck)
                {
                    if (varDecl == searchV)
                    {
                        if (varDecl.Initializer == null)
                        {
                            value = new AstEmptyStmt(varDecl.Location);
                        }
                        else
                        {
                            // set initializer to the instance field
                            var assignTarget = (_syntheticInstanceVarName.GetDeepCopy() as AstIdExpr).WrapToNested();
                            assignTarget = new AstNestedExpr(varDecl.Name.GetDeepCopy() as AstIdExpr, assignTarget, location: assignTarget.Location);
                            value = new AstAssignStmt(assignTarget, varDecl.Initializer.GetDeepCopy() as AstExpression, location: varDecl.Location);
                        }
                        return true;
                    }
                }
            }
            // if state 2
            else if (_currentReplacerState == LambdaReplacerState.ReplaceVarUsagesInParent && expr is AstIdExpr idExpr)
            {
                // search for the var in used decls
                foreach (var searchV in _currentReplacingDeclsToCheck)
                {
                    // skip 'this' param replacement
                    if (searchV is AstParamDecl paramD && paramD.Name.Name == "this")
                        continue;

                    if (idExpr.FindSymbol is DeclSymbol ds && ds.Decl == searchV)
                    {
                        value = new AstNestedExpr(idExpr, (_syntheticInstanceVarName.GetDeepCopy() as AstIdExpr).WrapToNested(), expr.Location);
                        return true;
                    }
                }
            }
            // if state 3
            else if (_currentReplacerState == LambdaReplacerState.ReplaceNestedUsagesInParent)
            {
                // search for the func in used decls
                foreach (var searchV in _currentReplacingDeclsToCheck)
                {
                    if (expr is AstIdExpr idExpr2 && idExpr2.FindSymbol is DeclSymbol ds && ds.Decl == searchV)
                    {
                        value = new AstNestedExpr(idExpr2, (_syntheticInstanceVarName.GetDeepCopy() as AstIdExpr).WrapToNested(), expr.Location);
                        return true;
                    }
                    else if (expr is AstNestedExpr nst && nst.LeftPart == null && nst.RightPart is AstCallExpr call && call.FuncName.FindSymbol is DeclSymbol ds2 && ds2.Decl == searchV)
                    {
                        if (call.TypeOrObjectName == null)
                        {
                            call.TypeOrObjectName = new AstNestedExpr((_syntheticInstanceVarName.GetDeepCopy() as AstIdExpr).WrapToNested(), null, expr.Location);
                        }
                        value = nst;
                        return true;
                    }
                }
            }

            value = null;
            return false;
        }
    }
}
