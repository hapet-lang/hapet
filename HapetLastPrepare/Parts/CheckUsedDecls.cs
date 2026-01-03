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

            CheckUsedDeclsDecl(_compiler.MainFunction, null, true);

            // set that stor and stor_var are used
            var unique = new List<AstDeclaration>();
            unique.AddRange(_postPreparer.AllClassesMetadata.Where(x => !x.IsImported));
            unique.AddRange(_postPreparer.AllStructsMetadata.Where(x => !x.IsImported));
            foreach (var decl in unique)
            {
                var stor = decl.GetDeclarations().FirstOrDefault(x => x is AstFuncDecl fnc && fnc.ClassFunctionType == ClassFunctionType.StaticCtor);
                // there could be no stor in sythetic classes
                if (stor == null)
                    continue;

                CheckUsedDeclsDecl(stor, null, true);
                var stor_var = decl.GetDeclarations().FirstOrDefault(x => x is AstVarDecl vd && vd.IsStaticCtorField);
                CheckUsedDeclsDecl(stor_var);
            }
        }

        private void CheckUsedDeclsDecl(AstDeclaration decl, List<AstDeclaration> usedDecls = null, bool goDeep = false)
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
                CheckUsedDeclsClass(classDecl, usedDecls, goDeep);
            }
            else if (decl is AstStructDecl structDecl)
            {
                CheckUsedDeclsStruct(structDecl, usedDecls, goDeep);
            }
            else if (decl is AstDelegateDecl delegateDecl)
            {
                CheckUsedDeclsDelegate(delegateDecl, usedDecls, goDeep);
            }
            else if (decl is AstFuncDecl funcDecl)
            {
                CheckUsedDeclsFunction(funcDecl, usedDecls, goDeep);
            }
            else if (decl is AstPropertyDecl propDecl)
            {
                if (propDecl.GetFunction != null)
                {
                    CheckUsedDeclsDecl(propDecl.GetFunction, usedDecls, goDeep);
                }
                if (propDecl.SetFunction != null)
                {
                    CheckUsedDeclsDecl(propDecl.SetFunction, usedDecls, goDeep);
                }

                if (propDecl is AstIndexerDecl indDecl)
                {
                    CheckUsedDeclsParam(indDecl.IndexerParameter, usedDecls, goDeep);
                }

                CheckUsedDeclsVar(propDecl, usedDecls, goDeep);
            }
            else if (decl is AstVarDecl varDecl)
            {
                CheckUsedDeclsVar(varDecl, usedDecls, goDeep);
            }
            else if (decl is AstParamDecl paramDecl)
            {
                CheckUsedDeclsParam(paramDecl, usedDecls, goDeep);
            }
        }

        public void CheckUsedDeclsClass(AstClassDecl decl, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            // if we inherit from a class/interface - all its fields/props/methods should be 
            // marked as used, because at codegen they are needed
            foreach (var i in decl.InheritedFrom)
            {
                // no need to fully check non-generics
                bool isGeneric = i.OutType is ClassType clsT && HasOrIsGenericDecl(clsT.Declaration);

                CheckUsedDeclsExpr(i, usedDecls, isGeneric);
                if (goDeep)
                    foreach (var d in (i.OutType as ClassType).Declaration.Declarations)
                    {
                        CheckUsedDeclsDecl(d, usedDecls, isGeneric);
                    }
            }

            CheckVirtuals(decl, usedDecls, goDeep);

            // set that ini is used
            var ini = decl.Declarations.FirstOrDefault(x => x is AstFuncDecl fnc && fnc.ClassFunctionType == ClassFunctionType.Initializer);
            if (ini != null)
                CheckUsedDeclsDecl(ini, usedDecls, HasOrIsGenericDecl(decl));
        }

        public void CheckUsedDeclsStruct(AstStructDecl decl, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            // if we inherit from a class/interface - all its fields/props/methods should be 
            // marked as used, because at codegen they are needed
            foreach (var i in decl.InheritedFrom)
            {
                // no need to fully check non-generics
                bool isGeneric = i.OutType is ClassType clsT && HasOrIsGenericDecl(clsT.Declaration);

                CheckUsedDeclsExpr(i, usedDecls, isGeneric);
                if (goDeep)
                    foreach (var d in (i.OutType as ClassType).Declaration.Declarations)
                    {
                        CheckUsedDeclsDecl(d, usedDecls, isGeneric);
                    }
            }

            CheckVirtuals(decl, usedDecls, goDeep);

            // set that ini is used
            var ini = decl.Declarations.FirstOrDefault(x => x is AstFuncDecl fnc && fnc.ClassFunctionType == ClassFunctionType.Initializer);
            if (ini != null)
                CheckUsedDeclsDecl(ini, usedDecls, HasOrIsGenericDecl(decl));
        }

        private void CheckVirtuals(AstDeclaration decl, List<AstDeclaration> usedDecls = null, bool goDeep = false)
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
                        if (HasOrIsGenericDecl(d) || !d.IsImported || goDeep)
                        {
                            CheckUsedDeclsDecl(d, usedDecls, true);
                        }
                        else
                        {
                            // add to list if required
                            if (usedDecls != null)
                            {
                                if (usedDecls.Contains(d))
                                    return;
                                usedDecls.Add(d);
                            }
                            else
                            {
                                d.IsDeclarationUsedOnlyDeclare = true;
                            }
                        }
                    }
                }
            }
        }

        public void CheckUsedDeclsDelegate(AstDelegateDecl decl, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            foreach (var p in decl.Parameters)
            {
                CheckUsedDeclsDecl(p, usedDecls, false);
            }
            CheckUsedDeclsExpr(decl.Returns, usedDecls, false);
        }

        public void CheckUsedDeclsFunction(AstFuncDecl decl, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            foreach (var p in decl.Parameters)
            {
                CheckUsedDeclsDecl(p, usedDecls, false);
            }
            CheckUsedDeclsExpr(decl.Returns, usedDecls, false);

            bool checkBody = goDeep || HasOrIsGenericDecl(decl);
            if (decl.Body != null && checkBody)
            {
                CheckUsedDeclsBlockExpr(decl.Body, usedDecls, false);
            }
        }

        public void CheckUsedDeclsVar(AstVarDecl decl, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(decl.Type, usedDecls, goDeep);

            if (decl.Initializer != null)
            {
                CheckUsedDeclsExpr(decl.Initializer, usedDecls, goDeep);
            }
        }

        public void CheckUsedDeclsParam(AstParamDecl decl, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(decl.Type, usedDecls, goDeep);

            if (decl.DefaultValue != null)
            {
                CheckUsedDeclsExpr(decl.DefaultValue, usedDecls, goDeep);
            }
        }

        public void CheckUsedDeclsExpr(AstStatement stmt, List<AstDeclaration> usedDecls = null, bool goDeep = false)
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
                    CheckUsedDeclsDecl(varDecl, usedDecls, goDeep);
                    break;

                case AstBlockExpr blockExpr:
                    CheckUsedDeclsBlockExpr(blockExpr, usedDecls, goDeep);
                    break;
                case AstUnaryExpr unExpr:
                    CheckUsedDeclsUnaryExpr(unExpr, usedDecls, goDeep);
                    break;
                case AstBinaryExpr binExpr:
                    CheckUsedDeclsBinaryExpr(binExpr, usedDecls, goDeep);
                    break;
                case AstPointerExpr pointerExpr:
                    CheckUsedDeclsPointerExpr(pointerExpr, usedDecls, goDeep);
                    break;
                case AstAddressOfExpr addrExpr:
                    CheckUsedDeclsAddressOfExpr(addrExpr, usedDecls, goDeep);
                    break;
                case AstNewExpr newExpr:
                    CheckUsedDeclsNewExpr(newExpr, usedDecls, goDeep);
                    break;
                case AstArgumentExpr argumentExpr:
                    CheckUsedDeclsArgumentExpr(argumentExpr, usedDecls, goDeep);
                    break;
                case AstIdGenericExpr genExpr:
                    CheckUsedDeclsIdGenericExpr(genExpr, usedDecls, goDeep);
                    break;
                case AstIdExpr idExpr:
                    CheckUsedDeclsIdExpr(idExpr, usedDecls, goDeep);
                    break;
                case AstCallExpr callExpr:
                    CheckUsedDeclsCallExpr(callExpr, usedDecls, goDeep);
                    break;
                case AstCastExpr castExpr:
                    CheckUsedDeclsCastExpr(castExpr, usedDecls, goDeep);
                    break;
                case AstNestedExpr nestExpr:
                    CheckUsedDeclsNestedExpr(nestExpr, usedDecls, goDeep);
                    break;
                case AstDefaultExpr defaultExpr:
                    CheckUsedDeclsDefaultExpr(defaultExpr, usedDecls, goDeep);
                    break;
                case AstDefaultGenericExpr _: // no need
                    break;
                case AstEmptyStructExpr _: // no need
                    break;
                case AstArrayExpr arrayExpr:
                    CheckUsedDeclsArrayExpr(arrayExpr, usedDecls, goDeep);
                    break;
                case AstArrayCreateExpr arrayCreateExpr:
                    CheckUsedDeclsArrayCreateExpr(arrayCreateExpr, usedDecls, goDeep);
                    break;
                case AstArrayAccessExpr arrayAccExpr:
                    CheckUsedDeclsArrayAccessExpr(arrayAccExpr, usedDecls, goDeep);
                    break;
                case AstTernaryExpr ternaryExpr:
                    CheckUsedDeclsTernaryExpr(ternaryExpr, usedDecls, goDeep);
                    break;
                case AstCheckedExpr checkedExpr:
                    CheckUsedDeclsCheckedExpr(checkedExpr, usedDecls, goDeep);
                    break;
                case AstSATOfExpr satExpr:
                    CheckUsedDeclsSATExpr(satExpr, usedDecls, goDeep);
                    break;
                case AstEmptyExpr:
                    break;
                case AstLambdaExpr lambdaExpr:
                    CheckUsedDeclsLambdaExpr(lambdaExpr, usedDecls, goDeep);
                    break;
                case AstNullableExpr nullableExpr:
                    CheckUsedDeclsNullableExpr(nullableExpr, usedDecls, goDeep);
                    break;

                // statements
                case AstAssignStmt assignStmt:
                    CheckUsedDeclsAssignStmt(assignStmt, usedDecls, goDeep);
                    break;
                case AstForStmt forStmt:
                    CheckUsedDeclsForStmt(forStmt, usedDecls, goDeep);
                    break;
                case AstWhileStmt whileStmt:
                    CheckUsedDeclsWhileStmt(whileStmt, usedDecls, goDeep);
                    break;
                case AstDoWhileStmt doWhileStmt:
                    CheckUsedDeclsDoWhileStmt(doWhileStmt, usedDecls, goDeep);
                    break;
                case AstIfStmt ifStmt:
                    CheckUsedDeclsIfStmt(ifStmt, usedDecls, goDeep);
                    break;
                case AstSwitchStmt switchStmt:
                    CheckUsedDeclsSwitchStmt(switchStmt, usedDecls, goDeep);
                    break;
                case AstCaseStmt caseStmt:
                    CheckUsedDeclsCaseStmt(caseStmt, usedDecls, goDeep);
                    break;
                case AstBreakContStmt:
                    // nothing to do
                    break;
                case AstReturnStmt returnStmt:
                    CheckUsedDeclsReturnStmt(returnStmt, usedDecls, goDeep);
                    break;
                case AstAttributeStmt attrStmt:
                    CheckUsedDeclsAttributeStmt(attrStmt, usedDecls, goDeep);
                    break;
                case AstBaseCtorStmt baseStmt:
                    CheckUsedDeclsBaseCtorStmt(baseStmt, usedDecls, goDeep);
                    break;
                case AstConstrainStmt constrainStmt:
                    CheckUsedDeclsConstrainStmt(constrainStmt, usedDecls, goDeep);
                    break;
                case AstThrowStmt throwStmt:
                    CheckUsedDeclsThrowStmt(throwStmt, usedDecls, goDeep);
                    break;
                case AstTryCatchStmt tryCatchStmt:
                    CheckUsedDeclsTryCatchStmt(tryCatchStmt, usedDecls, goDeep);
                    break;
                case AstCatchStmt catchStmt:
                    CheckUsedDeclsCatchStmt(catchStmt, usedDecls, goDeep);
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
                        _compiler.MessageHandler.ReportMessage(_postPreparer._currentSourceFile, stmt, [stmt.AAAName], ErrorCode.Get(CTEN.StmtNotImplemented));
                        break;
                    }
            }
        }

        private void CheckUsedDeclsBlockExpr(AstBlockExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            foreach (var stmt in expr.Statements)
            {
                if (stmt == null)
                    continue;
                if (stmt is AstFuncDecl)
                    continue; // skip nested funcs
                CheckUsedDeclsExpr(stmt, usedDecls, goDeep);
            }
        }

        private void CheckUsedDeclsUnaryExpr(AstUnaryExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(expr.SubExpr, usedDecls, goDeep);

            if (expr.ActualOperator is UserDefinedUnaryOperator userDef)
            {
                CheckUsedDeclsDecl(userDef.Function.Declaration, usedDecls, goDeep);
            }
        }

        private void CheckUsedDeclsBinaryExpr(AstBinaryExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(expr.Left, usedDecls, goDeep);
            CheckUsedDeclsExpr(expr.Right, usedDecls, goDeep);

            if (expr.ActualOperator is UserDefinedBinaryOperator userDef)
            {
                CheckUsedDeclsDecl(userDef.Function.Declaration, usedDecls, goDeep);
            }
        }

        private void CheckUsedDeclsPointerExpr(AstPointerExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(expr.SubExpression, usedDecls, goDeep);
        }

        private void CheckUsedDeclsAddressOfExpr(AstAddressOfExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(expr.SubExpression, usedDecls, goDeep);
        }

        private void CheckUsedDeclsNewExpr(AstNewExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(expr.TypeName, usedDecls, goDeep);
            foreach (var a in expr.Arguments)
            {
                CheckUsedDeclsArgumentExpr(a, usedDecls, goDeep);
            }
            CheckUsedDeclsDecl(expr.ConstructorSymbol.Decl, usedDecls, goDeep);

            // if ctor is used then dtor is also has to be used
            var parent = expr.ConstructorSymbol.Decl.ContainingParent;
            var dtor = parent.GetDeclarations().FirstOrDefault(x => x is AstFuncDecl fnc && fnc.ClassFunctionType == ClassFunctionType.Dtor);
            CheckUsedDeclsDecl(dtor, usedDecls, goDeep);
        }

        private void CheckUsedDeclsArgumentExpr(AstArgumentExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(expr.Expr, usedDecls, goDeep);
        }

        private void CheckUsedDeclsIdGenericExpr(AstIdGenericExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            for (int i = 0; i < expr.GenericRealTypes.Count; ++i)
            {
                CheckUsedDeclsExpr(expr.GenericRealTypes[i], usedDecls, goDeep);
            }

            if (expr.FindSymbol == null)
                return;

            var decl = (expr.FindSymbol as DeclSymbol).Decl;
            if ((HasOrIsGenericDecl(decl) || !decl.IsImported || goDeep) && usedDecls == null)
            {
                CheckUsedDeclsDecl(decl, usedDecls, true);
            }
            else
            {
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
            }
        }

        private void CheckUsedDeclsIdExpr(AstIdExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            if (expr.FindSymbol == null)
                return;

            var decl = (expr.FindSymbol as DeclSymbol).Decl;
            if ((HasOrIsGenericDecl(decl) || !decl.IsImported || goDeep) && usedDecls == null)
            {
                CheckUsedDeclsDecl(decl, usedDecls, true);
            }
            else
            {
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
            }
        }

        private void CheckUsedDeclsCallExpr(AstCallExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            if (expr.TypeOrObjectName != null)
            {
                CheckUsedDeclsExpr(expr.TypeOrObjectName, usedDecls, goDeep);
            }

            CheckUsedDeclsExpr(expr.FuncName, usedDecls, goDeep);

            // if it is a property function call
            // then set the orig property to be used
            if (expr.FuncName.OutType is FunctionType fnc && fnc.Declaration.IsPropertyFunction)
            {
                CheckUsedDeclsDecl(fnc.Declaration.NormalParent as AstPropertyDecl, usedDecls, goDeep);
            }

            foreach (var a in expr.Arguments)
            {
                CheckUsedDeclsArgumentExpr(a, usedDecls, goDeep);
            }
        }

        private void CheckUsedDeclsCastExpr(AstCastExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(expr.TypeExpr, usedDecls, goDeep);
            CheckUsedDeclsExpr(expr.SubExpression, usedDecls, goDeep);
        }

        private void CheckUsedDeclsNestedExpr(AstNestedExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(expr.LeftPart, usedDecls, goDeep);
            CheckUsedDeclsExpr(expr.RightPart, usedDecls, goDeep);
        }

        private void CheckUsedDeclsDefaultExpr(AstDefaultExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            if (expr.TypeForDefault != null)
                CheckUsedDeclsExpr(expr.TypeForDefault, usedDecls, goDeep);
        }

        private void CheckUsedDeclsArrayExpr(AstArrayExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(expr.SubExpression, usedDecls, goDeep);
        }

        private void CheckUsedDeclsArrayCreateExpr(AstArrayCreateExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(expr.TypeName, usedDecls, goDeep);
            foreach (var s in expr.SizeExprs)
            {
                CheckUsedDeclsExpr(s, usedDecls, goDeep);
            }
            foreach (var e in expr.Elements)
            {
                CheckUsedDeclsExpr(e, usedDecls, goDeep);
            }
        }

        private void CheckUsedDeclsArrayAccessExpr(AstArrayAccessExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(expr.ObjectName, usedDecls, goDeep);
            CheckUsedDeclsExpr(expr.ParameterExpr, usedDecls, goDeep);
            if (expr.IndexerDecl != null)
                CheckUsedDeclsDecl(expr.IndexerDecl, usedDecls, goDeep);
        }

        private void CheckUsedDeclsTernaryExpr(AstTernaryExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(expr.Condition, usedDecls, goDeep);
            CheckUsedDeclsExpr(expr.TrueExpr, usedDecls, goDeep);
            CheckUsedDeclsExpr(expr.FalseExpr, usedDecls, goDeep);
        }

        private void CheckUsedDeclsCheckedExpr(AstCheckedExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(expr.SubExpression, usedDecls, goDeep);
        }

        private void CheckUsedDeclsSATExpr(AstSATOfExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            if (expr.TargetType != null)
                CheckUsedDeclsExpr(expr.TargetType, usedDecls, goDeep);
        }

        private void CheckUsedDeclsLambdaExpr(AstLambdaExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            foreach (var p in expr.Parameters)
            {
                CheckUsedDeclsDecl(p, usedDecls, goDeep);
            }
            CheckUsedDeclsExpr(expr.Returns, usedDecls, goDeep);

            if (expr.Body != null)
            {
                CheckUsedDeclsBlockExpr(expr.Body, usedDecls, goDeep);
            }
        }

        private void CheckUsedDeclsNullableExpr(AstNullableExpr expr, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(expr.SubExpression, usedDecls, goDeep);
        }

        private void CheckUsedDeclsAssignStmt(AstAssignStmt stmt, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsNestedExpr(stmt.Target, usedDecls, goDeep);
            CheckUsedDeclsExpr(stmt.Value, usedDecls, goDeep);
        }

        private void CheckUsedDeclsForStmt(AstForStmt stmt, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(stmt.FirstArgument, usedDecls, goDeep);
            CheckUsedDeclsExpr(stmt.SecondArgument, usedDecls, goDeep);
            CheckUsedDeclsExpr(stmt.ThirdArgument, usedDecls, goDeep);

            CheckUsedDeclsExpr(stmt.ForeachArgument, usedDecls, goDeep);
            CheckUsedDeclsExpr(stmt.ForeachGetEnumeratorVar, usedDecls, goDeep);
            CheckUsedDeclsExpr(stmt.ForeachMoveNextCall, usedDecls, goDeep);
            CheckUsedDeclsExpr(stmt.ForeachCurrentAssign, usedDecls, goDeep);

            CheckUsedDeclsBlockExpr(stmt.Body, usedDecls, goDeep);
        }

        private void CheckUsedDeclsWhileStmt(AstWhileStmt stmt, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(stmt.Condition, usedDecls, goDeep);

            CheckUsedDeclsBlockExpr(stmt.Body, usedDecls, goDeep);
        }

        private void CheckUsedDeclsDoWhileStmt(AstDoWhileStmt stmt, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(stmt.Condition, usedDecls, goDeep);

            CheckUsedDeclsBlockExpr(stmt.Body, usedDecls, goDeep);
        }

        private void CheckUsedDeclsIfStmt(AstIfStmt stmt, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(stmt.Condition, usedDecls, goDeep);

            CheckUsedDeclsBlockExpr(stmt.BodyTrue, usedDecls, goDeep);
            if (stmt.BodyFalse != null)
                CheckUsedDeclsBlockExpr(stmt.BodyFalse, usedDecls, goDeep);
        }

        private void CheckUsedDeclsSwitchStmt(AstSwitchStmt stmt, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(stmt.SubExpression, usedDecls, goDeep);

            foreach (var c in stmt.Cases)
            {
                CheckUsedDeclsExpr(c, usedDecls, goDeep);
            }
        }

        private void CheckUsedDeclsCaseStmt(AstCaseStmt stmt, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(stmt.Pattern, usedDecls, goDeep);

            CheckUsedDeclsExpr(stmt.Body, usedDecls, goDeep);
        }

        private void CheckUsedDeclsReturnStmt(AstReturnStmt stmt, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(stmt.ReturnExpression, usedDecls, goDeep);
        }

        private void CheckUsedDeclsAttributeStmt(AstAttributeStmt stmt, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(stmt.AttributeName, usedDecls, goDeep);
            foreach (var s in stmt.Arguments)
                CheckUsedDeclsArgumentExpr(s, usedDecls, goDeep);
        }

        private void CheckUsedDeclsBaseCtorStmt(AstBaseCtorStmt stmt, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(stmt.ThisArgument, usedDecls, goDeep);
            foreach (var a in stmt.Arguments)
            {
                CheckUsedDeclsExpr(a, usedDecls, goDeep);
            }
        }

        private void CheckUsedDeclsConstrainStmt(AstConstrainStmt stmt, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            if (stmt.Expr != null)
                CheckUsedDeclsExpr(stmt.Expr, usedDecls, goDeep);
        }

        private void CheckUsedDeclsThrowStmt(AstThrowStmt stmt, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(stmt.ThrowExpression, usedDecls, goDeep);
        }

        private void CheckUsedDeclsTryCatchStmt(AstTryCatchStmt stmt, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(stmt.TryBlock, usedDecls, goDeep);
            foreach (var c in stmt.CatchBlocks)
                CheckUsedDeclsExpr(c, usedDecls, goDeep);
            if (stmt.FinallyBlock != null)
                CheckUsedDeclsExpr(stmt.FinallyBlock, usedDecls, goDeep);
        }

        private void CheckUsedDeclsCatchStmt(AstCatchStmt stmt, List<AstDeclaration> usedDecls = null, bool goDeep = false)
        {
            CheckUsedDeclsExpr(stmt.CatchBlock, usedDecls, goDeep);
            CheckUsedDeclsDecl(stmt.CatchParam, usedDecls, goDeep);
        }

        private bool HasOrIsGenericDecl(AstDeclaration decl, bool checkParent = true, bool checkNestParent = true)
        {
            if (decl.HasGenericTypes || decl.IsImplOfGeneric)
                return true;
            if (checkParent && decl.ContainingParent != null && (decl.ContainingParent.HasGenericTypes || decl.ContainingParent.IsImplOfGeneric))
                return true;
            if (checkNestParent && decl.IsNestedDecl && (decl.ParentDecl.HasGenericTypes || decl.ParentDecl.IsImplOfGeneric))
                return true;
            return false;
        }
    }
}
