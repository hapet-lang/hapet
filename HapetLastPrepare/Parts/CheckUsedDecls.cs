using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Ast;
using HapetFrontend.Errors;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetFrontend.Enums;
using HapetFrontend.Parsing;
using System;
using HapetFrontend.Extensions;

namespace HapetLastPrepare
{
    public partial class LastPrepare
    {
        public void CheckUsedDecls()
        {
            if (_compiler.CurrentProjectSettings.TargetFormat == HapetFrontend.TargetFormat.Library)
                return;

            CheckUsedDeclsDecl(_compiler.MainFunction);

            // set that stor and stor_var are used
            var unique = new List<AstDeclaration>();
            unique.AddRange(_postPreparer.AllClassesMetadata.Where(x => !x.IsImported));
            unique.AddRange(_postPreparer.AllStructsMetadata.Where(x => !x.IsImported));
            foreach (var decl in unique)
            {
                var stor = decl.GetDeclarations().FirstOrDefault(x => x is AstFuncDecl fnc && fnc.ClassFunctionType == ClassFunctionType.StaticCtor);
                CheckUsedDeclsDecl(stor);
                var stor_var = decl.GetDeclarations().FirstOrDefault(x => x is AstVarDecl vd && vd.IsStaticCtorField);
                CheckUsedDeclsDecl(stor_var);
            }
        }

        private void CheckUsedDeclsDecl(AstDeclaration decl, List<AstDeclaration> usedDecls = null)
        {
            // need to reset it so there won't be problems at codegen
            decl.IsDeclarationUsedOnlyDeclare = false;

            // add to list if required
            if (usedDecls != null)
            {
                if (usedDecls.Contains(decl))
                    return;
                usedDecls.Add(decl);
            }
            else
            {
                if (decl.IsDeclarationUsed)
                    return;
                decl.IsDeclarationUsed = true;
            }

            if (decl is AstClassDecl classDecl)
            {
                CheckUsedDeclsClass(classDecl, usedDecls);
            }
            else if (decl is AstStructDecl structDecl)
            {
                CheckUsedDeclsStruct(structDecl, usedDecls);
            }
            else if (decl is AstDelegateDecl delegateDecl)
            {
                CheckUsedDeclsDelegate(delegateDecl, usedDecls);
            }
            else if (decl is AstFuncDecl funcDecl)
            {
                CheckUsedDeclsFunction(funcDecl, usedDecls);
            }
            else if (decl is AstPropertyDecl propDecl)
            {
                if (propDecl.GetFunction != null)
                {
                    CheckUsedDeclsDecl(propDecl.GetFunction, usedDecls);
                }
                if (propDecl.SetFunction != null)
                {
                    CheckUsedDeclsDecl(propDecl.SetFunction, usedDecls);
                }

                if (propDecl is AstIndexerDecl indDecl)
                {
                    CheckUsedDeclsParam(indDecl.IndexerParameter, usedDecls);
                }

                CheckUsedDeclsVar(propDecl, usedDecls);
            }
            else if (decl is AstVarDecl varDecl)
            {
                CheckUsedDeclsVar(varDecl, usedDecls);
            }
            else if (decl is AstParamDecl paramDecl)
            {
                CheckUsedDeclsParam(paramDecl, usedDecls);
            }
        }

        public void CheckUsedDeclsClass(AstClassDecl decl, List<AstDeclaration> usedDecls = null)
        {
            // if we inherit from a class/interface - all its fields/props/methods should be 
            // marked as used, because at codegen they are needed
            foreach (var i in decl.InheritedFrom)
            {
                CheckUsedDeclsExpr(i, usedDecls);
                foreach (var d in (i.OutType as ClassType).Declaration.Declarations)
                {
                    CheckUsedDeclsDecl(d, usedDecls);
                }
            }

            CheckVirtuals(decl, usedDecls);
        }

        public void CheckUsedDeclsStruct(AstStructDecl decl, List<AstDeclaration> usedDecls = null)
        {
            // if we inherit from a class/interface - all its fields/props/methods should be 
            // marked as used, because at codegen they are needed
            foreach (var i in decl.InheritedFrom)
            {
                CheckUsedDeclsExpr(i, usedDecls);
                foreach (var d in (i.OutType as ClassType).Declaration.Declarations)
                {
                    CheckUsedDeclsDecl(d, usedDecls);
                }
            }

            CheckVirtuals(decl, usedDecls);
        }

        private void CheckVirtuals(AstDeclaration decl, List<AstDeclaration> usedDecls = null)
        {
            // we need to set all its virtual funcs to used to be able 
            // to generate vtables
            if (!decl.IsImported || (decl.IsImported && (decl.IsImplOfGeneric || decl.IsNestedDecl && decl.ParentDecl.IsImplOfGeneric)))
            {
                foreach (var d in decl.GetDeclarations())
                {
                    if (d.SpecialKeys.Contains(TokenType.KwVirtual) ||
                        d.SpecialKeys.Contains(TokenType.KwAbstract) ||
                        d.SpecialKeys.Contains(TokenType.KwOverride))
                    {
                        d.IsDeclarationUsedOnlyDeclare = true;
                        usedDecls?.Add(decl);
                    }
                }
            }
        }

        public void CheckUsedDeclsDelegate(AstDelegateDecl decl, List<AstDeclaration> usedDecls = null)
        {
            foreach (var p in decl.Parameters)
            {
                CheckUsedDeclsDecl(p, usedDecls);
            }
            CheckUsedDeclsExpr(decl.Returns, usedDecls);
        }

        public void CheckUsedDeclsFunction(AstFuncDecl decl, List<AstDeclaration> usedDecls = null)
        {
            foreach (var p in decl.Parameters)
            {
                CheckUsedDeclsDecl(p, usedDecls);
            }
            CheckUsedDeclsExpr(decl.Returns, usedDecls);

            if (decl.Body != null)
            {
                CheckUsedDeclsBlockExpr(decl.Body, usedDecls);
            }
        }

        public void CheckUsedDeclsVar(AstVarDecl decl, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(decl.Type, usedDecls);

            if (decl.Initializer != null)
            {
                CheckUsedDeclsExpr(decl.Initializer, usedDecls);
            }
        }

        public void CheckUsedDeclsParam(AstParamDecl decl, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(decl.Type, usedDecls);

            if (decl.DefaultValue != null)
            {
                CheckUsedDeclsExpr(decl.DefaultValue, usedDecls);
            }
        }

        public void CheckUsedDeclsExpr(AstStatement stmt, List<AstDeclaration> usedDecls = null)
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
                    CheckUsedDeclsDecl(varDecl, usedDecls);
                    break;

                case AstBlockExpr blockExpr:
                    CheckUsedDeclsBlockExpr(blockExpr, usedDecls);
                    break;
                case AstUnaryExpr unExpr:
                    CheckUsedDeclsUnaryExpr(unExpr, usedDecls);
                    break;
                case AstBinaryExpr binExpr:
                    CheckUsedDeclsBinaryExpr(binExpr, usedDecls);
                    break;
                case AstPointerExpr pointerExpr:
                    CheckUsedDeclsPointerExpr(pointerExpr, usedDecls);
                    break;
                case AstAddressOfExpr addrExpr:
                    CheckUsedDeclsAddressOfExpr(addrExpr, usedDecls);
                    break;
                case AstNewExpr newExpr:
                    CheckUsedDeclsNewExpr(newExpr, usedDecls);
                    break;
                case AstArgumentExpr argumentExpr:
                    CheckUsedDeclsArgumentExpr(argumentExpr, usedDecls);
                    break;
                case AstIdGenericExpr genExpr:
                    CheckUsedDeclsIdGenericExpr(genExpr, usedDecls);
                    break;
                case AstIdExpr idExpr:
                    CheckUsedDeclsIdExpr(idExpr, usedDecls);
                    break;
                case AstCallExpr callExpr:
                    CheckUsedDeclsCallExpr(callExpr, usedDecls);
                    break;
                case AstCastExpr castExpr:
                    CheckUsedDeclsCastExpr(castExpr, usedDecls);
                    break;
                case AstNestedExpr nestExpr:
                    CheckUsedDeclsNestedExpr(nestExpr, usedDecls);
                    break;
                case AstDefaultExpr defaultExpr:
                    CheckUsedDeclsDefaultExpr(defaultExpr, usedDecls);
                    break;
                case AstDefaultGenericExpr _: // no need
                    break;
                case AstEmptyStructExpr _: // no need
                    break;
                case AstArrayExpr arrayExpr:
                    CheckUsedDeclsArrayExpr(arrayExpr, usedDecls);
                    break;
                case AstArrayCreateExpr arrayCreateExpr:
                    CheckUsedDeclsArrayCreateExpr(arrayCreateExpr, usedDecls);
                    break;
                case AstArrayAccessExpr arrayAccExpr:
                    CheckUsedDeclsArrayAccessExpr(arrayAccExpr, usedDecls);
                    break;
                case AstTernaryExpr ternaryExpr:
                    CheckUsedDeclsTernaryExpr(ternaryExpr, usedDecls);
                    break;
                case AstCheckedExpr checkedExpr:
                    CheckUsedDeclsCheckedExpr(checkedExpr, usedDecls);
                    break;
                case AstSATOfExpr satExpr:
                    CheckUsedDeclsSATExpr(satExpr, usedDecls);
                    break;
                case AstEmptyExpr:
                    break;
                case AstLambdaExpr lambdaExpr:
                    CheckUsedDeclsLambdaExpr(lambdaExpr, usedDecls);
                    break;

                // statements
                case AstAssignStmt assignStmt:
                    CheckUsedDeclsAssignStmt(assignStmt, usedDecls);
                    break;
                case AstForStmt forStmt:
                    CheckUsedDeclsForStmt(forStmt, usedDecls);
                    break;
                case AstWhileStmt whileStmt:
                    CheckUsedDeclsWhileStmt(whileStmt, usedDecls);
                    break;
                case AstDoWhileStmt doWhileStmt:
                    CheckUsedDeclsDoWhileStmt(doWhileStmt, usedDecls);
                    break;
                case AstIfStmt ifStmt:
                    CheckUsedDeclsIfStmt(ifStmt, usedDecls);
                    break;
                case AstSwitchStmt switchStmt:
                    CheckUsedDeclsSwitchStmt(switchStmt, usedDecls);
                    break;
                case AstCaseStmt caseStmt:
                    CheckUsedDeclsCaseStmt(caseStmt, usedDecls);
                    break;
                case AstBreakContStmt:
                    // nothing to do
                    break;
                case AstReturnStmt returnStmt:
                    CheckUsedDeclsReturnStmt(returnStmt, usedDecls);
                    break;
                case AstAttributeStmt attrStmt:
                    CheckUsedDeclsAttributeStmt(attrStmt, usedDecls);
                    break;
                case AstBaseCtorStmt baseStmt:
                    CheckUsedDeclsBaseCtorStmt(baseStmt, usedDecls);
                    break;
                case AstConstrainStmt constrainStmt:
                    CheckUsedDeclsConstrainStmt(constrainStmt, usedDecls);
                    break;
                case AstThrowStmt throwStmt:
                    CheckUsedDeclsThrowStmt(throwStmt, usedDecls);
                    break;
                case AstTryCatchStmt tryCatchStmt:
                    CheckUsedDeclsTryCatchStmt(tryCatchStmt, usedDecls);
                    break;
                case AstCatchStmt catchStmt:
                    CheckUsedDeclsCatchStmt(catchStmt, usedDecls);
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

        private void CheckUsedDeclsBlockExpr(AstBlockExpr expr, List<AstDeclaration> usedDecls = null)
        {
            foreach (var stmt in expr.Statements)
            {
                if (stmt == null)
                    continue;
                if (stmt is AstFuncDecl)
                    continue; // skip nested funcs
                CheckUsedDeclsExpr(stmt, usedDecls);
            }
        }

        private void CheckUsedDeclsUnaryExpr(AstUnaryExpr expr, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(expr.SubExpr, usedDecls);

            if (expr.ActualOperator is UserDefinedUnaryOperator userDef)
            {
                CheckUsedDeclsDecl(userDef.Function.Declaration, usedDecls);
            }
        }

        private void CheckUsedDeclsBinaryExpr(AstBinaryExpr expr, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(expr.Left, usedDecls);
            CheckUsedDeclsExpr(expr.Right, usedDecls);

            if (expr.ActualOperator is UserDefinedBinaryOperator userDef)
            {
                CheckUsedDeclsDecl(userDef.Function.Declaration, usedDecls);
            }
        }

        private void CheckUsedDeclsPointerExpr(AstPointerExpr expr, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(expr.SubExpression, usedDecls);
        }

        private void CheckUsedDeclsAddressOfExpr(AstAddressOfExpr expr, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(expr.SubExpression, usedDecls);
        }

        private void CheckUsedDeclsNewExpr(AstNewExpr expr, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(expr.TypeName, usedDecls);
            foreach (var a in expr.Arguments)
            {
                CheckUsedDeclsArgumentExpr(a, usedDecls);
            }
            CheckUsedDeclsDecl(expr.ConstructorSymbol.Decl, usedDecls);

            // if ctor is used then dtor is also has to be used
            var parent = expr.ConstructorSymbol.Decl.ContainingParent;
            var dtor = parent.GetDeclarations().FirstOrDefault(x => x is AstFuncDecl fnc && fnc.ClassFunctionType == ClassFunctionType.Dtor);
            CheckUsedDeclsDecl(dtor, usedDecls);
        }

        private void CheckUsedDeclsArgumentExpr(AstArgumentExpr expr, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(expr.Expr, usedDecls);
        }

        private void CheckUsedDeclsIdGenericExpr(AstIdGenericExpr expr, List<AstDeclaration> usedDecls = null)
        {
            for (int i = 0; i < expr.GenericRealTypes.Count; ++i)
            {
                CheckUsedDeclsExpr(expr.GenericRealTypes[i], usedDecls);
            }

            if (expr.FindSymbol == null)
                return;
            CheckUsedDeclsDecl((expr.FindSymbol as DeclSymbol).Decl, usedDecls);
        }

        private void CheckUsedDeclsIdExpr(AstIdExpr expr, List<AstDeclaration> usedDecls = null)
        {
            if (expr.FindSymbol == null)
                return;
            CheckUsedDeclsDecl((expr.FindSymbol as DeclSymbol).Decl, usedDecls);
        }

        private void CheckUsedDeclsCallExpr(AstCallExpr expr, List<AstDeclaration> usedDecls = null)
        {
            if (expr.TypeOrObjectName != null)
            {
                CheckUsedDeclsExpr(expr.TypeOrObjectName, usedDecls);
            }

            CheckUsedDeclsExpr(expr.FuncName, usedDecls);

            // if it is a property function call
            // then set the orig property to be used
            if (expr.FuncName.OutType is FunctionType fnc && fnc.Declaration.IsPropertyFunction)
            {
                CheckUsedDeclsDecl(fnc.Declaration.NormalParent as AstPropertyDecl, usedDecls);
            }

            foreach (var a in expr.Arguments)
            {
                CheckUsedDeclsArgumentExpr(a, usedDecls);
            }
        }

        private void CheckUsedDeclsCastExpr(AstCastExpr expr, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(expr.TypeExpr, usedDecls);
            CheckUsedDeclsExpr(expr.SubExpression, usedDecls);
        }

        private void CheckUsedDeclsNestedExpr(AstNestedExpr expr, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(expr.LeftPart, usedDecls);
            CheckUsedDeclsExpr(expr.RightPart, usedDecls);
        }

        private void CheckUsedDeclsDefaultExpr(AstDefaultExpr expr, List<AstDeclaration> usedDecls = null)
        {
            if (expr.TypeForDefault != null)
                CheckUsedDeclsExpr(expr.TypeForDefault, usedDecls);
        }

        private void CheckUsedDeclsArrayExpr(AstArrayExpr expr, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(expr.SubExpression, usedDecls);
        }

        private void CheckUsedDeclsArrayCreateExpr(AstArrayCreateExpr expr, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(expr.TypeName, usedDecls);
            foreach (var s in expr.SizeExprs)
            {
                CheckUsedDeclsExpr(s, usedDecls);
            }
            foreach (var e in expr.Elements)
            {
                CheckUsedDeclsExpr(e, usedDecls);
            }
        }

        private void CheckUsedDeclsArrayAccessExpr(AstArrayAccessExpr expr, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(expr.ObjectName, usedDecls);
            CheckUsedDeclsExpr(expr.ParameterExpr, usedDecls);
            if (expr.IndexerDecl != null)
                CheckUsedDeclsDecl(expr.IndexerDecl, usedDecls);
        }

        private void CheckUsedDeclsTernaryExpr(AstTernaryExpr expr, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(expr.Condition, usedDecls);
            CheckUsedDeclsExpr(expr.TrueExpr, usedDecls);
            CheckUsedDeclsExpr(expr.FalseExpr, usedDecls);
        }

        private void CheckUsedDeclsCheckedExpr(AstCheckedExpr expr, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(expr.SubExpression, usedDecls);
        }

        private void CheckUsedDeclsSATExpr(AstSATOfExpr expr, List<AstDeclaration> usedDecls = null)
        {
            if (expr.TargetType != null)
                CheckUsedDeclsExpr(expr.TargetType, usedDecls);
        }

        private void CheckUsedDeclsLambdaExpr(AstLambdaExpr expr, List<AstDeclaration> usedDecls = null)
        {
            foreach (var p in expr.Parameters)
            {
                CheckUsedDeclsDecl(p, usedDecls);
            }
            CheckUsedDeclsExpr(expr.Returns, usedDecls);

            if (expr.Body != null)
            {
                CheckUsedDeclsBlockExpr(expr.Body, usedDecls);
            }
        }

        private void CheckUsedDeclsAssignStmt(AstAssignStmt stmt, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsNestedExpr(stmt.Target, usedDecls);
            CheckUsedDeclsExpr(stmt.Value, usedDecls);
        }

        private void CheckUsedDeclsForStmt(AstForStmt stmt, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(stmt.FirstArgument, usedDecls);
            CheckUsedDeclsExpr(stmt.SecondArgument, usedDecls);
            CheckUsedDeclsExpr(stmt.ThirdArgument, usedDecls);

            CheckUsedDeclsBlockExpr(stmt.Body, usedDecls);
        }

        private void CheckUsedDeclsWhileStmt(AstWhileStmt stmt, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(stmt.Condition, usedDecls);

            CheckUsedDeclsBlockExpr(stmt.Body, usedDecls);
        }

        private void CheckUsedDeclsDoWhileStmt(AstDoWhileStmt stmt, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(stmt.Condition, usedDecls);

            CheckUsedDeclsBlockExpr(stmt.Body, usedDecls);
        }

        private void CheckUsedDeclsIfStmt(AstIfStmt stmt, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(stmt.Condition, usedDecls);

            CheckUsedDeclsBlockExpr(stmt.BodyTrue, usedDecls);
            if (stmt.BodyFalse != null)
                CheckUsedDeclsBlockExpr(stmt.BodyFalse, usedDecls);
        }

        private void CheckUsedDeclsSwitchStmt(AstSwitchStmt stmt, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(stmt.SubExpression, usedDecls);

            foreach (var c in stmt.Cases)
            {
                CheckUsedDeclsExpr(c, usedDecls);
            }
        }

        private void CheckUsedDeclsCaseStmt(AstCaseStmt stmt, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(stmt.Pattern, usedDecls);

            CheckUsedDeclsExpr(stmt.Body, usedDecls);
        }

        private void CheckUsedDeclsReturnStmt(AstReturnStmt stmt, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(stmt.ReturnExpression, usedDecls);
        }

        private void CheckUsedDeclsAttributeStmt(AstAttributeStmt stmt, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(stmt.AttributeName, usedDecls);
            foreach (var s in stmt.Arguments)
                CheckUsedDeclsArgumentExpr(s, usedDecls);
        }

        private void CheckUsedDeclsBaseCtorStmt(AstBaseCtorStmt stmt, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(stmt.ThisArgument, usedDecls);
            foreach (var a in stmt.Arguments)
            {
                CheckUsedDeclsExpr(a, usedDecls);
            }
        }

        private void CheckUsedDeclsConstrainStmt(AstConstrainStmt stmt, List<AstDeclaration> usedDecls = null)
        {
            if (stmt.Expr != null)
                CheckUsedDeclsExpr(stmt.Expr, usedDecls);
        }

        private void CheckUsedDeclsThrowStmt(AstThrowStmt stmt, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(stmt.ThrowExpression, usedDecls);
        }

        private void CheckUsedDeclsTryCatchStmt(AstTryCatchStmt stmt, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(stmt.TryBlock, usedDecls);
            foreach (var c in stmt.CatchBlocks)
                CheckUsedDeclsExpr(c, usedDecls);
            if (stmt.FinallyBlock != null)
                CheckUsedDeclsExpr(stmt.FinallyBlock, usedDecls);
        }

        private void CheckUsedDeclsCatchStmt(AstCatchStmt stmt, List<AstDeclaration> usedDecls = null)
        {
            CheckUsedDeclsExpr(stmt.CatchBlock, usedDecls);
            CheckUsedDeclsDecl(stmt.CatchParam, usedDecls);
        }
    }
}
