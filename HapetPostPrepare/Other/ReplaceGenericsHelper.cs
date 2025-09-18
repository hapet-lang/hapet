using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private Dictionary<string, AstNestedExpr> _currentGenericToRealMappings = new Dictionary<string, AstNestedExpr>();

        private void MakeGenericMapping(List<AstIdExpr> generics, List<AstNestedExpr> normalTypes)
        {
            // ini the dict
            _currentGenericToRealMappings = new Dictionary<string, AstNestedExpr>();
            for (int i = 0; i < generics.Count; ++i)
            {
                _currentGenericToRealMappings.Add(generics[i].Name, normalTypes[i]);
            }
        }

        public void ReplaceAllGenericTypesInDecl(AstDeclaration decl)
        {
            if (decl is AstClassDecl clsDecl)
                ReplaceAllGenericTypesInClass(clsDecl);
            else if (decl is AstFuncDecl funcDecl)
                ReplaceAllGenericTypesInFunction(funcDecl);
            else if (decl is AstVarDecl varDecl)
                ReplaceAllGenericTypesInVar(varDecl);
            else if (decl is AstStructDecl strDecl)
                ReplaceAllGenericTypesInStruct(strDecl);
            else if (decl is AstDelegateDecl delDecl)
                ReplaceAllGenericTypesInDelegate(delDecl);
        }

        public void ReplaceAllGenericTypesInClass(AstClassDecl clsDecl)
        {
            // replacing inheritance
            for (int i = 0; i < clsDecl.InheritedFrom.Count; ++i)
            {
                var inh = clsDecl.InheritedFrom[i];
                if (IsGenericEntry(inh, out var val))
                    clsDecl.InheritedFrom[i] = val;
                else
                    ReplaceAllGenericTypesInExpr(clsDecl.InheritedFrom[i]);
            }

            // go all over the decls
            foreach (var decl in clsDecl.Declarations)
            {
                if (decl is AstFuncDecl funcDecl)
                {
                    ReplaceAllGenericTypesInFunction(funcDecl);
                }
                else if (decl is AstPropertyDecl propDecl)
                {
                    if (propDecl.GetBlock != null)
                    {
                        ReplaceAllGenericTypesInExpr(propDecl.GetBlock);
                    }
                    if (propDecl.SetBlock != null)
                    {
                        ReplaceAllGenericTypesInExpr(propDecl.SetBlock);
                    }

                    // replacing indexer parameter
                    if (propDecl is AstIndexerDecl indDecl)
                    {
                        ReplaceAllGenericTypesInParam(indDecl.IndexerParameter);
                    }

                    ReplaceAllGenericTypesInVar(propDecl);
                }
                else if (decl is AstVarDecl fieldDecl) // field 
                {
                    ReplaceAllGenericTypesInVar(fieldDecl);
                }
                else
                {
                    ReplaceAllGenericTypesInDecl(decl);
                }
            }
        }

        public void ReplaceAllGenericTypesInStruct(AstStructDecl strDecl)
        {
            // replacing inheritance
            for (int i = 0; i < strDecl.InheritedFrom.Count; ++i)
            {
                var inh = strDecl.InheritedFrom[i];
                if (IsGenericEntry(inh, out var val))
                    strDecl.InheritedFrom[i] = val;
                else
                    ReplaceAllGenericTypesInExpr(strDecl.InheritedFrom[i]);
            }

            // go all over the decls
            foreach (var decl in strDecl.Declarations)
            {
                if (decl is AstFuncDecl funcDecl)
                {
                    ReplaceAllGenericTypesInFunction(funcDecl);
                }
                else if (decl is AstPropertyDecl propDecl)
                {
                    if (propDecl.GetBlock != null)
                    {
                        ReplaceAllGenericTypesInExpr(propDecl.GetBlock);
                    }
                    if (propDecl.SetBlock != null)
                    {
                        ReplaceAllGenericTypesInExpr(propDecl.SetBlock);
                    }

                    // replacing indexer parameter
                    if (propDecl is AstIndexerDecl indDecl)
                    {
                        ReplaceAllGenericTypesInParam(indDecl.IndexerParameter);
                    }

                    ReplaceAllGenericTypesInVar(propDecl);
                }
                else if (decl is AstVarDecl fieldDecl) // field 
                {
                    ReplaceAllGenericTypesInVar(fieldDecl);
                }
                else
                {
                    ReplaceAllGenericTypesInDecl(decl);
                }
            }
        }

        private void ReplaceAllGenericTypesInFunction(AstFuncDecl funcDecl)
        {
            // replacing func attrs
            foreach (var a in funcDecl.Attributes)
            {
                ReplaceAllGenericTypesInExpr(a);
            }

            // base ctor call replacing
            if (funcDecl.BaseCtorCall != null)
            {
                ReplaceAllGenericTypesInExpr(funcDecl.BaseCtorCall);
            }

            // body replacing
            if (funcDecl.Body != null)
            {
                ReplaceAllGenericTypesInExpr(funcDecl.Body);
            }

            // replacing parameters
            foreach (var p in funcDecl.Parameters)
            {
                // settings the block scope to the parameters (so they are in the scope of the block)
                ReplaceAllGenericTypesInParam(p);

                // scoping param attrs
                foreach (var a in p.Attributes)
                {
                    ReplaceAllGenericTypesInExpr(a);
                }
            }

            ReplaceAllGenericTypesInExpr(funcDecl.Name);

            // return type replacing
            if (IsGenericEntry(funcDecl.Returns, out var val))
                funcDecl.Returns = val;
            else
                ReplaceAllGenericTypesInExpr(funcDecl.Returns);
        }

        private void ReplaceAllGenericTypesInDelegate(AstDelegateDecl delDecl)
        {
            // replacing func attrs
            foreach (var a in delDecl.Attributes)
            {
                ReplaceAllGenericTypesInExpr(a);
            }

            // replacing parameters
            foreach (var p in delDecl.Parameters)
            {
                // settings the block scope to the parameters (so they are in the scope of the block)
                ReplaceAllGenericTypesInParam(p);

                // scoping param attrs
                foreach (var a in p.Attributes)
                {
                    ReplaceAllGenericTypesInExpr(a);
                }
            }

            ReplaceAllGenericTypesInExpr(delDecl.Name);

            // return type replacing
            if (IsGenericEntry(delDecl.Returns, out var val))
                delDecl.Returns = val;
            else
                ReplaceAllGenericTypesInExpr(delDecl.Returns);
        }

        private void ReplaceAllGenericTypesInVar(AstVarDecl varDecl)
        {
            // replacing var attrs
            foreach (var a in varDecl.Attributes)
            {
                ReplaceAllGenericTypesInExpr(a);
            }

            if (IsGenericEntry(varDecl.Type, out var val))
                varDecl.Type = val;
            else
                ReplaceAllGenericTypesInExpr(varDecl.Type);

            ReplaceAllGenericTypesInExpr(varDecl.Name);

            if (varDecl.Initializer != null)
            {
                ReplaceAllGenericTypesInExpr(varDecl.Initializer);
            }
        }

        private void ReplaceAllGenericTypesInParam(AstParamDecl paramDecl)
        {
            // replacing var attrs
            foreach (var a in paramDecl.Attributes)
            {
                ReplaceAllGenericTypesInExpr(a);
            }

            if (IsGenericEntry(paramDecl.Type, out var val))
                paramDecl.Type = val;
            else
                ReplaceAllGenericTypesInExpr(paramDecl.Type);

            if (paramDecl.DefaultValue != null)
            {
                ReplaceAllGenericTypesInExpr(paramDecl.DefaultValue);
            }
        }

        private void ReplaceAllGenericTypesInExpr(AstStatement expr)
        {
            if (expr == null)
                return;

            switch (expr)
            {
                // special case at least for 'for' loop
                // when 'for (int i = 0;...)' where 'int i' 
                // would not be handled by blockExpr
                case AstVarDecl varDecl:
                    ReplaceAllGenericTypesInVar(varDecl);
                    break;

                case AstBlockExpr blockExpr:
                    ReplaceAllGenericTypesInBlockExpr(blockExpr);
                    break;
                case AstUnaryExpr unExpr:
                    ReplaceAllGenericTypesInUnExpr(unExpr);
                    break;
                case AstBinaryExpr binExpr:
                    ReplaceAllGenericTypesInBinExpr(binExpr);
                    break;
                case AstPointerExpr pointerExpr:
                    ReplaceAllGenericTypesInPointerExpr(pointerExpr);
                    break;
                case AstAddressOfExpr addrExpr:
                    ReplaceAllGenericTypesInAddressOfExpr(addrExpr);
                    break;
                case AstNewExpr newExpr:
                    ReplaceAllGenericTypesInNewExpr(newExpr);
                    break;
                case AstArgumentExpr argumentExpr:
                    ReplaceAllGenericTypesInArgumentExpr(argumentExpr);
                    break;
                case AstIdGenericExpr genExpr:
                    ReplaceAllGenericTypesInGenIdExpr(genExpr);
                    break;
                case AstIdTupledExpr tupledExpr:
                    ReplaceAllGenericTypesInTupledIdExpr(tupledExpr);
                    break;
                case AstIdExpr idExpr:
                    ReplaceAllGenericTypesInIdExpr(idExpr);
                    break;
                case AstCallExpr callExpr:
                    ReplaceAllGenericTypesInCallExpr(callExpr);
                    break;
                case AstCastExpr castExpr:
                    ReplaceAllGenericTypesInCastExpr(castExpr);
                    break;
                case AstNestedExpr nestExpr:
                    ReplaceAllGenericTypesInNestedExpr(nestExpr);
                    break;
                case AstDefaultExpr defaultExpr:
                    ReplaceAllGenericTypesInDefaultExpr(defaultExpr);
                    break;
                case AstEmptyStructExpr: // no need
                    break;
                case AstArrayExpr arrayExpr:
                    ReplaceAllGenericTypesInArrayExpr(arrayExpr);
                    break;
                case AstArrayCreateExpr arrayCreateExpr:
                    ReplaceAllGenericTypesInArrayCreateExpr(arrayCreateExpr);
                    break;
                case AstArrayAccessExpr arrayAccExpr:
                    ReplaceAllGenericTypesInArrayAccessExpr(arrayAccExpr);
                    break;
                case AstTernaryExpr ternaryExpr:
                    ReplaceAllGenericTypesInTernaryExpr(ternaryExpr);
                    break;
                case AstCheckedExpr checkedExpr:
                    ReplaceAllGenericTypesInCheckedExpr(checkedExpr);
                    break;
                case AstSATOfExpr satExpr:
                    ReplaceAllGenericTypesInSATExpr(satExpr);
                    break;
                case AstNullableExpr nullableExpr:
                    ReplaceAllGenericTypesInNullableExpr(nullableExpr);
                    break;
                case AstEmptyExpr:
                    break;

                // statements
                case AstAssignStmt assignStmt:
                    ReplaceAllGenericTypesInAssignStmt(assignStmt);
                    break;
                case AstForStmt forStmt:
                    ReplaceAllGenericTypesInForStmt(forStmt);
                    break;
                case AstWhileStmt whileStmt:
                    ReplaceAllGenericTypesInWhileStmt(whileStmt);
                    break;
                case AstDoWhileStmt doWhileStmt:
                    ReplaceAllGenericTypesInDoWhileStmt(doWhileStmt);
                    break;
                case AstIfStmt ifStmt:
                    ReplaceAllGenericTypesInIfStmt(ifStmt);
                    break;
                case AstSwitchStmt switchStmt:
                    ReplaceAllGenericTypesInSwitchStmt(switchStmt);
                    break;
                case AstCaseStmt caseStmt:
                    ReplaceAllGenericTypesInCaseStmt(caseStmt);
                    break;
                case AstBreakContStmt:
                    // nothing to do
                    break;
                case AstReturnStmt returnStmt:
                    ReplaceAllGenericTypesInReturnStmt(returnStmt);
                    break;
                case AstAttributeStmt attrStmt:
                    ReplaceAllGenericTypesInAttributeStmt(attrStmt);
                    break;
                case AstBaseCtorStmt baseStmt:
                    ReplaceAllGenericTypesInBaseStmt(baseStmt);
                    break;
                case AstConstrainStmt constrainStmt:
                    ReplaceAllGenericTypesInConstainStmt(constrainStmt);
                    break;
                case AstThrowStmt throwStmt:
                    ReplaceAllGenericTypesInThrowStmt(throwStmt);
                    break;
                case AstTryCatchStmt tryCatchStmt:
                    ReplaceAllGenericTypesInTryCatchStmt(tryCatchStmt);
                    break;
                case AstCatchStmt сatchStmt:
                    ReplaceAllGenericTypesInCatchStmt(сatchStmt);
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
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, expr, [expr.AAAName], ErrorCode.Get(CTEN.StmtNotImplemented));
                        break;
                    }
            }
        }

        private void ReplaceAllGenericTypesInBlockExpr(AstBlockExpr blockExpr)
        {
            foreach (var stmt in blockExpr.Statements)
            {
                if (stmt == null)
                    continue;

                ReplaceAllGenericTypesInExpr(stmt);
            }
        }

        private void ReplaceAllGenericTypesInUnExpr(AstUnaryExpr unExpr)
        {
            ReplaceAllGenericTypesInExpr(unExpr.SubExpr);
        }

        private void ReplaceAllGenericTypesInBinExpr(AstBinaryExpr binExpr)
        {
            ReplaceAllGenericTypesInExpr(binExpr.Left);
            ReplaceAllGenericTypesInExpr(binExpr.Right);
        }

        private void ReplaceAllGenericTypesInPointerExpr(AstPointerExpr pointerExpr)
        {
            if (IsGenericEntry(pointerExpr.SubExpression, out var val) && !pointerExpr.IsDereference)
                pointerExpr.SubExpression = val;
            else
                ReplaceAllGenericTypesInExpr(pointerExpr.SubExpression);
        }

        private void ReplaceAllGenericTypesInAddressOfExpr(AstAddressOfExpr addrExpr)
        {
            ReplaceAllGenericTypesInExpr(addrExpr.SubExpression);
        }

        private void ReplaceAllGenericTypesInNewExpr(AstNewExpr newExpr)
        {
            if (IsGenericEntry(newExpr.TypeName, out var val))
                newExpr.TypeName = val;
            else
                ReplaceAllGenericTypesInExpr(newExpr.TypeName);

            foreach (var a in newExpr.Arguments)
            {
                ReplaceAllGenericTypesInExpr(a);
            }
        }

        private void ReplaceAllGenericTypesInArgumentExpr(AstArgumentExpr argumentExpr)
        {
            if (IsGenericEntry(argumentExpr.Expr, out var val))
                argumentExpr.Expr = val;
            else
                ReplaceAllGenericTypesInExpr(argumentExpr.Expr);

            if (argumentExpr.Name != null)
            {
                ReplaceAllGenericTypesInExpr(argumentExpr.Name);
            }
        }

        private void ReplaceAllGenericTypesInGenIdExpr(AstIdGenericExpr genExpr)
        {
            for (int i = 0; i < genExpr.GenericRealTypes.Count; ++i)
            {
                var currGt = genExpr.GenericRealTypes[i];
                if (IsGenericEntry(currGt, out var val))
                    genExpr.GenericRealTypes[i] = val;
                else
                    ReplaceAllGenericTypesInExpr(genExpr.GenericRealTypes[i]);
            }

            ReplaceAllGenericTypesInIdExpr(genExpr);
        }

        private void ReplaceAllGenericTypesInTupledIdExpr(AstIdTupledExpr tupledExpr)
        {
            throw new NotImplementedException();

            ReplaceAllGenericTypesInIdExpr(tupledExpr);
        }

        private void ReplaceAllGenericTypesInIdExpr(AstIdExpr idExpr)
        {
            if (idExpr.AdditionalData == null)
                return;

            if (IsGenericEntry(idExpr.AdditionalData, out var val))
                idExpr.AdditionalData = val;
            else
                ReplaceAllGenericTypesInExpr(idExpr.AdditionalData);
        }

        private void ReplaceAllGenericTypesInCallExpr(AstCallExpr callExpr)
        {
            // usually when in the same class
            if (callExpr.TypeOrObjectName != null && IsGenericEntry(callExpr.TypeOrObjectName, out var val))
                callExpr.TypeOrObjectName = val;
            else
                ReplaceAllGenericTypesInExpr(callExpr.TypeOrObjectName);

            ReplaceAllGenericTypesInExpr(callExpr.FuncName);
            foreach (var a in callExpr.Arguments)
            {
                ReplaceAllGenericTypesInExpr(a);
            }
        }

        private void ReplaceAllGenericTypesInCastExpr(AstCastExpr castExpr)
        {
            ReplaceAllGenericTypesInExpr(castExpr.SubExpression);

            // need to handle this shite here like that
            if (castExpr.TypeExpr is AstEmptyExpr)
            {
                castExpr.TypeExpr = null;
                return;
            }

            if (IsGenericEntry(castExpr.TypeExpr, out var val))
                castExpr.TypeExpr = val;
            else
                ReplaceAllGenericTypesInExpr(castExpr.TypeExpr);
        }

        private void ReplaceAllGenericTypesInNestedExpr(AstNestedExpr nestExpr)
        {
            if (IsGenericEntry(nestExpr.RightPart, out var val))
            {
                // we need to make replaces more carefully
                var savedLeft = nestExpr.LeftPart;
                nestExpr.RightPart = val.RightPart;
                nestExpr.LeftPart = val.LeftPart?.GetDeepCopy() as AstNestedExpr;
                nestExpr.LeftPart?.AddToTheEnd(savedLeft);
            }
            else
                ReplaceAllGenericTypesInExpr(nestExpr.RightPart);

            if (nestExpr.LeftPart != null)
            {
                if (IsGenericEntry(nestExpr.LeftPart, out var val2))
                    nestExpr.LeftPart = val2;
                else
                    ReplaceAllGenericTypesInExpr(nestExpr.LeftPart);
            }
        }

        private void ReplaceAllGenericTypesInDefaultExpr(AstDefaultExpr defaultExpr)
        {
            if (IsGenericEntry(defaultExpr.TypeForDefault, out var val))
                defaultExpr.TypeForDefault = val;
            else
                ReplaceAllGenericTypesInExpr(defaultExpr.TypeForDefault);
        }

        private void ReplaceAllGenericTypesInArrayExpr(AstArrayExpr arrayExpr)
        {
            if (IsGenericEntry(arrayExpr.SubExpression, out var val))
                arrayExpr.SubExpression = val;
            else
                ReplaceAllGenericTypesInExpr(arrayExpr.SubExpression);
        }

        private void ReplaceAllGenericTypesInArrayCreateExpr(AstArrayCreateExpr arrayExpr)
        {
            foreach (var sz in arrayExpr.SizeExprs)
            {
                ReplaceAllGenericTypesInExpr(sz);
            }

            if (IsGenericEntry(arrayExpr.TypeName, out var val))
                arrayExpr.TypeName = val;
            else
                ReplaceAllGenericTypesInExpr(arrayExpr.TypeName);

            foreach (var e in arrayExpr.Elements)
            {
                ReplaceAllGenericTypesInExpr(e);
            }
        }

        private void ReplaceAllGenericTypesInArrayAccessExpr(AstArrayAccessExpr arrayAccExpr)
        {
            ReplaceAllGenericTypesInExpr(arrayAccExpr.ParameterExpr);
            ReplaceAllGenericTypesInExpr(arrayAccExpr.ObjectName);
        }

        private void ReplaceAllGenericTypesInTernaryExpr(AstTernaryExpr ternaryExpr)
        {
            ReplaceAllGenericTypesInExpr(ternaryExpr.Condition);
            ReplaceAllGenericTypesInExpr(ternaryExpr.TrueExpr);
            ReplaceAllGenericTypesInExpr(ternaryExpr.FalseExpr);
        }

        private void ReplaceAllGenericTypesInCheckedExpr(AstCheckedExpr checkedExpr) 
        {
            ReplaceAllGenericTypesInExpr(checkedExpr.SubExpression);
        }

        private void ReplaceAllGenericTypesInSATExpr(AstSATOfExpr satExpr)
        {
            ReplaceAllGenericTypesInNestedExpr(satExpr.TargetType);
        }

        private void ReplaceAllGenericTypesInNullableExpr(AstNullableExpr expr)
        {
            ReplaceAllGenericTypesInExpr(expr.SubExpression);
        }

        // statements
        private void ReplaceAllGenericTypesInAssignStmt(AstAssignStmt assignStmt)
        {
            ReplaceAllGenericTypesInExpr(assignStmt.Target);
            if (assignStmt.Value != null)
            {
                ReplaceAllGenericTypesInExpr(assignStmt.Value);
            }
        }

        private void ReplaceAllGenericTypesInForStmt(AstForStmt forStmt)
        {
            ReplaceAllGenericTypesInExpr(forStmt.Body);

            if (forStmt.FirstArgument != null)
            {
                ReplaceAllGenericTypesInExpr(forStmt.FirstArgument);
            }
            if (forStmt.SecondArgument != null)
            {
                ReplaceAllGenericTypesInExpr(forStmt.SecondArgument);
            }
            if (forStmt.ThirdArgument != null)
            {
                ReplaceAllGenericTypesInExpr(forStmt.ThirdArgument);
            }
        }

        private void ReplaceAllGenericTypesInWhileStmt(AstWhileStmt whileStmt)
        {
            ReplaceAllGenericTypesInExpr(whileStmt.Body);

            if (whileStmt.Condition != null)
            {
                ReplaceAllGenericTypesInExpr(whileStmt.Condition);
            }
        }

        private void ReplaceAllGenericTypesInDoWhileStmt(AstDoWhileStmt doWhileStmt)
        {
            ReplaceAllGenericTypesInExpr(doWhileStmt.Body);

            if (doWhileStmt.Condition != null)
            {
                ReplaceAllGenericTypesInExpr(doWhileStmt.Condition);
            }
        }

        private void ReplaceAllGenericTypesInIfStmt(AstIfStmt ifStmt)
        {
            ReplaceAllGenericTypesInExpr(ifStmt.BodyTrue);
            if (ifStmt.BodyFalse != null)
                ReplaceAllGenericTypesInExpr(ifStmt.BodyFalse);

            if (ifStmt.Condition != null)
            {
                ReplaceAllGenericTypesInExpr(ifStmt.Condition);
            }
        }

        private void ReplaceAllGenericTypesInSwitchStmt(AstSwitchStmt switchStmt)
        {
            ReplaceAllGenericTypesInExpr(switchStmt.SubExpression);

            foreach (var cc in switchStmt.Cases)
            {
                ReplaceAllGenericTypesInExpr(cc);
            }
        }

        private void ReplaceAllGenericTypesInCaseStmt(AstCaseStmt caseStmt)
        {
            if (!caseStmt.IsDefaultCase)
            {
                if (IsGenericEntry(caseStmt.Pattern, out var val))
                    caseStmt.Pattern = val;
                else
                    ReplaceAllGenericTypesInExpr(caseStmt.Pattern);
            }

            if (!caseStmt.IsFallingCase)
            {
                ReplaceAllGenericTypesInExpr(caseStmt.Body);
            }
        }

        private void ReplaceAllGenericTypesInReturnStmt(AstReturnStmt returnStmt)
        {
            if (returnStmt.ReturnExpression != null)
            {
                if (IsGenericEntry(returnStmt.ReturnExpression, out var val))
                    returnStmt.ReturnExpression = val;
                else
                    ReplaceAllGenericTypesInExpr(returnStmt.ReturnExpression);
            }
        }

        private void ReplaceAllGenericTypesInAttributeStmt(AstAttributeStmt attrStmt)
        {
            ReplaceAllGenericTypesInExpr(attrStmt.AttributeName);
            for (int i = 0; i < attrStmt.Arguments.Count; ++i)
            {
                ReplaceAllGenericTypesInExpr(attrStmt.Arguments[i]);
            }
        }

        private void ReplaceAllGenericTypesInBaseStmt(AstBaseCtorStmt baseCtor)
        {
            for (int i = 0; i < baseCtor.Arguments.Count; ++i)
            {
                ReplaceAllGenericTypesInExpr(baseCtor.Arguments[i]);
            }
        }

        private void ReplaceAllGenericTypesInConstainStmt(AstConstrainStmt stmt)
        {
            if (stmt.Expr != null)
                ReplaceAllGenericTypesInExpr(stmt.Expr);
            foreach (var a in stmt.AdditionalExprs)
                ReplaceAllGenericTypesInExpr(a);
        }

        private void ReplaceAllGenericTypesInThrowStmt(AstThrowStmt stmt)
        {
            if (stmt.ThrowExpression != null)
                ReplaceAllGenericTypesInExpr(stmt.ThrowExpression);
        }

        private void ReplaceAllGenericTypesInTryCatchStmt(AstTryCatchStmt stmt)
        {
            ReplaceAllGenericTypesInExpr(stmt.TryBlock);
            if (stmt.FinallyBlock != null)
                ReplaceAllGenericTypesInExpr(stmt.FinallyBlock);
            foreach (var a in stmt.CatchBlocks)
                ReplaceAllGenericTypesInExpr(a);
        }

        private void ReplaceAllGenericTypesInCatchStmt(AstCatchStmt stmt)
        {
            ReplaceAllGenericTypesInExpr(stmt.CatchBlock);
            if (stmt.CatchParam != null)
                ReplaceAllGenericTypesInParam(stmt.CatchParam);
        }

        private bool IsGenericEntry(AstStatement expr, out AstNestedExpr value)
        {
            // generic types are like that :)
            if (expr is AstNestedExpr nestExpr &&
                nestExpr.LeftPart == null &&
                nestExpr.RightPart is AstIdExpr idExpr)
            {
                // if found the generic entry - replace it
                if (_currentGenericToRealMappings.TryGetValue(idExpr.Name, out var val))
                {
                    value = val;
                    return true;
                }
            }

            // could be when T[] or T*
            if (expr is AstIdExpr idExpr3)
            {
                // if found the generic entry - replace it
                if (_currentGenericToRealMappings.TryGetValue(idExpr3.Name, out var val))
                {
                    value = val;
                    return true;
                }
            }

            // we also need to replace all generic defaults
            if (expr is AstDefaultGenericExpr defG)
            {
                // getting the name of default
                var defaultName = defG.TypeForDefault.Declaration.Name.Name;

                // if found the generic entry - replace it
                if (_currentGenericToRealMappings.TryGetValue(defaultName, out var val))
                {
                    // getting default for the new type
                    value = new AstNestedExpr(AstDefaultExpr.GetDefaultValueForType(val.OutType, defG, _compiler.MessageHandler), null, defG.Location)
                    {
                        Scope = defG.Scope,
                        SourceFile = defG.SourceFile,
                    };
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}
