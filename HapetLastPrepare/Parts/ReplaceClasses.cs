using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Errors;
using HapetFrontend.Types;
using System.Runtime;
using System;
using HapetFrontend.Scoping;
using HapetFrontend.Helpers;
using System.Collections.Generic;
using HapetFrontend.Parsing;

namespace HapetLastPrepare
{
    // LPRAC - Last Prepare Replace All Classes
    public partial class LastPrepare
    {
        public void ReplaceAllClasses()
        {
            foreach (var cls in _postPreparer.AllClassesMetadata)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(cls))
                    continue;
                _postPreparer._currentSourceFile = cls.SourceFile;
                LPRACClass(cls);
            }
            foreach (var str in _postPreparer.AllStructsMetadata)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(str))
                    continue;
                _postPreparer._currentSourceFile = str.SourceFile;
                LPRACStruct(str);
            }
            foreach (var del in _postPreparer.AllDelegatesMetadata)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(del))
                    continue;
                _postPreparer._currentSourceFile = del.SourceFile;
                LPRACDelegate(del);
            }
            foreach (var func in _postPreparer.AllFunctionsMetadata)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(func))
                    continue;
                _postPreparer._currentSourceFile = func.SourceFile;
                LPRACFunction(func);
            }
        }

        private void LPRACDecl(AstDeclaration decl)
        {
            if (decl is AstClassDecl classDecl)
            {
                LPRACClass(classDecl);
            }
            else if (decl is AstStructDecl structDecl)
            {
                LPRACStruct(structDecl);
            }
            else if (decl is AstDelegateDecl delegateDecl)
            {
                LPRACDelegate(delegateDecl);
            }
            else if (decl is AstFuncDecl funcDecl)
            {
                LPRACFunction(funcDecl);
            }
            else if (decl is AstPropertyDecl)
            {
                // no need to do anything with props
                // they are not handled as props anymore here 
                // their funcs will be prepared
            }
            else if (decl is AstVarDecl varDecl)
            {
                LPRACVar(varDecl);
            }
        }

        public void LPRACClass(AstClassDecl decl)
        {
            foreach (var d in decl.Declarations)
            {
                if (d is AstFuncDecl || d.IsNestedDecl)
                {
                    // skip funcs - they are prepared in another loop
                    // skip nested - they are prepared by themselves
                    continue;
                }
                else
                {
                    LPRACDecl(d);
                }
            }
        }

        public void LPRACStruct(AstStructDecl decl)
        {
            foreach (var d in decl.Declarations)
            {
                if (d is AstFuncDecl || d.IsNestedDecl)
                {
                    // skip funcs - they are prepared in another loop
                    // skip nested - they are prepared by themselves
                    continue;
                }
                else
                {
                    LPRACDecl(d);
                }
            }
        }

        public void LPRACDelegate(AstDelegateDecl decl)
        {
            foreach (var p in decl.Parameters)
            {
                LPRACParam(p);
            }

            if (decl.Returns.OutType is ClassType)
            {
                decl.Returns = GetPointerType(decl.Returns);
            }
        }

        public void LPRACFunction(AstFuncDecl decl)
        {
            foreach (var p in decl.Parameters)
            {
                LPRACParam(p);
            }

            if (decl.Returns.OutType is ClassType)
            {
                decl.Returns = GetPointerType(decl.Returns);
            }

            if (decl.Body != null)
            {
                LPRACBlockExpr(decl.Body);
            }
        }

        public void LPRACVar(AstVarDecl decl)
        {
            if (decl.Type.OutType is ClassType)
            {
                decl.Type = GetPointerType(decl.Type);
            }

            if (decl.Initializer != null)
            {
                LPRACExpr(decl.Initializer);
            }
        }

        public void LPRACParam(AstParamDecl decl)
        {
            // no need to do anything with it
            if (decl.ParameterModificator == HapetFrontend.Enums.ParameterModificator.Arglist)
                return;

            if (decl.Type.OutType is ClassType)
            {
                decl.Type = GetPointerType(decl.Type);
            }

            if (decl.DefaultValue != null)
            {
                LPRACExpr(decl.DefaultValue);
            }
        }

        public void LPRACExpr(AstStatement stmt)
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
                    LPRACVar(varDecl);
                    break;

                case AstBlockExpr blockExpr:
                    LPRACBlockExpr(blockExpr);
                    break;
                case AstUnaryExpr unExpr:
                    LPRACUnaryExpr(unExpr);
                    break;
                case AstBinaryExpr binExpr:
                    LPRACBinaryExpr(binExpr);
                    break;
                case AstPointerExpr pointerExpr:
                    LPRACPointerExpr(pointerExpr);
                    break;
                case AstAddressOfExpr addrExpr:
                    LPRACAddressOfExpr(addrExpr);
                    break;
                case AstNewExpr newExpr:
                    LPRACNewExpr(newExpr);
                    break;
                case AstArgumentExpr argumentExpr:
                    LPRACArgumentExpr(argumentExpr);
                    break;
                case AstIdGenericExpr genExpr:
                    LPRACIdGenericExpr(genExpr);
                    break;
                case AstIdExpr idExpr:
                    LPRACIdExpr(idExpr);
                    break;
                case AstCallExpr callExpr:
                    LPRACCallExpr(callExpr);
                    break;
                case AstCastExpr castExpr:
                    LPRACCastExpr(castExpr);
                    break;
                case AstNestedExpr nestExpr:
                    LPRACNestedExpr(nestExpr);
                    break;
                case AstDefaultExpr defaultExpr:
                    LPRACDefaultExpr(defaultExpr);
                    break;
                case AstDefaultGenericExpr _: // no need
                    break;
                case AstEmptyStructExpr _: // no need
                    break;
                case AstArrayExpr arrayExpr:
                    LPRACArrayExpr(arrayExpr);
                    break;
                case AstArrayCreateExpr arrayCreateExpr:
                    LPRACArrayCreateExpr(arrayCreateExpr);
                    break;
                case AstArrayAccessExpr arrayAccExpr:
                    LPRACArrayAccessExpr(arrayAccExpr);
                    break;
                case AstTernaryExpr ternaryExpr:
                    LPRACTernaryExpr(ternaryExpr);
                    break;
                case AstCheckedExpr checkedExpr:
                    LPRACCheckedExpr(checkedExpr);
                    break;
                case AstSATOfExpr satExpr:
                    LPRACSATExpr(satExpr);
                    break;
                case AstEmptyExpr:
                    break;
                case AstLambdaExpr lambdaExpr:
                    LPRACLambdaExpr(lambdaExpr);
                    break;

                // statements
                case AstAssignStmt assignStmt:
                    LPRACAssignStmt(assignStmt);
                    break;
                case AstForStmt forStmt:
                    LPRACForStmt(forStmt);
                    break;
                case AstWhileStmt whileStmt:
                    LPRACWhileStmt(whileStmt);
                    break;
                case AstDoWhileStmt doWhileStmt:
                    LPRACDoWhileStmt(doWhileStmt);
                    break;
                case AstIfStmt ifStmt:
                    LPRACIfStmt(ifStmt);
                    break;
                case AstSwitchStmt switchStmt:
                    LPRACSwitchStmt(switchStmt);
                    break;
                case AstCaseStmt caseStmt:
                    LPRACCaseStmt(caseStmt);
                    break;
                case AstBreakContStmt:
                    // nothing to do
                    break;
                case AstReturnStmt returnStmt:
                    LPRACReturnStmt(returnStmt);
                    break;
                case AstAttributeStmt attrStmt:
                    LPRACAttributeStmt(attrStmt);
                    break;
                case AstBaseCtorStmt baseStmt:
                    LPRACBaseCtorStmt(baseStmt);
                    break;
                case AstConstrainStmt constrainStmt:
                    LPRACConstrainStmt(constrainStmt);
                    break;
                case AstThrowStmt throwStmt:
                    LPRACThrowStmt(throwStmt);
                    break;
                case AstTryCatchStmt tryCatchStmt:
                    LPRACTryCatchStmt(tryCatchStmt);
                    break;
                case AstCatchStmt catchStmt:
                    LPRACCatchStmt(catchStmt);
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
                        _compiler.MessageHandler.ReportMessage(_postPreparer._currentSourceFile.Text, stmt, [stmt.AAAName], ErrorCode.Get(CTEN.StmtNotImplemented));
                        break;
                    }
            }
        }

        private void LPRACBlockExpr(AstBlockExpr expr)
        {
            foreach (var stmt in expr.Statements)
            {
                if (stmt == null)
                    continue;

                // special check for nested function
                if (stmt is AstFuncDecl)
                {
                    // funcs are prepared in another loop
                    continue;
                }
                else
                {
                    LPRACExpr(stmt);
                }
            }
        }

        private void LPRACUnaryExpr(AstUnaryExpr expr)
        {
            LPRACExpr(expr.SubExpr);

            if (expr.OutType is ClassType)
            {
                expr.OutType = PointerType.GetPointerType(expr.OutType, true);
            }
        }

        private void LPRACBinaryExpr(AstBinaryExpr expr)
        {
            LPRACExpr(expr.Left);
            LPRACExpr(expr.Right);

            // special cringe case
            if (expr.Operator == "as" || expr.Operator == "is")
            {
                if (expr.Right.OutType is ClassType)
                    expr.Right.OutType = PointerType.GetPointerType(expr.Right.OutType, true);
            }

            if (expr.OutType is ClassType)
            {
                expr.OutType = PointerType.GetPointerType(expr.OutType, true);
            }
        }

        private void LPRACPointerExpr(AstPointerExpr expr)
        {
            LPRACExpr(expr.SubExpression);
        }

        private void LPRACAddressOfExpr(AstAddressOfExpr expr)
        {
            LPRACExpr(expr.SubExpression);
        }

        private void LPRACNewExpr(AstNewExpr expr)
        {
            if (expr.OutType is ClassType)
            {
                expr.OutType = PointerType.GetPointerType(expr.OutType, true);
            }

            foreach (var a in expr.Arguments)
            {
                LPRACArgumentExpr(a);
                a.OutType = a.Expr.OutType;
            }
        }

        private void LPRACArgumentExpr(AstArgumentExpr expr)
        {
            LPRACExpr(expr.Expr);
        }

        private void LPRACIdGenericExpr(AstIdGenericExpr expr)
        {
            for (int i = 0; i < expr.GenericRealTypes.Count; ++i)
            {
                var g = expr.GenericRealTypes[i];
                if (g.OutType is ClassType)
                {
                    expr.GenericRealTypes[i] = GetPointerType(g);
                }
            }

            expr.OutType = (expr.FindSymbol as DeclSymbol).Decl.Type.OutType;
        }

        private void LPRACIdExpr(AstIdExpr expr)
        {
            expr.OutType = (expr.FindSymbol as DeclSymbol).Decl.Type.OutType;
        }

        private void LPRACCallExpr(AstCallExpr expr)
        {
            if (expr.TypeOrObjectName?.OutType is ClassType && !expr.StaticCall)
            {
                expr.TypeOrObjectName.OutType = PointerType.GetPointerType(expr.TypeOrObjectName.OutType, true);
            }

            if (expr.OutType is ClassType)
            {
                expr.OutType = PointerType.GetPointerType(expr.OutType, true);
            }

            if (expr.TypeOrObjectName != null)
            {
                LPRACExpr(expr.TypeOrObjectName);
            }

            foreach (var a in expr.Arguments)
            {
                LPRACArgumentExpr(a);
            }
        }

        private void LPRACCastExpr(AstCastExpr expr)
        {
            if (expr.OutType is ClassType)
            {
                expr.OutType = PointerType.GetPointerType(expr.OutType, true);
            }

            LPRACExpr(expr.SubExpression);
        }

        private void LPRACNestedExpr(AstNestedExpr expr)
        {
            LPRACExpr(expr.LeftPart);
            LPRACExpr(expr.RightPart);

            expr.OutType = expr.RightPart.OutType;
        }

        private void LPRACDefaultExpr(AstDefaultExpr expr)
        {

        }

        private void LPRACArrayExpr(AstArrayExpr expr)
        {
            if (expr.SubExpression.OutType is ClassType)
            {
                expr.SubExpression = GetPointerType(expr.SubExpression);
            }

            LPRACExpr(expr.SubExpression);
        }

        private void LPRACArrayCreateExpr(AstArrayCreateExpr expr)
        {
            if (expr.TypeName.OutType is ClassType)
            {
                expr.TypeName = GetPointerType(expr.TypeName);
            }

            foreach (var s in expr.SizeExprs)
            {
                LPRACExpr(s);
            }
            foreach (var e in expr.Elements)
            {
                LPRACExpr(e);
            }
        }

        private void LPRACArrayAccessExpr(AstArrayAccessExpr expr)
        {
            LPRACExpr(expr.ParameterExpr);
            LPRACExpr(expr.ObjectName);

            if (expr.OutType is ClassType)
            {
                expr.OutType = PointerType.GetPointerType(expr.OutType, true);
            }
        }

        private void LPRACTernaryExpr(AstTernaryExpr expr)
        {
            LPRACExpr(expr.Condition);
            LPRACExpr(expr.TrueExpr);
            LPRACExpr(expr.FalseExpr);

            if (expr.OutType is ClassType)
            {
                expr.OutType = PointerType.GetPointerType(expr.OutType, true);
            }
        }

        private void LPRACCheckedExpr(AstCheckedExpr expr)
        {
            LPRACExpr(expr.SubExpression);

            if (expr.OutType is ClassType)
            {
                expr.OutType = PointerType.GetPointerType(expr.OutType, true);
            }
        }

        private void LPRACSATExpr(AstSATOfExpr expr)
        {
            if (expr.TargetType.OutType is ClassType && 
                (expr.ExprType == HapetFrontend.Parsing.TokenType.KwSizeof || expr.ExprType == HapetFrontend.Parsing.TokenType.KwAlignof))
            {
                expr.TargetType = GetPointerType(expr.TargetType);
            }
        }

        private void LPRACLambdaExpr(AstLambdaExpr expr)
        {
            foreach (var p in expr.Parameters)
            {
                LPRACParam(p);
            }

            if (expr.Returns.OutType is ClassType)
            {
                expr.Returns = GetPointerType(expr.Returns);
            }

            if (expr.Body != null)
            {
                LPRACBlockExpr(expr.Body);
            }
        }

        private void LPRACAssignStmt(AstAssignStmt stmt)
        {
            LPRACNestedExpr(stmt.Target);
            LPRACExpr(stmt.Value);

            if (stmt.Value?.OutType is ClassType)
            {
                stmt.Value.OutType = PointerType.GetPointerType(stmt.Value.OutType, true);
            }
        }

        private void LPRACForStmt(AstForStmt stmt)
        {
            LPRACExpr(stmt.FirstArgument);
            LPRACExpr(stmt.SecondArgument);
            LPRACExpr(stmt.ThirdArgument);

            LPRACBlockExpr(stmt.Body);
        }

        private void LPRACWhileStmt(AstWhileStmt stmt)
        {
            LPRACExpr(stmt.Condition);

            LPRACBlockExpr(stmt.Body);
        }

        private void LPRACDoWhileStmt(AstDoWhileStmt stmt)
        {
            LPRACExpr(stmt.Condition);

            LPRACBlockExpr(stmt.Body);
        }

        private void LPRACIfStmt(AstIfStmt stmt)
        {
            LPRACExpr(stmt.Condition);

            LPRACBlockExpr(stmt.BodyTrue);
            if (stmt.BodyFalse != null)
                LPRACBlockExpr(stmt.BodyFalse);
        }

        private void LPRACSwitchStmt(AstSwitchStmt stmt)
        {
            LPRACExpr(stmt.SubExpression);

            if (stmt.SubExpression?.OutType is ClassType)
            {
                stmt.SubExpression.OutType = PointerType.GetPointerType(stmt.SubExpression.OutType, true);
            }

            foreach (var c in stmt.Cases)
            {
                LPRACExpr(c);
            }
        }

        private void LPRACCaseStmt(AstCaseStmt stmt)
        {
            LPRACExpr(stmt.Pattern);

            if (stmt.Pattern?.OutType is ClassType)
            {
                stmt.Pattern.OutType = PointerType.GetPointerType(stmt.Pattern.OutType, true);
            }

            LPRACExpr(stmt.Body);
        }

        private void LPRACReturnStmt(AstReturnStmt stmt)
        {
            LPRACExpr(stmt.ReturnExpression);

            if (stmt.ReturnExpression?.OutType is ClassType)
            {
                stmt.ReturnExpression.OutType = PointerType.GetPointerType(stmt.ReturnExpression.OutType, true);
            }
        }

        private void LPRACAttributeStmt(AstAttributeStmt stmt)
        {

        }

        private void LPRACBaseCtorStmt(AstBaseCtorStmt stmt)
        {
            if (stmt.ThisArgument.OutType is ClassType)
            {
                stmt.ThisArgument.OutType = PointerType.GetPointerType(stmt.ThisArgument.OutType, true);
            }

            foreach (var a in stmt.Arguments)
            {
                LPRACExpr(a);
            }
        }

        private void LPRACConstrainStmt(AstConstrainStmt stmt)
        {
            LPRACExpr(stmt.Expr);
        }

        private void LPRACThrowStmt(AstThrowStmt stmt)
        {
            LPRACExpr(stmt.ThrowExpression);

            if (stmt.ThrowExpression?.OutType is ClassType)
            {
                stmt.ThrowExpression.OutType = PointerType.GetPointerType(stmt.ThrowExpression.OutType, true);
            }
        }

        private void LPRACTryCatchStmt(AstTryCatchStmt stmt)
        {
            LPRACExpr(stmt.TryBlock);
            foreach (var c in stmt.CatchBlocks)
                LPRACExpr(c);
            if (stmt.FinallyBlock != null)
                LPRACExpr(stmt.FinallyBlock);
        }

        private void LPRACCatchStmt(AstCatchStmt stmt)
        {
            LPRACParam(stmt.CatchParam);
            LPRACExpr(stmt.CatchBlock);
        }

        public AstNestedExpr GetPointerType(AstExpression expr)
        {
            // the return type is actually a pointer to the class
            var astPtr = new AstPointerExpr(expr, false, expr.Location);
            astPtr.SetDataFromStmt(expr);
            astPtr.OutType = PointerType.GetPointerType(astPtr.SubExpression.OutType, true);
            var nst = new AstNestedExpr(astPtr, null, expr.Location);
            nst.SetDataFromStmt(expr);
            nst.OutType = astPtr.OutType;
            return nst;
        }
    }
}
