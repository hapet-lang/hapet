using System.Data;
using System.Diagnostics;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Scoping;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareScoping()
        {
            foreach (var (_, file) in _compiler.GetFiles())
            {
                PostPrepareFileScoping(file);
            }
        }

        public void PostPrepareFileScoping(ProgramFile file)
        {
            _currentSourceFile = file;
            foreach (var stmt in file.Statements)
            {
                stmt.Scope = file.NamespaceScope;

                if (stmt is not AstDeclaration decl)
                    continue;

                PostPrepareDeclScoping(decl);
            }
        }

        public void PostPrepareDeclScoping(AstDeclaration stmt)
        {
            _currentParentStack.AddParent(stmt);
            if (stmt is AstClassDecl classDecl)
            {
                PostPrepareClassScoping(classDecl);
            }
            else if (stmt is AstStructDecl structDecl)
            {
                PostPrepareStructScoping(structDecl);
            }
            else if (stmt is AstEnumDecl enumDecl)
            {
                PostPrepareEnumScoping(enumDecl);
            }
            else if (stmt is AstDelegateDecl delegateDecl)
            {
                PostPrepareDelegateScoping(delegateDecl);
            }
            else if (stmt is AstFuncDecl funcDecl)
            {
                PostPrepareFunctionScoping(funcDecl);
            }
            else if (stmt is AstPropertyDecl propDecl)
            {
                if (propDecl.GetBlock != null)
                {
                    SetScopeAndParent(propDecl.GetBlock, propDecl);
                    PostPrepareExprScoping(propDecl.GetBlock);
                }
                if (propDecl.SetBlock != null)
                {
                    SetScopeAndParent(propDecl.SetBlock, propDecl);
                    PostPrepareExprScoping(propDecl.SetBlock);
                }

                // scoping generic shite
                foreach (var c in propDecl.GenericConstrains)
                {
                    foreach (var currentC in c.Value)
                    {
                        // subscoping generic type constrains
                        SetScopeAndParent(currentC, propDecl, propDecl.SubScope);
                        PostPrepareExprScoping(currentC);
                    }
                }

                // scoping indexer parameter
                if (propDecl is AstIndexerDecl indDecl)
                {
                    SetScopeAndParent(indDecl.IndexerParameter, propDecl);
                    PostPrepareExprScoping(indDecl.IndexerParameter);
                }
                PostPrepareVarScoping(propDecl, true);
            }
            _currentParentStack.RemoveParent();
        }

        private void PostPrepareClassScoping(AstClassDecl classDecl)
        {
            classDecl.SourceFile = _currentSourceFile;
            var classScope = new Scope($"{classDecl.Name.Name}_scope", classDecl.Scope) { GlobalScope = _compiler.GlobalScope };
            classDecl.SubScope = classScope; // setting the sub scope

            SetScopeAndParent(classDecl.Name, classDecl);
            PostPrepareExprScoping(classDecl.Name);

            // scoping class attrs
            foreach (var a in classDecl.Attributes)
            {
                SetScopeAndParent(a, classDecl);
                PostPrepareExprScoping(a);
            }

            // Scoping inheritance
            foreach (var inh in classDecl.InheritedFrom)
            {
                SetScopeAndParent(inh, classDecl);
                PostPrepareExprScoping(inh);
            }

            // scoping generic shite
            foreach (var c in classDecl.GenericConstrains)
            {
                foreach (var currentC in c.Value)
                {
                    // subscoping generic type constrains
                    SetScopeAndParent(currentC, classDecl, classDecl.SubScope);
                    PostPrepareExprScoping(currentC);
                }
            }

            foreach (var decl in classDecl.Declarations)
            {
                if (decl == null)
                    continue;

                SetScopeAndParent(decl, classDecl, classScope);

                if (decl is AstFuncDecl funcDecl)
                {
                    /// defining in a scope is done in <see cref="PostPrepareFunctionInference"/>

                    PostPrepareFunctionScoping(funcDecl);
                }
                else if (decl is AstPropertyDecl propDecl) // property
                {
                    if (propDecl.GetBlock != null)
                    {
                        SetScopeAndParent(propDecl.GetBlock, propDecl);
                        PostPrepareExprScoping(propDecl.GetBlock);
                    }
                    if (propDecl.SetBlock != null)
                    {
                        SetScopeAndParent(propDecl.SetBlock, propDecl);
                        PostPrepareExprScoping(propDecl.SetBlock);
                    }

                    // scoping indexer parameter
                    if (propDecl is AstIndexerDecl indDecl)
                    {
                        SetScopeAndParent(indDecl.IndexerParameter, propDecl);
                        PostPrepareParamScoping(indDecl.IndexerParameter);
                    }

                    // setting already defined to 'true' because of some shite with access types
                    PostPrepareVarScoping(propDecl, true);
                }
                else if (decl is AstVarDecl fieldDecl) // field 
                {
                    // setting already defined to 'true' because of some shite with access types
                    PostPrepareVarScoping(fieldDecl, true);
                }
                else
                {
                    PostPrepareDeclScoping(decl);
                }
            }
        }

        private void PostPrepareStructScoping(AstStructDecl structDecl)
        {
            structDecl.SourceFile = _currentSourceFile;
            var structScope = new Scope($"{structDecl.Name.Name}_scope", structDecl.Scope) { GlobalScope = _compiler.GlobalScope };
            structDecl.SubScope = structScope;

            SetScopeAndParent(structDecl.Name, structDecl);
            PostPrepareExprScoping(structDecl.Name);

            // scoping struct attrs
            foreach (var a in structDecl.Attributes)
            {
                SetScopeAndParent(a, structDecl);
                PostPrepareExprScoping(a);
            }

            // Scoping inheritance
            foreach (var inh in structDecl.InheritedFrom)
            {
                SetScopeAndParent(inh, structDecl);
                PostPrepareExprScoping(inh);
            }

            // scoping generic shite
            foreach (var c in structDecl.GenericConstrains)
            {
                foreach (var currentC in c.Value)
                {
                    // subscoping generic type constrains
                    SetScopeAndParent(currentC, structDecl, structDecl.SubScope);
                    PostPrepareExprScoping(currentC);
                }
            }

            foreach (var decl in structDecl.Declarations)
            {
                if (decl == null)
                    continue;

                SetScopeAndParent(decl, structDecl, structScope);

                if (decl is AstFuncDecl funcDecl)
                {
                    /// defining in a scope is done in <see cref="PostPrepareFunctionInference"/>

                    PostPrepareFunctionScoping(funcDecl);
                }
                else if (decl is AstPropertyDecl propDecl) // property
                {
                    if (propDecl.GetBlock != null)
                    {
                        SetScopeAndParent(propDecl.GetBlock, propDecl);
                        PostPrepareExprScoping(propDecl.GetBlock);
                    }
                    if (propDecl.SetBlock != null)
                    {
                        SetScopeAndParent(propDecl.SetBlock, propDecl);
                        PostPrepareExprScoping(propDecl.SetBlock);
                    }

                    // scoping indexer parameter
                    if (propDecl is AstIndexerDecl indDecl)
                    {
                        // WARN!!!! do not set the scope the same as delegate scope because its params would be visible in class or smth
                        // creating a Scope in which the params would be
                        var paramsBlockScope = new Scope($"params_{indDecl.Name.Name}_scope", indDecl.Scope) { GlobalScope = _compiler.GlobalScope };

                        SetScopeAndParent(indDecl.IndexerParameter, propDecl, paramsBlockScope);
                        PostPrepareParamScoping(indDecl.IndexerParameter);
                    }

                    // setting already defined to 'true' because of some shite with access types
                    PostPrepareVarScoping(propDecl, true);
                }
                else if (decl is AstVarDecl fieldDecl) // field 
                {
                    // setting already defined to 'true' because of some shite with access types
                    PostPrepareVarScoping(fieldDecl, true);
                }
                else
                {
                    PostPrepareDeclScoping(decl);
                }
            }
        }

        private void PostPrepareEnumScoping(AstEnumDecl enumDecl)
        {
            enumDecl.SourceFile = _currentSourceFile;
            var enumScope = new Scope($"{enumDecl.Name.Name}_scope", enumDecl.Scope) { GlobalScope = _compiler.GlobalScope };
            enumDecl.SubScope = enumScope;

            SetScopeAndParent(enumDecl.Name, enumDecl);
            PostPrepareExprScoping(enumDecl.Name);

            // scoping struct attrs
            foreach (var a in enumDecl.Attributes)
            {
                SetScopeAndParent(a, enumDecl);
                PostPrepareExprScoping(a);
            }

            if (enumDecl.InheritedType != null)
            {
                SetScopeAndParent(enumDecl.InheritedType, enumDecl);
                PostPrepareExprScoping(enumDecl.InheritedType);
            }

            foreach (var decl in enumDecl.Declarations)
            {
                SetScopeAndParent(decl, enumDecl, enumScope);

                // setting already defined to 'true' because of some shite with access types
                PostPrepareVarScoping(decl, true);
            }
        }

        private void PostPrepareDelegateScoping(AstDelegateDecl delegateDecl)
        {
            SetScopeAndParent(delegateDecl.Name, delegateDecl);
            PostPrepareExprScoping(delegateDecl.Name);

            // required for generics at least
            var delegateScope = new Scope($"{delegateDecl.Name.Name}_scope", delegateDecl.Scope) { GlobalScope = _compiler.GlobalScope };
            delegateDecl.SubScope = delegateScope;

            // scoping delegate attrs
            foreach (var a in delegateDecl.Attributes)
            {
                SetScopeAndParent(a, delegateDecl);
                PostPrepareExprScoping(a);
            }

            // scoping generic shite
            foreach (var c in delegateDecl.GenericConstrains)
            {
                foreach (var currentC in c.Value)
                {
                    // subscoping generic type constrains
                    SetScopeAndParent(currentC, delegateDecl, delegateDecl.SubScope);
                    PostPrepareExprScoping(currentC);
                }
            }

            // WARN!!!! do not set the scope the same as delegate scope because its params would be visible in class or smth
            // creating a Scope in which the params would be
            var paramsBlockScope = new Scope($"params_{delegateDecl.Name.Name}_scope", delegateDecl.Scope) { GlobalScope = _compiler.GlobalScope };

            // defining parameters in the delegate scope
            foreach (var p in delegateDecl.Parameters)
            {
                // settings the block scope to the parameters (so they are in the scope of the block)
                SetScopeAndParent(p, delegateDecl, paramsBlockScope);
                PostPrepareParamScoping(p);
            }
            // return type is the same
            SetScopeAndParent(delegateDecl.Returns, delegateDecl, paramsBlockScope);
            PostPrepareExprScoping(delegateDecl.Returns);
        }

        private void PostPrepareFunctionScoping(AstFuncDecl funcDecl)
        {
            _currentParentStack.AddParent(funcDecl);

            SetScopeAndParent(funcDecl.Name, funcDecl);
            PostPrepareExprScoping(funcDecl.Name);

            // scoping func attrs
            foreach (var a in funcDecl.Attributes)
            {
                SetScopeAndParent(a, funcDecl);
                PostPrepareExprScoping(a);
            }

            Scope blockScope;
            if (funcDecl.Body != null)
            {
                // body scope is the same
                SetScopeAndParent(funcDecl.Body, funcDecl);
                blockScope = PostPrepareBlockScoping(funcDecl.Body, $"{funcDecl.Name.Name}_scope");
            }
            else
            {
                // WARN!!!! do not set the scope the same as func scope because its params would be visible in class or smth
                // creating a Scope in which the params would be
                blockScope = new Scope($"params_{funcDecl.Name.Name}_scope", funcDecl.Scope) { GlobalScope = _compiler.GlobalScope };
            }
            funcDecl.SubScope = blockScope;

            // scoping generic shite
            foreach (var c in funcDecl.GenericConstrains)
            {
                foreach (var currentC in c.Value)
                {
                    // subscoping generic type constrains
                    SetScopeAndParent(currentC, funcDecl, funcDecl.SubScope);
                    PostPrepareExprScoping(currentC);
                }
            }

            // defining parameters in the func scope
            foreach (var p in funcDecl.Parameters)
            {
                // settings the block scope to the parameters (so they are in the scope of the block)
                SetScopeAndParent(p, funcDecl, blockScope);
                PostPrepareParamScoping(p);

                // scoping param attrs
                foreach (var a in p.Attributes)
                {
                    SetScopeAndParent(a, p);
                    PostPrepareExprScoping(a);
                }
            }
            // return type is the same
            SetScopeAndParent(funcDecl.Returns, funcDecl, blockScope);
            PostPrepareExprScoping(funcDecl.Returns);

            // base ctor call scoping
            if (funcDecl.BaseCtorCall != null)
            {
                SetScopeAndParent(funcDecl.BaseCtorCall, funcDecl, blockScope);
                PostPrepareExprScoping(funcDecl.BaseCtorCall);
            }
            // this ctor call scoping
            if (funcDecl.ThisCtorCall != null)
            {
                SetScopeAndParent(funcDecl.ThisCtorCall, funcDecl, blockScope);
                PostPrepareExprScoping(funcDecl.ThisCtorCall);
            }

            _currentParentStack.RemoveParent();
        }

        /// <summary>
        /// Post preparation of varDecl
        /// </summary>
        /// <param name="varDecl">The var decl</param>
        /// <param name="alreadyDefined">It could be already defined for example by classDecl (because of public/private shite)</param>
        private void PostPrepareVarScoping(AstVarDecl varDecl, bool doNotDefine = false)
        {
            if (varDecl.Name != null)
                SetScopeAndParent(varDecl.Name, varDecl);
            if (varDecl.Type != null)
                SetScopeAndParent(varDecl.Type, varDecl);

            // scoping var attrs
            foreach (var a in varDecl.Attributes)
            {
                SetScopeAndParent(a, varDecl);
                PostPrepareExprScoping(a);
            }

            if (varDecl.Name != null)
                PostPrepareExprScoping(varDecl.Name);
            if (varDecl.Type != null)
                PostPrepareExprScoping(varDecl.Type);

            if (varDecl.Initializer != null)
            {
                SetScopeAndParent(varDecl.Initializer, varDecl);
                PostPrepareExprScoping(varDecl.Initializer);
            }
            // define it in the scope if it is not yet
            if (!doNotDefine && varDecl.Name != null)
            {
                varDecl.Scope.DefineDeclSymbol(varDecl.Name, varDecl);
            }
        }

        private void PostPrepareParamScoping(AstParamDecl paramDecl)
        {
            // it can be null when the func is only declared but not defined!
            if (paramDecl.Name != null)
                SetScopeAndParent(paramDecl.Name, paramDecl);
            // it can be null when param is 'arglist'
            if (paramDecl.Type != null)
                SetScopeAndParent(paramDecl.Type, paramDecl);

            // scoping param attrs
            foreach (var a in paramDecl.Attributes)
            {
                SetScopeAndParent(a, paramDecl);
                PostPrepareExprScoping(a);
            }

            if (paramDecl.Type != null)
                PostPrepareExprScoping(paramDecl.Type);
            if (paramDecl.DefaultValue != null)
            {
                // preparing scopes of default values if they exist
                SetScopeAndParent(paramDecl.DefaultValue, paramDecl);
                PostPrepareExprScoping(paramDecl.DefaultValue);
            }
            // it can be null when the func is only declared but not defined!
            if (paramDecl.Name != null)
            {
                // defining the symbol in the scope so it can be easily found
                paramDecl.Scope.DefineDeclSymbol(paramDecl.Name, paramDecl);
            }
        }

        public void PostPrepareExprScoping(AstStatement expr)
        {
            switch (expr)
            {
                // special case at least for 'for' loop
                // when 'for (int i = 0;...)' where 'int i' 
                // would not be handled by blockExpr
                case AstVarDecl varDecl:
                    PostPrepareVarScoping(varDecl);
                    break;

                case AstBlockExpr blockExpr:
                    PostPrepareBlockScoping(blockExpr);
                    break;
                case AstUnaryExpr unExpr:
                    PostPrepareUnaryExprScoping(unExpr);
                    break;
                case AstBinaryExpr binExpr:
                    PostPrepareBinaryExprScoping(binExpr);
                    break;
                case AstPointerExpr pointerExpr:
                    PostPreparePointerExprScoping(pointerExpr);
                    break;
                case AstAddressOfExpr addrExpr:
                    PostPrepareAddressOfExprScoping(addrExpr);
                    break;
                case AstNewExpr newExpr:
                    PostPrepareNewExprScoping(newExpr);
                    break;
                case AstArgumentExpr argumentExpr:
                    PostPrepareArgumentExprScoping(argumentExpr);
                    break;
                case AstIdGenericExpr genExpr:
                    PostPrepareIdGenericExprScoping(genExpr);
                    break;
                case AstIdExpr idExpr:
                    PostPrepareIdExprScoping(idExpr);
                    break;
                case AstCallExpr callExpr:
                    PostPrepareCallExprScoping(callExpr);
                    break;
                case AstCastExpr castExpr:
                    PostPrepareCastExprScoping(castExpr);
                    break;
                case AstNestedExpr nestExpr:
                    PostPrepareNestedExprScoping(nestExpr);
                    break;
                case AstDefaultExpr defaultExpr:
                    PostPrepareDefaultExprScoping(defaultExpr);
                    break;
                case AstDefaultGenericExpr _: // no need to scope anything
                    break;
                case AstEmptyStructExpr _: // no need
                    break;
                case AstArrayExpr arrayExpr:
                    PostPrepareArrayExprScoping(arrayExpr);
                    break;
                case AstArrayCreateExpr arrayCreateExpr:
                    PostPrepareArrayCreateExprScoping(arrayCreateExpr);
                    break;
                case AstArrayAccessExpr arrayAccExpr:
                    PostPrepareArrayAccessExprScoping(arrayAccExpr);
                    break;
                case AstTernaryExpr ternaryExpr:
                    PostPrepareTernaryExprScoping(ternaryExpr);
                    break;
                case AstCheckedExpr checkedExpr:
                    PostPrepareCheckedExprScoping(checkedExpr);
                    break;
                case AstSATOfExpr satExpr:
                    PostPrepareSATExprScoping(satExpr);
                    break;
                case AstEmptyExpr:
                    break;
                case AstLambdaExpr lambdaExpr:
                    PostPrepareLambdaExprScoping(lambdaExpr);
                    break;
                case AstNullableExpr nullableExpr:
                    PostPrepareNullableExprScoping(nullableExpr);
                    break;

                // statements
                case AstAssignStmt assignStmt:
                    PostPrepareAssignStmtScoping(assignStmt);
                    break;
                case AstForStmt forStmt:
                    PostPrepareForStmtScoping(forStmt);
                    break;
                case AstWhileStmt whileStmt:
                    PostPrepareWhileStmtScoping(whileStmt);
                    break;
                case AstDoWhileStmt doWhileStmt:
                    PostPrepareDoWhileStmtScoping(doWhileStmt);
                    break;
                case AstIfStmt ifStmt:
                    PostPrepareIfStmtScoping(ifStmt);
                    break;
                case AstSwitchStmt switchStmt:
                    PostPrepareSwitchStmtScoping(switchStmt);
                    break;
                case AstCaseStmt caseStmt:
                    PostPrepareCaseStmtScoping(caseStmt);
                    break;
                case AstBreakContStmt:
                    // nothing to do
                    break;
                case AstReturnStmt returnStmt:
                    PostPrepareReturnStmtScoping(returnStmt);
                    break;
                case AstAttributeStmt attrStmt:
                    PostPrepareAttributeStmtScoping(attrStmt);
                    break;
                case AstBaseCtorStmt baseStmt:
                    PostPrepareBaseCtorStmtScoping(baseStmt);
                    break;
                case AstConstrainStmt constrainStmt:
                    PostPrepareConstrainScoping(constrainStmt);
                    break;
                case AstThrowStmt throwStmt:
                    PostPrepareThrowScoping(throwStmt);
                    break;
                case AstTryCatchStmt tryCatchStmt:
                    PostPrepareTryCatchScoping(tryCatchStmt);
                    break;
                case AstCatchStmt сatchStmt:
                    PostPrepareCatchScoping(сatchStmt);
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
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, expr, [expr.AAAName], ErrorCode.Get(CTEN.StmtNotImplemented));
                        break;
                    }
            }
        }

        private static ulong _blockCounter = 0;
        private Scope PostPrepareBlockScoping(AstBlockExpr blockExpr, string scopename = "")
        {
            if (string.IsNullOrWhiteSpace(scopename))
                scopename = $"block_{_blockCounter++}_scope";

            var blockScope = new Scope(scopename, blockExpr.Scope) { GlobalScope = _compiler.GlobalScope };
            blockExpr.SubScope = blockScope; // setting the sub scope

            foreach (var stmt in blockExpr.Statements)
            {
                if (stmt == null)
                    continue;

                SetScopeAndParent(stmt, blockExpr, blockScope);

                // special check for nested function
                if (stmt is AstFuncDecl func)
                {
                    var parentFunction = _currentParentStack.GetNearestParentFunc();
                    func.ContainingParent = parentFunction;
                    PostPrepareFunctionScoping(func);
                }
                else
                {
                    PostPrepareExprScoping(stmt);
                }
            }

            return blockScope;
        }

        private void PostPrepareUnaryExprScoping(AstUnaryExpr unExpr)
        {
            SetScopeAndParent(unExpr.SubExpr, unExpr);
            
            PostPrepareExprScoping(unExpr.SubExpr); 
        }

        private void PostPrepareBinaryExprScoping(AstBinaryExpr binExpr)
        {
            // these scopes are probably the same for the bin expr parts
            if (binExpr.Left != null)
                SetScopeAndParent(binExpr.Left, binExpr);
            if (binExpr.Right != null)
                SetScopeAndParent(binExpr.Right, binExpr);

            if (binExpr.Left != null)
                PostPrepareExprScoping(binExpr.Left);
            if (binExpr.Right != null)
                PostPrepareExprScoping(binExpr.Right);
        }

        private void PostPreparePointerExprScoping(AstPointerExpr pointerExpr)
        {
            SetScopeAndParent(pointerExpr.SubExpression, pointerExpr);
            PostPrepareExprScoping(pointerExpr.SubExpression);
        }

        private void PostPrepareAddressOfExprScoping(AstAddressOfExpr addrExpr)
        {
            SetScopeAndParent(addrExpr.SubExpression, addrExpr);
            PostPrepareExprScoping(addrExpr.SubExpression);
        }

        private void PostPrepareNewExprScoping(AstNewExpr newExpr)
        {
            SetScopeAndParent(newExpr.TypeName, newExpr);
            PostPrepareExprScoping(newExpr.TypeName);
            foreach (var a in newExpr.Arguments)
            {
                SetScopeAndParent(a, newExpr);
                PostPrepareExprScoping(a);
            }
        }

        private void PostPrepareArgumentExprScoping(AstArgumentExpr argumentExpr)
        {
            if (argumentExpr.Expr != null)
            {
                SetScopeAndParent(argumentExpr.Expr, argumentExpr);
                PostPrepareExprScoping(argumentExpr.Expr);
            }
            if (argumentExpr.Name != null)
            {
                SetScopeAndParent(argumentExpr.Name, argumentExpr);
                PostPrepareExprScoping(argumentExpr.Name);
            }
        }

        private void PostPrepareIdGenericExprScoping(AstIdGenericExpr genExpr)
        {
            foreach (var g in genExpr.GenericRealTypes)
            {
                SetScopeAndParent(g, genExpr);
                PostPrepareExprScoping(g);
            }
        }

        private void PostPrepareIdExprScoping(AstIdExpr idExpr)
        {
            if (idExpr.AdditionalData != null)
            {
                SetScopeAndParent(idExpr.AdditionalData, idExpr);
                PostPrepareExprScoping(idExpr.AdditionalData);
            }
        }

        public void PostPrepareCallExprScoping(AstCallExpr callExpr)
        {
            // usually when in the same class
            if (callExpr.TypeOrObjectName != null)
            {
                SetScopeAndParent(callExpr.TypeOrObjectName, callExpr);
                PostPrepareExprScoping(callExpr.TypeOrObjectName);
            }

            SetScopeAndParent(callExpr.FuncName, callExpr);
            PostPrepareExprScoping(callExpr.FuncName);
            foreach (var a in callExpr.Arguments)
            {
                SetScopeAndParent(a, callExpr);
                PostPrepareExprScoping(a);
            }
        }

        private void PostPrepareCastExprScoping(AstCastExpr castExpr)
        {
            SetScopeAndParent(castExpr.SubExpression, castExpr);
            PostPrepareExprScoping(castExpr.SubExpression);

            if (castExpr.TypeExpr != null)
            {
                SetScopeAndParent(castExpr.TypeExpr, castExpr);
                PostPrepareExprScoping(castExpr.TypeExpr);
            }
        }

        private void PostPrepareNestedExprScoping(AstNestedExpr nestExpr)
        {
            // could be null on wrong parsing
            if (nestExpr.RightPart != null)
            {
                SetScopeAndParent(nestExpr.RightPart, nestExpr);
                PostPrepareExprScoping(nestExpr.RightPart);
            }
            if (nestExpr.LeftPart != null)
            {
                SetScopeAndParent(nestExpr.LeftPart, nestExpr);
                PostPrepareExprScoping(nestExpr.LeftPart);
            }
        }

        private void PostPrepareDefaultExprScoping(AstDefaultExpr defaultExpr)
        {
            if (defaultExpr.TypeForDefault != null)
            {
                SetScopeAndParent(defaultExpr.TypeForDefault, defaultExpr);
                PostPrepareExprScoping(defaultExpr.TypeForDefault);
            }
        }

        private void PostPrepareArrayExprScoping(AstArrayExpr arrayExpr)
        {
            SetScopeAndParent(arrayExpr.SubExpression, arrayExpr);
            PostPrepareExprScoping(arrayExpr.SubExpression);
        }

        private void PostPrepareArrayCreateExprScoping(AstArrayCreateExpr arrayExpr)
        {
            foreach (var sz in arrayExpr.SizeExprs)
            {
                SetScopeAndParent(sz, arrayExpr);
                PostPrepareExprScoping(sz);
            }
            SetScopeAndParent(arrayExpr.TypeName, arrayExpr);
            PostPrepareExprScoping(arrayExpr.TypeName);
            foreach (var e in arrayExpr.Elements)
            {
                SetScopeAndParent(e, arrayExpr);
                PostPrepareExprScoping(e);
            }
        }

        private void PostPrepareArrayAccessExprScoping(AstArrayAccessExpr arrayAccExpr)
        {
            if (arrayAccExpr.ParameterExpr != null)
            {
                SetScopeAndParent(arrayAccExpr.ParameterExpr, arrayAccExpr);
                PostPrepareExprScoping(arrayAccExpr.ParameterExpr);
            }
            SetScopeAndParent(arrayAccExpr.ObjectName, arrayAccExpr);
            PostPrepareExprScoping(arrayAccExpr.ObjectName);
        }

        private void PostPrepareTernaryExprScoping(AstTernaryExpr ternaryExpr)
        {
            SetScopeAndParent(ternaryExpr.Condition, ternaryExpr);
            PostPrepareExprScoping(ternaryExpr.Condition);
            SetScopeAndParent(ternaryExpr.TrueExpr, ternaryExpr);
            PostPrepareExprScoping(ternaryExpr.TrueExpr);
            SetScopeAndParent(ternaryExpr.FalseExpr, ternaryExpr);
            PostPrepareExprScoping(ternaryExpr.FalseExpr);
        }

        private void PostPrepareCheckedExprScoping(AstCheckedExpr checkedExpr)
        {
            SetScopeAndParent(checkedExpr.SubExpression, checkedExpr);
            PostPrepareExprScoping(checkedExpr.SubExpression);
        }

        private void PostPrepareSATExprScoping(AstSATOfExpr satExpr)
        {
            SetScopeAndParent(satExpr.TargetType, satExpr);
            PostPrepareExprScoping(satExpr.TargetType);
        }

        private void PostPrepareLambdaExprScoping(AstLambdaExpr lambdaExpr)
        {
            Scope blockScope;
            if (lambdaExpr.Body != null)
            {
                // body scope is the same
                SetScopeAndParent(lambdaExpr.Body, lambdaExpr);
                blockScope = PostPrepareBlockScoping(lambdaExpr.Body, $"lambda_scope");
            }
            else
            {
                // WARN!!!! do not set the scope the same as func scope because its params would be visible in class or smth
                // creating a Scope in which the params would be
                blockScope = new Scope($"params_lambda_scope", lambdaExpr.Scope) { GlobalScope = _compiler.GlobalScope };
            }
            lambdaExpr.SubScope = blockScope;

            // defining parameters in the func scope
            foreach (var p in lambdaExpr.Parameters)
            {
                // settings the block scope to the parameters (so they are in the scope of the block)
                SetScopeAndParent(p, lambdaExpr, blockScope);
                PostPrepareParamScoping(p);
            }

            if (lambdaExpr.Returns != null)
            {
                // return type is the same
                SetScopeAndParent(lambdaExpr.Returns, lambdaExpr, blockScope);
                PostPrepareExprScoping(lambdaExpr.Returns);
            }
        }

        private void PostPrepareNullableExprScoping(AstNullableExpr expr)
        {
            SetScopeAndParent(expr.SubExpression, expr);
            PostPrepareExprScoping(expr.SubExpression);
        }

        // statements
        private void PostPrepareAssignStmtScoping(AstAssignStmt assignStmt)
        {
            SetScopeAndParent(assignStmt.Target, assignStmt);
            PostPrepareExprScoping(assignStmt.Target);
            if (assignStmt.Value != null)
            {
                SetScopeAndParent(assignStmt.Value, assignStmt);
                PostPrepareExprScoping(assignStmt.Value);
            }
        }

        private static ulong _forCounter = 0;
        private void PostPrepareForStmtScoping(AstForStmt forStmt)
        {
            SetScopeAndParent(forStmt.Body, forStmt);

            string scopename = $"for_{_forCounter++}_scope";
            var forScope = PostPrepareBlockScoping(forStmt.Body, scopename);

            if (forStmt.FirstArgument != null)
            {
                SetScopeAndParent(forStmt.FirstArgument, forStmt, forScope);
                PostPrepareExprScoping(forStmt.FirstArgument);
            }
            if (forStmt.SecondArgument != null)
            {
                SetScopeAndParent(forStmt.SecondArgument, forStmt, forScope);
                PostPrepareExprScoping(forStmt.SecondArgument);
            }
            if (forStmt.ThirdArgument != null)
            {
                SetScopeAndParent(forStmt.ThirdArgument, forStmt, forScope);
                PostPrepareExprScoping(forStmt.ThirdArgument);
            }
            if (forStmt.ForeachArgument != null)
            {
                SetScopeAndParent(forStmt.ForeachArgument, forStmt, forScope);
                PostPrepareExprScoping(forStmt.ForeachArgument);
            }
        }

        private static ulong _whileCounter = 0;
        private void PostPrepareWhileStmtScoping(AstWhileStmt whileStmt)
        {
            SetScopeAndParent(whileStmt.Body, whileStmt);

            string scopename = $"while_{_whileCounter++}_scope";
            var whileScope = PostPrepareBlockScoping(whileStmt.Body, scopename);

            if (whileStmt.Condition != null)
            {
                SetScopeAndParent(whileStmt.Condition, whileStmt, whileScope);
                PostPrepareExprScoping(whileStmt.Condition);
            }
        }

        private static ulong _doWhileCounter = 0;
        private void PostPrepareDoWhileStmtScoping(AstDoWhileStmt whileStmt)
        {
            SetScopeAndParent(whileStmt.Body, whileStmt);

            string scopename = $"do_while_{_doWhileCounter++}_scope";
            var whileScope = PostPrepareBlockScoping(whileStmt.Body, scopename);

            if (whileStmt.Condition != null)
            {
                SetScopeAndParent(whileStmt.Condition, whileStmt, whileScope);
                PostPrepareExprScoping(whileStmt.Condition);
            }
        }

        private static ulong _ifCounter = 0;
        private void PostPrepareIfStmtScoping(AstIfStmt ifStmt)
        {
            SetScopeAndParent(ifStmt.BodyTrue, ifStmt);
            if (ifStmt.BodyFalse != null)
                SetScopeAndParent(ifStmt.BodyFalse, ifStmt);

            string scopename = $"if_{_ifCounter}_scope";
            var ifScope = PostPrepareBlockScoping(ifStmt.BodyTrue, scopename);

            if (ifStmt.Condition != null)
            {
                SetScopeAndParent(ifStmt.Condition, ifStmt, ifScope);
                PostPrepareExprScoping(ifStmt.Condition);
            }

            string scopenameElse = $"else_{_ifCounter++}_scope";
            if (ifStmt.BodyFalse != null)
                PostPrepareBlockScoping(ifStmt.BodyFalse, scopenameElse);
        }

        private void PostPrepareSwitchStmtScoping(AstSwitchStmt switchStmt)
        {
            SetScopeAndParent(switchStmt.SubExpression, switchStmt);
            PostPrepareExprScoping(switchStmt.SubExpression);

            foreach (var cc in switchStmt.Cases)
            {
                SetScopeAndParent(cc, switchStmt);
                PostPrepareExprScoping(cc);
            }
        }

        private void PostPrepareCaseStmtScoping(AstCaseStmt caseStmt)
        {
            if (!caseStmt.IsDefaultCase)
            {
                SetScopeAndParent(caseStmt.Pattern, caseStmt);
                PostPrepareExprScoping(caseStmt.Pattern);
            }

            if (!caseStmt.IsFallingCase)
            {
                SetScopeAndParent(caseStmt.Body, caseStmt);
                PostPrepareExprScoping(caseStmt.Body);
            }
        }

        private void PostPrepareReturnStmtScoping(AstReturnStmt returnStmt)
        {
            if (returnStmt.ReturnExpression != null)
            {
                SetScopeAndParent(returnStmt.ReturnExpression, returnStmt);
                PostPrepareExprScoping(returnStmt.ReturnExpression);
            }
            if (returnStmt.WeakReturnStatement != null)
            {
                SetScopeAndParent(returnStmt.WeakReturnStatement, returnStmt);
                PostPrepareExprScoping(returnStmt.WeakReturnStatement);
            }
        }

        private void PostPrepareAttributeStmtScoping(AstAttributeStmt attrStmt)
        {
            SetScopeAndParent(attrStmt.AttributeName, attrStmt);
            PostPrepareExprScoping(attrStmt.AttributeName);
            foreach (var a in attrStmt.Arguments)
            {
                SetScopeAndParent(a, attrStmt);
                PostPrepareExprScoping(a);
            }
        }

        private void PostPrepareBaseCtorStmtScoping(AstBaseCtorStmt baseCtor)
        {
            foreach (var a in baseCtor.Arguments)
            {
                SetScopeAndParent(a, baseCtor);
                PostPrepareExprScoping(a);
            }
        }

        private void PostPrepareConstrainScoping(AstConstrainStmt constrainStmt)
        {
            SetScopeAndParent(constrainStmt.Expr, constrainStmt);
            PostPrepareExprScoping(constrainStmt.Expr);

            // go over additional 
            foreach (var a in constrainStmt.AdditionalExprs)
            {
                SetScopeAndParent(a, constrainStmt);
                PostPrepareExprScoping(a);
            }
        }

        private void PostPrepareThrowScoping(AstThrowStmt throwStmt)
        {
            if (throwStmt.ThrowExpression == null)
                return;
            SetScopeAndParent(throwStmt.ThrowExpression, throwStmt);
            PostPrepareExprScoping(throwStmt.ThrowExpression);
        }

        private void PostPrepareTryCatchScoping(AstTryCatchStmt stmt)
        {
            SetScopeAndParent(stmt.TryBlock, stmt);
            PostPrepareExprScoping(stmt.TryBlock);

            foreach (var c in stmt.CatchBlocks)
            {
                SetScopeAndParent(c, stmt);
                PostPrepareExprScoping(c);
            }

            if (stmt.FinallyBlock != null)
            {
                SetScopeAndParent(stmt.FinallyBlock, stmt);
                PostPrepareExprScoping(stmt.FinallyBlock);
            }
        }

        private void PostPrepareCatchScoping(AstCatchStmt stmt)
        {
            SetScopeAndParent(stmt.CatchBlock, stmt);
            var blockScope = PostPrepareBlockScoping(stmt.CatchBlock);

            // settings the block scope to the parameters (so they are in the scope of the block)
            SetScopeAndParent(stmt.CatchParam, stmt, blockScope);
            PostPrepareParamScoping(stmt.CatchParam);
        }


        /// <summary>
        /// Sets parent and scope to a child
        /// </summary>
        /// <param name="child">The child</param>
        /// <param name="parent">The parent</param>
        /// <param name="anotherScope">Scope to be set to a child. If null then parent scope is used</param>
        public void SetScopeAndParent(AstStatement child, AstStatement parent, Scope anotherScope = null)
        {
            anotherScope ??= parent?.Scope;
            child.Scope = anotherScope;
            child.Parent = parent;
            child.SourceFile = _currentSourceFile;
        }
    }
}
