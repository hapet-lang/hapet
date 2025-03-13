using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Helpers;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private Dictionary<string, AstNestedExpr> _currentGenericMapping = new Dictionary<string, AstNestedExpr>();

        private void MakeGenericMapping(List<AstIdExpr> generics, List<AstNestedExpr> normalTypes)
        {
            // ini the dict
            _currentGenericMapping = new Dictionary<string, AstNestedExpr>();
            for (int i = 0; i < generics.Count; ++i)
            {
                _currentGenericMapping.Add(generics[i].Name, normalTypes[i]);
            }
        }

        public void ReplaceAllGenericTypesInDecl(AstDeclaration decl)
        {
            if (decl is AstClassDecl clsDecl)
                ReplaceAllGenericTypesInClass(clsDecl);
            else if (decl is AstFuncDecl funcDecl)
                ReplaceAllGenericTypesInFunction(funcDecl);
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
                        ReplaceAllGenericTypesInExpr(indDecl.IndexerParameter);
                    }

                    ReplaceAllGenericTypesInVar(propDecl);
                }
                else if (decl is AstVarDecl fieldDecl) // field 
                {
                    ReplaceAllGenericTypesInVar(fieldDecl);
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

            // return type replacing
            if (IsGenericEntry(funcDecl.Returns, out var val))
                funcDecl.Returns = val;
            else
                ReplaceAllGenericTypesInExpr(funcDecl.Returns);
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
                case AstIdExpr _:
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
                case AstDefaultExpr _:
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
                // TODO: check other expressions

                default:
                    {
                        // TODO: anything to do here?
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

            if (IsGenericEntry(castExpr.TypeExpr, out var val))
                castExpr.TypeExpr = val;
            else
                ReplaceAllGenericTypesInExpr(castExpr.TypeExpr);
        }

        private void ReplaceAllGenericTypesInNestedExpr(AstNestedExpr nestExpr)
        {
            if (IsGenericEntry(nestExpr.RightPart, out var val))
                nestExpr.RightPart = val;
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

        private bool IsGenericEntry(AstStatement expr, out AstNestedExpr value)
        {
            // generic types are like that :)
            if (expr is AstNestedExpr nestExpr &&
                nestExpr.LeftPart == null &&
                nestExpr.RightPart is AstIdExpr idExpr)
            {
                // if found the generic entry - replace it
                if (_currentGenericMapping.TryGetValue(idExpr.Name, out var val))
                {
                    value = val;
                    return true;
                }
            }

            // if found something generic inside another generic shite
            if (expr is AstIdGenericExpr genExpr)
            {
                var nestedGenerics = genExpr.GenericRealTypes.GetNestedList();
                for (int i = 0; i < nestedGenerics.Count; ++i)
                {
                    var currNest = nestedGenerics[i];
                    // generic types are like that :)
                    if (currNest.LeftPart == null &&
                        currNest.RightPart is AstIdExpr idExpr2)
                    {
                        // if found the generic entry - replace it
                        if (_currentGenericMapping.TryGetValue(idExpr2.Name, out var val))
                        {
                            // no need to tell em about it. we do it by our own here
                            genExpr.GenericRealTypes[i] = val;
                        }
                    }
                }
            }

            // could be when T[] or T*
            if (expr is AstIdExpr idExpr3)
            {
                // if found the generic entry - replace it
                if (_currentGenericMapping.TryGetValue(idExpr3.Name, out var val))
                {
                    value = val;
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}
