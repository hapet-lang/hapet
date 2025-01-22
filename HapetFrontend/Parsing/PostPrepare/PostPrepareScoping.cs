using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Scoping;

namespace HapetFrontend.Parsing.PostPrepare
{
    public partial class PostPrepare
    {
        /// <summary>
        /// This kostyl is used to set TokenLocations on asts that were included from other projects/libraries
        /// TODO: there is a normal workaround for it. Just when deserializing metadata - we need to set normal 
        /// token locations relatively to .json file
        /// </summary>
        private string _externalProjectName = null;

        private void PostPrepareScoping()
        {
            PostPrepareInternalShiteScoping();
            foreach (var (path, file) in _compiler.GetFiles())
            {
                _currentSourceFile = file;
                foreach (var stmt in file.Statements)
                {
                    stmt.Scope = file.NamespaceScope;
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
                }
            }
        }

        private void PostPrepareInternalShiteScoping()
        {
            AstStringExpr.StringStruct.Scope = _compiler.GlobalScope;
            PostPrepareStructScoping(AstStringExpr.StringStruct);
            AstArrayExpr.ArrayStruct.Scope = _compiler.GlobalScope;
            PostPrepareStructScoping(AstArrayExpr.ArrayStruct);
        }

        private void PostPrepareClassScoping(AstClassDecl classDecl)
        {
            _currentClass = classDecl;

            classDecl.SourceFile = _currentSourceFile;
            var classScope = new Scoping.Scope($"{classDecl.Name.Name}_scope", classDecl.Scope);
            classDecl.SubScope = classScope; // setting the sub scope

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

            foreach (var decl in classDecl.Declarations)
            {
                SetScopeAndParent(decl, classDecl, classScope);

                if (decl is AstFuncDecl funcDecl)
                {
                    funcDecl.ContainingClass = classDecl;

                    /// defining in a scope is done in <see cref="PostPrepareFunctionInference"/>

                    PostPrepareFunctionScoping(funcDecl);
                }
                else if (decl is AstPropertyDecl propDecl) // property
                {
                    propDecl.ContainingParent = classDecl;

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

                    // if it is public field/property - it should be visible in the scope in which var's class is
                    classDecl.SubScope.DefineDeclSymbol(propDecl.Name.Name, propDecl);

                    // setting already defined to 'true' because of some shite with access types
                    PostPrepareVarScoping(propDecl, true);
                }
                else if (decl is AstVarDecl fieldDecl) // field 
                {
                    fieldDecl.ContainingParent = classDecl;

                    // if it is public field/property - it should be visible in the scope in which var's class is
                    classDecl.SubScope.DefineDeclSymbol(fieldDecl.Name.Name, fieldDecl);

                    // setting already defined to 'true' because of some shite with access types
                    PostPrepareVarScoping(fieldDecl, true);
                }
            }
        }

        private void PostPrepareStructScoping(AstStructDecl structDecl)
        {
            structDecl.SourceFile = _currentSourceFile;
            var structScope = new Scoping.Scope($"{structDecl.Name.Name}_scope", structDecl.Scope);
            structDecl.SubScope = structScope;

            // scoping struct attrs
            foreach (var a in structDecl.Attributes)
            {
                SetScopeAndParent(a, structDecl);
                PostPrepareExprScoping(a);
            }

            foreach (var decl in structDecl.Declarations)
            {
                SetScopeAndParent(decl, structDecl, structScope);

                var fieldDecl = decl as AstVarDecl;
                fieldDecl.ContainingParent = structDecl;

                // if it is public field/property - it should be visible in the scope in which var's class is
                structDecl.SubScope.DefineDeclSymbol(fieldDecl.Name.Name, fieldDecl);

                // setting already defined to 'true' because of some shite with access types
                PostPrepareVarScoping(fieldDecl, true);
            }
        }

        private void PostPrepareEnumScoping(AstEnumDecl enumDecl)
        {
            enumDecl.SourceFile = _currentSourceFile;
            var enumScope = new Scoping.Scope($"{enumDecl.Name.Name}_scope", enumDecl.Scope);
            enumDecl.SubScope = enumScope;

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
                decl.ContainingParent = enumDecl;

                // if it is public field/property - it should be visible in the scope in which var's class is
                enumDecl.SubScope.DefineDeclSymbol(decl.Name.Name, decl);

                // setting already defined to 'true' because of some shite with access types
                PostPrepareVarScoping(decl, true);
            }
        }

        private void PostPrepareDelegateScoping(AstDelegateDecl delegateDecl)
        {
            // scoping delegate attrs
            foreach (var a in delegateDecl.Attributes)
            {
                SetScopeAndParent(a, delegateDecl);
                PostPrepareExprScoping(a);
            }

            // WARN!!!! do not set the scope the same as delegate scope because its params would be visible in class or smth
            // creating a Scope in which the params would be
            var paramsBlockScope = new Scoping.Scope($"params_{delegateDecl.Name.Name}_scope", delegateDecl.Scope);

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
            _currentFunction = funcDecl;

            // scoping func attrs
            foreach (var a in funcDecl.Attributes)
            {
                SetScopeAndParent(a, funcDecl);
                PostPrepareExprScoping(a);
            }

            // base ctor call scoping
            if (funcDecl.BaseCtorCall != null)
            {
                SetScopeAndParent(funcDecl.BaseCtorCall, funcDecl);
                PostPrepareExprScoping(funcDecl.BaseCtorCall);
            }

            // TODO: refactor similar shite!
            if (funcDecl.Body != null)
            {
                // body scope is the same
                SetScopeAndParent(funcDecl.Body, funcDecl);
                var blockScope = PostPrepareBlockScoping(funcDecl.Body, $"{funcDecl.Name.Name}_scope");
                funcDecl.SubScope = blockScope;
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
            }
            else
            {
                // WARN!!!! do not set the scope the same as func scope because its params would be visible in class or smth
                // creating a Scope in which the params would be
                var paramsBlockScope = new Scoping.Scope($"params_{funcDecl.Name.Name}_scope", funcDecl.Scope);
                funcDecl.SubScope = paramsBlockScope;
                // defining parameters in the func scope
                foreach (var p in funcDecl.Parameters)
                {
                    // settings the block scope to the parameters (so they are in the scope of the block)
                    SetScopeAndParent(p, funcDecl, paramsBlockScope);
                    PostPrepareParamScoping(p);
                }
                // return type is the same
                SetScopeAndParent(funcDecl.Returns, funcDecl, paramsBlockScope);
                PostPrepareExprScoping(funcDecl.Returns);
            }
        }

        /// <summary>
        /// Post preparation of varDecl
        /// </summary>
        /// <param name="varDecl">The var decl</param>
        /// <param name="alreadyDefined">It could be already defined for example by classDecl (because of public/private shite)</param>
        private void PostPrepareVarScoping(AstVarDecl varDecl, bool alreadyDefined = false)
        {
            SetScopeAndParent(varDecl.Name, varDecl);
            SetScopeAndParent(varDecl.Type, varDecl);

            // scoping var attrs
            foreach (var a in varDecl.Attributes)
            {
                SetScopeAndParent(a, varDecl);
                PostPrepareExprScoping(a);
            }

            PostPrepareExprScoping(varDecl.Type);
            if (varDecl.Initializer != null)
            {
                SetScopeAndParent(varDecl.Initializer, varDecl);
                PostPrepareExprScoping(varDecl.Initializer);
            }
            // define it in the scope if it is not yet
            if (!alreadyDefined)
                varDecl.Scope.DefineDeclSymbol(varDecl.Name.Name, varDecl);
        }

        private void PostPrepareParamScoping(AstParamDecl paramDecl)
        {
            // it can be null when the func is only declared but not defined!
            if (paramDecl.Name != null)
                SetScopeAndParent(paramDecl.Name, paramDecl);
            SetScopeAndParent(paramDecl.Type, paramDecl);
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
                paramDecl.Scope.DefineDeclSymbol(paramDecl.Name.Name, paramDecl);
            }
        }

        private void PostPrepareExprScoping(AstStatement expr)
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
                case AstIdExpr _:
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
                case AstDefaultExpr _:
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
                // TODO: check other expressions

                default:
                    {
                        // TODO: anything to do here?
                        break;
                    }
            }
        }

        private static ulong _blockCounter = 0;
        private Scope PostPrepareBlockScoping(AstBlockExpr blockExpr, string scopename = "")
        {
            if (string.IsNullOrWhiteSpace(scopename))
                scopename = $"block_{_blockCounter++}_scope";

            var blockScope = new Scoping.Scope(scopename, blockExpr.Scope);
            blockExpr.SubScope = blockScope; // setting the sub scope

            foreach (var stmt in blockExpr.Statements)
            {
                if (stmt == null)
                    continue;

                SetScopeAndParent(stmt, blockExpr, blockScope);
                PostPrepareExprScoping(stmt);
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
            SetScopeAndParent(binExpr.Left, binExpr);
            SetScopeAndParent(binExpr.Right, binExpr);
            
            PostPrepareExprScoping(binExpr.Left);
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
            SetScopeAndParent(argumentExpr.Expr, argumentExpr);
            PostPrepareExprScoping(argumentExpr.Expr);
            if (argumentExpr.Name != null)
            {
                SetScopeAndParent(argumentExpr.Name, argumentExpr);
                PostPrepareExprScoping(argumentExpr.Name);
            }
        }

        private void PostPrepareCallExprScoping(AstCallExpr callExpr)
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

            SetScopeAndParent(castExpr.TypeExpr, castExpr);
            PostPrepareExprScoping(castExpr.TypeExpr);
        }

        private void PostPrepareNestedExprScoping(AstNestedExpr nestExpr)
        {
            SetScopeAndParent(nestExpr.RightPart, nestExpr);
            PostPrepareExprScoping(nestExpr.RightPart);
            if (nestExpr.LeftPart != null)
            {
                SetScopeAndParent(nestExpr.LeftPart, nestExpr);
                PostPrepareExprScoping(nestExpr.LeftPart);
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
            SetScopeAndParent(arrayAccExpr.ParameterExpr, arrayAccExpr);
            PostPrepareExprScoping(arrayAccExpr.ParameterExpr);
            SetScopeAndParent(arrayAccExpr.ObjectName, arrayAccExpr);
            PostPrepareExprScoping(arrayAccExpr.ObjectName);
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

            if (forStmt.FirstParam != null)
            {
                SetScopeAndParent(forStmt.FirstParam, forStmt, forScope);
                PostPrepareExprScoping(forStmt.FirstParam);
            }
            if (forStmt.SecondParam != null)
            {
                SetScopeAndParent(forStmt.SecondParam, forStmt, forScope);
                PostPrepareExprScoping(forStmt.SecondParam);
            }
            if (forStmt.ThirdParam != null)
            {
                SetScopeAndParent(forStmt.ThirdParam, forStmt, forScope);
                PostPrepareExprScoping(forStmt.ThirdParam);
            }
        }

        private static ulong _whileCounter = 0;
        private void PostPrepareWhileStmtScoping(AstWhileStmt whileStmt)
        {
            SetScopeAndParent(whileStmt.Body, whileStmt);

            string scopename = $"while_{_whileCounter++}_scope";
            var whileScope = PostPrepareBlockScoping(whileStmt.Body, scopename);

            if (whileStmt.ConditionParam != null)
            {
                SetScopeAndParent(whileStmt.ConditionParam, whileStmt, whileScope);
                PostPrepareExprScoping(whileStmt.ConditionParam);
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
            if (!caseStmt.DefaultCase)
            {
                SetScopeAndParent(caseStmt.Pattern, caseStmt);
                PostPrepareExprScoping(caseStmt.Pattern);
            }

            if (!caseStmt.FallingCase)
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
        }

        private void PostPrepareAttributeStmtScoping(AstAttributeStmt attrStmt)
        {
            SetScopeAndParent(attrStmt.AttributeName, attrStmt);
            PostPrepareExprScoping(attrStmt.AttributeName);
            foreach (var a in attrStmt.Parameters)
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


        /// <summary>
        /// Sets parent and scope to a child
        /// </summary>
        /// <param name="child">The child</param>
        /// <param name="parent">The parent</param>
        /// <param name="anotherScope">Scope to be set to a child. If null then parent scope is used</param>
        private void SetScopeAndParent(AstStatement child, AstStatement parent, Scope anotherScope = null)
        {
            anotherScope ??= parent.Scope;
            child.Scope = anotherScope;
            child.Parent = parent;
            child.SourceFile = _currentSourceFile;

            if (_externalProjectName != null && child.Location == null)
            {
                child.Location = new Location(new TokenLocation() { File = _externalProjectName });
            }
        }
    }
}
