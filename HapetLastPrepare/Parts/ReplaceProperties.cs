using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Ast;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetLastPrepare.Entities;
using HapetPostPrepare.Other;
using HapetPostPrepare;
using System;
using HapetFrontend.Parsing;
using HapetFrontend.Extensions;

namespace HapetLastPrepare
{
    // LPRAP - Last Prepare Replace All Props (and static/const fields)
    public partial class LastPrepare
    {
        public void ReplaceAllProperties()
        {
            OutInfo outInfo = OutInfo.Default;
            var savedParentStack = _postPreparer._currentParentStack;
            _postPreparer._currentParentStack = ParentStackManager.Create(_compiler.MessageHandler);

            foreach (var cls in _postPreparer.AllClassesMetadata)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(cls))
                    continue;

                _postPreparer._currentParentStack.AddParent(cls);
                _postPreparer._currentSourceFile = cls.SourceFile;
                LPRAPClass(cls, ref outInfo);
                _postPreparer._currentParentStack.RemoveParent();
            }
            foreach (var str in _postPreparer.AllStructsMetadata)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(str))
                    continue;

                _postPreparer._currentParentStack.AddParent(str);
                _postPreparer._currentSourceFile = str.SourceFile;
                LPRAPStruct(str, ref outInfo);
                _postPreparer._currentParentStack.RemoveParent();
            }
            foreach (var del in _postPreparer.AllDelegatesMetadata)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(del))
                    continue;

                var parent = del.ContainingParent;
                if (parent?.IsNestedDecl ?? false)
                    _postPreparer._currentParentStack.AddParent(parent.ParentDecl);
                if (parent != null)
                    _postPreparer._currentParentStack.AddParent(parent);
                _postPreparer._currentParentStack.AddParent(del);

                _postPreparer._currentSourceFile = del.SourceFile;
                LPRAPDelegate(del, ref outInfo);

                _postPreparer._currentParentStack.RemoveParent();
                if (parent?.IsNestedDecl ?? false)
                    _postPreparer._currentParentStack.RemoveParent();
                if (parent != null)
                    _postPreparer._currentParentStack.RemoveParent();
            }
            foreach (var func in _postPreparer.AllFunctionsMetadata)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(func))
                    continue;

                var parent = func.ContainingParent;
                if (parent?.IsNestedDecl ?? false)
                    _postPreparer._currentParentStack.AddParent(parent.ParentDecl);
                if (parent != null)
                    _postPreparer._currentParentStack.AddParent(parent);
                _postPreparer._currentParentStack.AddParent(func);

                _postPreparer._currentSourceFile = func.SourceFile;
                LPRAPFunction(func, ref outInfo);

                _postPreparer._currentParentStack.RemoveParent();
                if (parent?.IsNestedDecl ?? false)
                    _postPreparer._currentParentStack.RemoveParent();
                if (parent != null)
                    _postPreparer._currentParentStack.RemoveParent();
            }

            _postPreparer._currentParentStack = savedParentStack;
        }

        private void LPRAPDecl(AstDeclaration decl, ref OutInfo outInfo)
        {
            if (decl is AstClassDecl classDecl)
            {
                LPRAPClass(classDecl, ref outInfo);
            }
            else if (decl is AstStructDecl structDecl)
            {
                LPRAPStruct(structDecl, ref outInfo);
            }
            else if (decl is AstDelegateDecl delegateDecl)
            {
                LPRAPDelegate(delegateDecl, ref outInfo);
            }
            else if (decl is AstFuncDecl funcDecl)
            {
                LPRAPFunction(funcDecl, ref outInfo);
            }
            else if (decl is AstPropertyDecl propDecl)
            {
                LPRAPVar(propDecl, ref outInfo);
            }
            else if (decl is AstVarDecl varDecl)
            {
                LPRAPVar(varDecl, ref outInfo);
            }
        }

        public void LPRAPClass(AstClassDecl decl, ref OutInfo outInfo)
        {
            foreach (var d in decl.Declarations)
            {
                if (d is AstFuncDecl)
                {
                    // skip funcs - they are prepared in another loop
                    continue;
                }
                else
                {
                    LPRAPDecl(d, ref outInfo);
                }
            }
        }

        public void LPRAPStruct(AstStructDecl decl, ref OutInfo outInfo)
        {
            foreach (var d in decl.Declarations)
            {
                if (d is AstFuncDecl)
                {
                    // skip funcs - they are prepared in another loop
                    continue;
                }
                else
                {
                    LPRAPDecl(d, ref outInfo);
                }
            }
        }

        public void LPRAPDelegate(AstDelegateDecl decl, ref OutInfo outInfo)
        {
            foreach (var p in decl.Parameters)
            {
                LPRAPParam(p, ref outInfo);
            }
        }

        public void LPRAPFunction(AstFuncDecl decl, ref OutInfo outInfo)
        {
            foreach (var p in decl.Parameters)
            {
                LPRAPParam(p, ref outInfo);
            }

            if (decl.Body != null)
            {
                LPRAPBlockExpr(decl.Body, ref outInfo);
            }
        }

        public void LPRAPVar(AstVarDecl decl, ref OutInfo outInfo)
        {
            if (decl.Initializer != null)
            {
                LPRAPExpr(decl.Initializer, ref outInfo);
            }
        }

        public void LPRAPParam(AstParamDecl decl, ref OutInfo outInfo)
        {
            if (decl.DefaultValue != null)
            {
                LPRAPExpr(decl.DefaultValue, ref outInfo);
            }
        }

        public void LPRAPExpr(AstStatement stmt, ref OutInfo outInfo)
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
                    LPRAPVar(varDecl, ref outInfo);
                    break;

                case AstBlockExpr blockExpr:
                    LPRAPBlockExpr(blockExpr, ref outInfo);
                    break;
                case AstUnaryExpr unExpr:
                    LPRAPUnaryExpr(unExpr, ref outInfo);
                    break;
                case AstBinaryExpr binExpr:
                    LPRAPBinaryExpr(binExpr, ref outInfo);
                    break;
                case AstPointerExpr pointerExpr:
                    LPRAPPointerExpr(pointerExpr, ref outInfo);
                    break;
                case AstAddressOfExpr addrExpr:
                    LPRAPAddressOfExpr(addrExpr, ref outInfo);
                    break;
                case AstNewExpr newExpr:
                    LPRAPNewExpr(newExpr, ref outInfo);
                    break;
                case AstArgumentExpr argumentExpr:
                    LPRAPArgumentExpr(argumentExpr, ref outInfo);
                    break;
                case AstIdExpr idExpr:
                    LPRAPIdExpr(idExpr, ref outInfo);
                    break;
                case AstCallExpr callExpr:
                    LPRAPCallExpr(callExpr, ref outInfo);
                    break;
                case AstCastExpr castExpr:
                    LPRAPCastExpr(castExpr, ref outInfo);
                    break;
                case AstNestedExpr nestExpr:
                    LPRAPNestedExpr(nestExpr, ref outInfo);
                    break;
                case AstDefaultExpr defaultExpr:
                    LPRAPDefaultExpr(defaultExpr, ref outInfo);
                    break;
                case AstDefaultGenericExpr _: // no need
                    break;
                case AstEmptyStructExpr _: // no need
                    break;
                case AstArrayExpr arrayExpr:
                    LPRAPArrayExpr(arrayExpr, ref outInfo);
                    break;
                case AstArrayCreateExpr arrayCreateExpr:
                    LPRAPArrayCreateExpr(arrayCreateExpr, ref outInfo);
                    break;
                case AstArrayAccessExpr arrayAccExpr:
                    LPRAPArrayAccessExpr(arrayAccExpr, ref outInfo);
                    break;
                case AstTernaryExpr ternaryExpr:
                    LPRAPTernaryExpr(ternaryExpr, ref outInfo);
                    break;
                case AstCheckedExpr checkedExpr:
                    LPRAPCheckedExpr(checkedExpr, ref outInfo);
                    break;
                case AstEmptyExpr:
                    break;

                // statements
                case AstAssignStmt assignStmt:
                    LPRAPAssignStmt(assignStmt, ref outInfo);
                    break;
                case AstForStmt forStmt:
                    LPRAPForStmt(forStmt, ref outInfo);
                    break;
                case AstWhileStmt whileStmt:
                    LPRAPWhileStmt(whileStmt, ref outInfo);
                    break;
                case AstIfStmt ifStmt:
                    LPRAPIfStmt(ifStmt, ref outInfo);
                    break;
                case AstSwitchStmt switchStmt:
                    LPRAPSwitchStmt(switchStmt, ref outInfo);
                    break;
                case AstCaseStmt caseStmt:
                    LPRAPCaseStmt(caseStmt, ref outInfo);
                    break;
                case AstBreakContStmt:
                    // nothing to do
                    break;
                case AstReturnStmt returnStmt:
                    LPRAPReturnStmt(returnStmt, ref outInfo);
                    break;
                case AstAttributeStmt attrStmt:
                    LPRAPAttributeStmt(attrStmt, ref outInfo);
                    break;
                case AstBaseCtorStmt baseStmt:
                    LPRAPBaseCtorStmt(baseStmt, ref outInfo);
                    break;
                case AstConstrainStmt constrainStmt:
                    LPRAPConstrainStmt(constrainStmt, ref outInfo);
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

        private void LPRAPBlockExpr(AstBlockExpr expr, ref OutInfo outInfo)
        {
            // list of all replacements that should be done
            // so all Propa assigns would be replaced with func calls
            Dictionary<AstAssignStmt, AstCallExpr> repls = new Dictionary<AstAssignStmt, AstCallExpr>();

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
                // we need to handle the statements to replaces props with calls
                else if (stmt is AstAssignStmt asgn)
                {
                    LPRAPAssignStmt(asgn, ref outInfo);

                    var target = asgn.Target;
                    if (outInfo.ItWasProperty)
                    {
                        // reset
                        outInfo.ItWasProperty = false;

                        AstIdExpr propaName = target.UnrollToRightPart<AstIdExpr>();
                        // creating a call 
                        var fncVal = new AstArgumentExpr(asgn.Value, null);
                        fncVal.SetDataFromStmt(asgn.Value);
                        var fncCall = new AstCallExpr(target.LeftPart, propaName.GetCopy($"set_{propaName.Name}"), new List<AstArgumentExpr>() { fncVal });
                        fncCall.SetDataFromStmt(target);
                        fncCall.FuncName.OutType = outInfo.Property.SetFunction.Type.OutType;
                        UpdateCallWithFunc(fncCall, outInfo.Property.SetFunction);

                        repls.Add(asgn, fncCall);
                    }
                    else if (outInfo.ItWasIndexer)
                    {
                        // reset
                        outInfo.ItWasIndexer = false;

                        // if getting indexer to set
                        var fncName = new AstIdExpr("set_indexer__", target);
                        fncName.SetDataFromStmt(target);
                        // creating a call 
                        var fncArg = new AstArgumentExpr(outInfo.IndexedIndex, null);
                        fncArg.SetDataFromStmt(asgn.Value);
                        var fncVal = new AstArgumentExpr(asgn.Value, null);
                        fncVal.SetDataFromStmt(asgn.Value);
                        var fncCall = new AstCallExpr(outInfo.IndexedObject, fncName, new List<AstArgumentExpr>() { fncArg, fncVal });
                        fncCall.SetDataFromStmt(target);
                        fncCall.FuncName.OutType = outInfo.Property.SetFunction.Type.OutType;
                        UpdateCallWithFunc(fncCall, outInfo.Property.SetFunction);

                        repls.Add(asgn, fncCall);
                    }
                    else if (outInfo.ItWasStaticConst)
                    {
                        // reset
                        outInfo.ItWasStaticConst = false;

                        var gsMethods = outInfo.VarDecl.GetSetMethodsForStatic;
                        AstIdExpr varName = target.UnrollToRightPart<AstIdExpr>();
                        // creating a call 
                        var fncVal = new AstArgumentExpr(asgn.Value, null);
                        fncVal.SetDataFromStmt(asgn.Value);
                        var fncCall = new AstCallExpr(target.LeftPart, varName.GetCopy($"set_{varName.Name}"), new List<AstArgumentExpr>() { fncVal });
                        fncCall.SetDataFromStmt(target);
                        fncCall.FuncName.OutType = gsMethods.Value.Item2.Type.OutType;
                        UpdateCallWithFunc(fncCall, gsMethods.Value.Item2);

                        repls.Add(asgn, fncCall);
                    }
                }
                else
                {
                    LPRAPExpr(stmt, ref outInfo);
                }
            }

            // begin all replacements
            foreach (var pair in repls)
            {
                // replace the assign statement
                int assignIndex = expr.Statements.IndexOf(pair.Key);
                expr.Statements.Remove(pair.Key);
                expr.Statements.Insert(assignIndex, pair.Value);
            }
        }

        private void LPRAPUnaryExpr(AstUnaryExpr expr, ref OutInfo outInfo)
        {
            LPRAPExpr(expr.SubExpr, ref outInfo);
        }

        private void LPRAPBinaryExpr(AstBinaryExpr expr, ref OutInfo outInfo)
        {
            LPRAPExpr(expr.Left, ref outInfo);
            LPRAPExpr(expr.Right, ref outInfo);
        }

        private void LPRAPPointerExpr(AstPointerExpr expr, ref OutInfo outInfo)
        {
            LPRAPExpr(expr.SubExpression, ref outInfo);
        }

        private void LPRAPAddressOfExpr(AstAddressOfExpr expr, ref OutInfo outInfo)
        {
            LPRAPExpr(expr.SubExpression, ref outInfo);
        }

        private void LPRAPNewExpr(AstNewExpr expr, ref OutInfo outInfo)
        {
            foreach (var a in expr.Arguments)
            {
                LPRAPArgumentExpr(a, ref outInfo);
            }
        }

        private void LPRAPArgumentExpr(AstArgumentExpr expr, ref OutInfo outInfo)
        {
            LPRAPExpr(expr.Expr, ref outInfo);
        }

        private void LPRAPIdExpr(AstIdExpr expr, ref OutInfo outInfo)
        {

        }

        private void LPRAPCallExpr(AstCallExpr expr, ref OutInfo outInfo)
        {
            if (expr.TypeOrObjectName != null)
            {
                LPRAPExpr(expr.TypeOrObjectName, ref outInfo);
            }

            foreach (var a in expr.Arguments)
            {
                LPRAPArgumentExpr(a, ref outInfo);
            }
        }

        private void LPRAPCastExpr(AstCastExpr expr, ref OutInfo outInfo)
        {
            LPRAPExpr(expr.SubExpression, ref outInfo);
        }

        private void LPRAPNestedExpr(AstNestedExpr expr, ref OutInfo outInfo)
        {
            var tmpInInfo = HapetPostPrepare.Entities.InInfo.Default;
            var tmpOutInfo = HapetPostPrepare.Entities.OutInfo.Default;

            LPRAPExpr(expr.LeftPart, ref outInfo);
            LPRAPExpr(expr.RightPart, ref outInfo);
            if (expr.LeftPart == null)
            {
                // if getting indexer to set smth
                if (outInfo.ItWasIndexer && outInfo.IsPropertySet)
                {
                    // just skip - it should be handled by AssignInferencer
                    return;
                }
                else if (outInfo.ItWasIndexer)
                {
                    // reset
                    outInfo.ItWasIndexer = false;

                    // if getting indexer to get
                    var fncName = new AstIdExpr("get_indexer__", expr);
                    fncName.SetDataFromStmt(expr);
                    // creating a call 
                    var fncArg = new AstArgumentExpr(outInfo.IndexedIndex, null, expr);
                    fncArg.SetDataFromStmt(expr);
                    var fncCall = new AstCallExpr(outInfo.IndexedObject, fncName, new List<AstArgumentExpr>() { fncArg });
                    fncCall.SetDataFromStmt(expr);
                    fncCall.FuncName.OutType = outInfo.Property.GetFunction.Type.OutType;
                    UpdateCallWithFunc(fncCall, outInfo.Property.GetFunction);

                    expr.LeftPart = null;
                    expr.RightPart = fncCall;
                }
            }
            else
            {
                var idExpr = expr.RightPart as AstIdExpr;
                var smbl = idExpr.FindSymbol;
                if (smbl is DeclSymbol typed)
                {
                    // if true - found set propa
                    if (CheckForProperty(typed.Decl, idExpr, ref outInfo))
                        return;
                    // if true - found set field
                    if (CheckForStaticConst(typed.Decl, idExpr, ref outInfo))
                        return;
                }
            }
            outInfo.ItWasProperty = false;
            outInfo.ItWasStaticConst = false;

            bool CheckForProperty(AstDeclaration decl, AstIdExpr propaName, ref OutInfo outInfoInside)
            {
                // if the ast is an access to a property
                if (decl is AstPropertyDecl pd)
                {
                    outInfoInside.Property = pd;
                    // if getting property to set smth
                    if (outInfoInside.IsPropertySet)
                    {
                        outInfoInside.ItWasProperty = true;
                        return true;
                    }
                    else
                    {
                        // if getting propa to get
                        var fncCall = new AstCallExpr(expr.LeftPart, propaName.GetCopy($"get_{propaName.Name}"), null);
                        fncCall.SetDataFromStmt(expr);
                        fncCall.FuncName.OutType = outInfoInside.Property.GetFunction.Type.OutType;
                        UpdateCallWithFunc(fncCall, outInfoInside.Property.GetFunction);

                        expr.LeftPart = null;
                        expr.RightPart = fncCall;
                    }
                }
                return false;
            }

            bool CheckForStaticConst(AstDeclaration decl, AstIdExpr varName, ref OutInfo outInfoInside)
            {
                if (decl is AstVarDecl vd && (vd.SpecialKeys.Contains(TokenType.KwStatic) || vd.SpecialKeys.Contains(TokenType.KwConst)))
                {
                    outInfoInside.VarDecl = vd;
                    // if getting var to set smth
                    if (outInfoInside.IsStaticConstSet)
                    {
                        outInfoInside.ItWasStaticConst = true;
                        return true;
                    }
                    else
                    {
                        var gsMethods = vd.GetSetMethodsForStatic;
                        // if getting propa to get
                        var fncCall = new AstCallExpr(expr.LeftPart, varName.GetCopy($"get_{varName.Name}"), null);
                        fncCall.SetDataFromStmt(expr);
                        fncCall.FuncName.OutType = gsMethods.Value.Item1.Type.OutType;
                        UpdateCallWithFunc(fncCall, gsMethods.Value.Item1);

                        expr.LeftPart = null;
                        expr.RightPart = fncCall;
                    }
                }
                return false;
            }
        }

        private void LPRAPDefaultExpr(AstDefaultExpr expr, ref OutInfo outInfo)
        {

        }

        private void LPRAPArrayExpr(AstArrayExpr expr, ref OutInfo outInfo)
        {
            // nop
        }

        private void LPRAPArrayCreateExpr(AstArrayCreateExpr expr, ref OutInfo outInfo)
        {
            foreach (var s in expr.SizeExprs)
            {
                LPRAPExpr(s, ref outInfo);
            }
            foreach (var e in expr.Elements)
            {
                LPRAPExpr(e, ref outInfo);
            }
        }

        private void LPRAPArrayAccessExpr(AstArrayAccessExpr expr, ref OutInfo outInfo)
        {
            // set propertySet to false because if we are in ArrayAccess - then ObjectName if it is property - has to be 'get_prop'
            var savedPropSet = outInfo.IsPropertySet;
            outInfo.IsPropertySet = false;
            LPRAPExpr(expr.ParameterExpr, ref outInfo);
            LPRAPExpr(expr.ObjectName, ref outInfo);
            outInfo.IsPropertySet = savedPropSet;

            if (expr.IndexerDecl != null)
            {
                outInfo.Property = expr.IndexerDecl;
                outInfo.ItWasIndexer = true;
                outInfo.IndexedIndex = expr.ParameterExpr;
                outInfo.IndexedObject = expr.ObjectName as AstNestedExpr;
                return; // everything is ok :)
            }
        }

        private void LPRAPTernaryExpr(AstTernaryExpr expr, ref OutInfo outInfo)
        {
            LPRAPExpr(expr.Condition, ref outInfo);
            LPRAPExpr(expr.TrueExpr, ref outInfo);
            LPRAPExpr(expr.FalseExpr, ref outInfo);
        }

        private void LPRAPCheckedExpr(AstCheckedExpr expr, ref OutInfo outInfo)
        {
            LPRAPExpr(expr.SubExpression, ref outInfo);
        }

        private void LPRAPAssignStmt(AstAssignStmt stmt, ref OutInfo outInfo)
        {
            // propaSet is true only here
            outInfo.IsPropertySet = true;
            LPRAPNestedExpr(stmt.Target, ref outInfo);
            outInfo.IsPropertySet = false;

            // pp assign value
            if (stmt.Value != null)
            {
                // save previous
                var saved1 = outInfo.ItWasIndexer;
                var saved2 = outInfo.ItWasProperty;
                var saved3 = outInfo.ItWasStaticConst;
                outInfo.ItWasIndexer = false;
                outInfo.ItWasProperty = false;
                outInfo.ItWasStaticConst = false;
                LPRAPExpr(stmt.Value, ref outInfo);
                outInfo.ItWasIndexer = saved1;
                outInfo.ItWasProperty = saved2;
                outInfo.ItWasStaticConst = saved3;
            }
        }

        private void LPRAPForStmt(AstForStmt stmt, ref OutInfo outInfo)
        {
            LPRAPExpr(stmt.FirstArgument, ref outInfo);
            LPRAPExpr(stmt.SecondArgument, ref outInfo);
            LPRAPExpr(stmt.ThirdArgument, ref outInfo);

            LPRAPBlockExpr(stmt.Body, ref outInfo);
        }

        private void LPRAPWhileStmt(AstWhileStmt stmt, ref OutInfo outInfo)
        {
            LPRAPExpr(stmt.Condition, ref outInfo);

            LPRAPBlockExpr(stmt.Body, ref outInfo);
        }

        private void LPRAPIfStmt(AstIfStmt stmt, ref OutInfo outInfo)
        {
            LPRAPExpr(stmt.Condition, ref outInfo);

            LPRAPBlockExpr(stmt.BodyTrue, ref outInfo);
            if (stmt.BodyFalse != null)
                LPRAPBlockExpr(stmt.BodyFalse, ref outInfo);
        }

        private void LPRAPSwitchStmt(AstSwitchStmt stmt, ref OutInfo outInfo)
        {
            LPRAPExpr(stmt.SubExpression, ref outInfo);

            foreach (var c in stmt.Cases)
            {
                LPRAPExpr(c, ref outInfo);
            }
        }

        private void LPRAPCaseStmt(AstCaseStmt stmt, ref OutInfo outInfo)
        {
            LPRAPExpr(stmt.Pattern, ref outInfo);

            LPRAPExpr(stmt.Body, ref outInfo);
        }

        private void LPRAPReturnStmt(AstReturnStmt stmt, ref OutInfo outInfo)
        {
            LPRAPExpr(stmt.ReturnExpression, ref outInfo);
        }

        private void LPRAPAttributeStmt(AstAttributeStmt stmt, ref OutInfo outInfo)
        {
            // nop
        }

        private void LPRAPBaseCtorStmt(AstBaseCtorStmt stmt, ref OutInfo outInfo)
        {
            foreach (var a in stmt.Arguments)
            {
                LPRAPExpr(a, ref outInfo);
            }
        }

        private void LPRAPConstrainStmt(AstConstrainStmt stmt, ref OutInfo outInfo)
        {
            // nop
        }

        private static void UpdateCallWithFunc(AstCallExpr call, AstFuncDecl decl)
        {
            /// same as in <see cref="PostPrepare.PostPrepareCallExprInference"/>

            // checking if it is a static func
            call.StaticCall = decl.SpecialKeys.Contains(TokenType.KwStatic);
            // call expr type is the same as func return type
            call.OutType = decl.Returns.OutType;
        }
    }
}
