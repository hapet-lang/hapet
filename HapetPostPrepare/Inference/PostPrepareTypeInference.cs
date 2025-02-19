using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using System;
using System.Xml.Linq;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareTypeInference()
        {
            foreach (var classDecl in AllClassesMetadata)
            {
                _currentSourceFile = classDecl.SourceFile;
                PostPrepareClassInference(classDecl);
            }
            foreach (var structDecl in AllStructsMetadata)
            {
                _currentSourceFile = structDecl.SourceFile;
                PostPrepareStructInference(structDecl);
            }
            foreach (var enumDecl in AllEnumsMetadata)
            {
                _currentSourceFile = enumDecl.SourceFile;
                PostPrepareEnumInference(enumDecl);
            }
            foreach (var delegateDecl in AllDelegatesMetadata)
            {
                _currentSourceFile = delegateDecl.SourceFile;
                PostPrepareDelegateInference(delegateDecl);
            }
        }

        private void PostPrepareClassInference(AstClassDecl classDecl)
        {
            _currentClass = classDecl;

            /// WARN: attributes are inferrenced in <see cref="PostPrepareMetadataAttributes"/>

            /// fields should be already inferred in <see cref="PostPrepareMetadataTypes"/> and <see cref="PostPrepareMetadataTypeFields"/>
            foreach (var decl in classDecl.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl))
            {
                PostPrepareFunctionInference(decl);
            }

            /// some shite is already inferrenced in <see cref="PostPrepareMetadataTypeFields"/>
            foreach (var decl in classDecl.Declarations.Where(x => x is AstPropertyDecl).Select(x => x as AstPropertyDecl))
            {
                if (decl.GetBlock != null)
                {
                    PostPrepareExprInference(decl.GetBlock);
                }
                if (decl.SetBlock != null)
                {
                    PostPrepareExprInference(decl.SetBlock);
                }
            }
        }

        private void PostPrepareStructInference(AstStructDecl structDecl)
        {
            /// WARN: should be already inferred in <see cref="PostPrepareMetadataTypes"/> and <see cref="PostPrepareMetadataTypeFields"/>
            /// WARN: attributes are inferrenced in <see cref="PostPrepareMetadataAttributes"/>
            /// 
            foreach (var decl in structDecl.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl))
            {
                PostPrepareFunctionInference(decl);
            }

            /// some shite is already inferrenced in <see cref="PostPrepareMetadataTypeFields"/>
            foreach (var decl in structDecl.Declarations.Where(x => x is AstPropertyDecl).Select(x => x as AstPropertyDecl))
            {
                if (decl.GetBlock != null)
                {
                    PostPrepareExprInference(decl.GetBlock);
                }
                if (decl.SetBlock != null)
                {
                    PostPrepareExprInference(decl.SetBlock);
                }
            }
        }

        private void PostPrepareEnumInference(AstEnumDecl enumDecl)
        {
            /// WARN: should be already inferred in <see cref="PostPrepareMetadataTypes"/> and <see cref="PostPrepareMetadataTypeFields"/>
            /// WARN: attributes are inferrenced in <see cref="PostPrepareMetadataAttributes"/>
        }

        private void PostPrepareDelegateInference(AstDelegateDecl delegateDecl)
        {
            /// WARN: should be already inferred in <see cref="PostPrepareMetadataTypes"/> and <see cref="PostPrepareMetadataTypeFields"/>
            /// WARN: attributes are inferrenced in <see cref="PostPrepareMetadataAttributes"/>

            // inferencing parameters 
            foreach (var p in delegateDecl.Parameters)
            {
                PostPrepareParamInference(p);
            }

            // inferencing return type 
            {
                PostPrepareExprInference(delegateDecl.Returns);
            }
        }

        private void PostPrepareFunctionInference(AstFuncDecl funcDecl, bool forMetadata = false)
        {
            /// WARN: attributes are inferrenced in <see cref="PostPrepareMetadataAttributes"/>

            _currentFunction = funcDecl;

            // if the function inference is for metadata - infer everything except body
            // if not - infer only body because func decl already infered from metadata :)
            if (forMetadata)
            {
                // inferencing parameters 
                foreach (var p in funcDecl.Parameters)
                {
                    PostPrepareParamInference(p);
                }

                // inferencing return type 
                {
                    PostPrepareExprInference(funcDecl.Returns);

                    if (funcDecl.Returns.OutType is ClassType)
                    {
                        // the return type is actually a pointer to the class
                        var astPtr = new AstPointerExpr(funcDecl.Returns, false, funcDecl.Returns.Location);
                        astPtr.OutType = PointerType.GetPointerType(astPtr.SubExpression.OutType);
                        astPtr.Scope = funcDecl.Returns.Scope;
                        funcDecl.Returns = astPtr;
                    }
                }

                // if the containing class is empty - it is external func
                if (funcDecl.ContainingParent != null)
                {
                    // it could already contain all the shite if the func is imported from another assembly :)
                    string newName = funcDecl.Name.Name;
                    if (!funcDecl.Name.Name.Contains("::"))
                        // renaming func name from 'Anime' to 'Anime(int, float)'
                        newName = $"{funcDecl.ContainingParent.Name.Name}::{funcDecl.Name.Name}{funcDecl.Parameters.GetParamsString()}";
                    // if it is public func - it should be visible in the scope in which func's class is
                    funcDecl.ContainingParent.SubScope.DefineDeclSymbol(newName, funcDecl);
                    funcDecl.Name = funcDecl.Name.GetCopy(newName);

                    // register operator decl
                    if (funcDecl is AstOverloadDecl overDecl2)
                    {
                        if (overDecl2.OverloadType == OverloadType.UnaryOperator ||
                            overDecl2.OverloadType == OverloadType.Indexer)
                        {
                            var op = new UserDefinedUnaryOperator(overDecl2.Operator, overDecl2.Returns.OutType, overDecl2.Parameters[0].Type.OutType);
                            op.Function = funcDecl.Type.OutType as FunctionType;
                            funcDecl.ContainingParent.SubScope.DefineUnaryOperator(op);
                        }
                        else if (overDecl2.OverloadType == OverloadType.BinaryOperator)
                        {
                            var op = new UserDefinedBinaryOperator(overDecl2.Operator, overDecl2.Returns.OutType, overDecl2.Parameters[0].Type.OutType, overDecl2.Parameters[1].Type.OutType);
                            op.Function = funcDecl.Type.OutType as FunctionType;
                            funcDecl.ContainingParent.SubScope.DefineBinaryOperator(op);
                        }
                        else if (overDecl2.OverloadType == OverloadType.ImplicitCast ||
                            overDecl2.OverloadType == OverloadType.ExplicitCast)
                        {
                            var op = new UserDefinedBinaryOperator(overDecl2.Operator, overDecl2.Returns.OutType, overDecl2.Returns.OutType, overDecl2.Parameters[0].Type.OutType);
                            op.Function = funcDecl.Type.OutType as FunctionType;
                            funcDecl.ContainingParent.SubScope.DefineBinaryOperator(op);
                        }
                    }
                }
            }
            else
            {
                // inferring body
                if (funcDecl.Body != null)
                    PostPrepareBlockInference(funcDecl.Body);

                // check if the class if inherited from smth
                if (funcDecl.ClassFunctionType == HapetFrontend.Enums.ClassFunctionType.Ctor &&
                    funcDecl.ContainingParent is AstClassDecl clsDecl &&
                    clsDecl.InheritedFrom.Count > 0 &&
                    funcDecl.BaseCtorCall != null &&
                    clsDecl.InheritedFrom[0].OutType is ClassType baseType &&
                    !baseType.Declaration.IsInterface)
                {
                    PostPrepareExprInference(funcDecl.BaseCtorCall);

                    // preparing shite for easier code gen
                    funcDecl.BaseCtorCall.BaseType = baseType;
                    var thisArg = new AstIdExpr("this", funcDecl.BaseCtorCall);
                    SetScopeAndParent(thisArg, funcDecl.Body, funcDecl.Body.SubScope);
                    PostPrepareExprInference(thisArg);
                    funcDecl.BaseCtorCall.ThisArgument = thisArg;

                    // we need to insert it into block so it would be generated normally
                    // but why to the index 1? - https://stackoverflow.com/questions/140490/base-constructor-in-c-sharp-which-gets-called-first
                    funcDecl.Body.Statements.Insert(1, funcDecl.BaseCtorCall);
                }
            }
        }

        private void PostPrepareVarInference(AstVarDecl varDecl, bool allowSpecialKeys = false)
        {
            PostPrepareExprInference(varDecl.Type);

            if (varDecl.Initializer != null)
                PostPrepareExprInference(varDecl.Initializer);

            // change variable type to a normal one
            if (varDecl.Type.OutType is VarType)
            {
                if (varDecl.Initializer == null)
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, varDecl, [], ErrorCode.Get(CTEN.VarVarNoIniter));
                else if (varDecl.Initializer.OutType is VoidType)
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, varDecl, [], ErrorCode.Get(CTEN.VarVoidType));
                else
                    varDecl.Type.OutType = varDecl.Initializer.OutType;
            }

            if (varDecl.Type.OutType is ClassType)
            {
                // the var is actually a pointer to the class
                var astPtr = new AstPointerExpr(varDecl.Type, false, varDecl.Type.Location);
                astPtr.OutType = PointerType.GetPointerType(astPtr.SubExpression.OutType);
                astPtr.Scope = varDecl.Type.Scope;
                varDecl.Type = astPtr;
            }

            // pp assign value
            if (varDecl.Initializer != null)
                varDecl.Initializer = PostPrepareVarValueAssign(varDecl.Initializer, varDecl.Type.OutType);

            // special keys could not be allowed when the var is declared in BlockExpr
            if (!allowSpecialKeys)
            {
                foreach (var kk in varDecl.SpecialKeys)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, new Location(varDecl.Beginning, varDecl.Name.Ending), [kk.ToString()], ErrorCode.Get(CTEN.VarTokenNotAllowed));
                }
            }

            // check for const value that it is compile time evaluated
            if ((varDecl.Initializer == null || varDecl.Initializer.OutValue == null) && varDecl.SpecialKeys.Contains(TokenType.KwConst))
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, varDecl.Name, [], ErrorCode.Get(CTEN.ConstValueNonComptime));
            }
        }

        private void PostPrepareParamInference(AstParamDecl paramDecl)
        {
            PostPrepareExprInference(paramDecl.Type);

            if (paramDecl.Type.OutType is ClassType)
            {
                // the var is actually a pointer to the class
                var astPtr = new AstPointerExpr(paramDecl.Type, false, paramDecl.Type.Location);
                astPtr.Scope = paramDecl.Type.Scope;
                paramDecl.Type = astPtr;
                PostPrepareExprInference(paramDecl.Type);
            }

            if (paramDecl.DefaultValue != null)
                PostPrepareExprInference(paramDecl.DefaultValue);
        }

        private void PostPrepareExprInference(AstStatement expr)
        {
            switch (expr)
            {
                // special case at least for 'for' loop
                // when 'for (int i = 0;...)' where 'int i' 
                // would not be handled by blockExpr
                case AstVarDecl varDecl:
                    PostPrepareVarInference(varDecl);
                    break;

                case AstBlockExpr blockExpr:
                    PostPrepareBlockInference(blockExpr);
                    break;
                case AstUnaryExpr unExpr:
                    PostPrepareUnaryExprInference(unExpr);
                    break;
                case AstBinaryExpr binExpr:
                    PostPrepareBinaryExprInference(binExpr);
                    break;
                case AstPointerExpr pointerExpr:
                    PostPreparePointerExprInference(pointerExpr);
                    break;
                case AstAddressOfExpr addrExpr:
                    PostPrepareAddressOfExprInference(addrExpr);
                    break;
                case AstNewExpr newExpr:
                    PostPrepareNewExprInference(newExpr);
                    break;
                case AstArgumentExpr argumentExpr:
                    PostPrepareArgumentExprInference(argumentExpr);
                    break;
                case AstIdExpr idExpr:
                    PostPrepareIdentifierInference(idExpr);
                    return;
                case AstCallExpr callExpr:
                    PostPrepareCallExprInference(callExpr);
                    break;
                case AstCastExpr castExpr:
                    PostPrepareCastExprInference(castExpr);
                    break;
                case AstNestedExpr nestExpr:
                    PostPrepareNestedExprInference(nestExpr, out bool _);
                    break;
                case AstDefaultExpr defaultExpr:
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, defaultExpr, [], ErrorCode.Get(CTEN.DefaultWasNotInfered));
                    break;
                case AstArrayExpr arrayExpr:
                    PostPrepareArrayExprInference(arrayExpr);
                    break;
                case AstArrayCreateExpr arrayCreateExpr:
                    PostPrepareArrayCreateExprInference(arrayCreateExpr);
                    break;
                case AstArrayAccessExpr arrayAccExpr:
                    PostPrepareArrayAccessExprInference(arrayAccExpr);
                    break;
                case AstStringExpr stringExpr:
                    stringExpr.OutType = StringType.GetInstance(stringExpr.Scope);
                    break;

                // statements
                case AstAssignStmt assignStmt:
                    // _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, assignStmt, "(Compiler exception) The statement has to be handled by block expr");
                    PostPrepareAssignStmtInference(assignStmt, out bool _);
                    break;
                case AstForStmt forStmt:
                    PostPrepareForStmtInference(forStmt);
                    break;
                case AstWhileStmt whileStmt:
                    PostPrepareWhileStmtInference(whileStmt);
                    break;
                case AstIfStmt ifStmt:
                    PostPrepareIfStmtInference(ifStmt);
                    break;
                case AstSwitchStmt switchStmt:
                    PostPrepareSwitchStmtInference(switchStmt);
                    break;
                case AstCaseStmt caseStmt:
                    PostPrepareCaseStmtInference(caseStmt);
                    break;
                case AstBreakContStmt _:
                    break;
                case AstReturnStmt returnStmt:
                    PostPrepareReturnStmtInference(returnStmt);
                    break;
                case AstAttributeStmt attrStmt:
                    PostPrepareAttributeStmtInference(attrStmt);
                    break;
                case AstBaseCtorStmt baseStmt:
                    PostPrepareBaseCtorStmtInference(baseStmt);
                    break;
                // TODO: check other expressions

                default:
                    {
                        // TODO: anything to do here?
                        break;
                    }
            }
        }

        private void PostPrepareBlockInference(AstBlockExpr blockExpr)
        {
            // list of all replacements that should be done
            // so all Propa assigns would be replaced with func calls
            Dictionary<AstAssignStmt, AstCallExpr> repls = new Dictionary<AstAssignStmt, AstCallExpr>();
            // go all over the statements
            foreach (var stmt in blockExpr.Statements)
            {
                if (stmt == null)
                    continue;

                // we need to handle the statements to replaces props with calls
                if (stmt is AstAssignStmt asgn)
                {
                    PostPrepareAssignStmtInference(asgn, out bool itWasPropa);
                    if (itWasPropa)
                    {
                        AstIdExpr propaName = (asgn.Target.RightPart as AstIdExpr);
                        // creating a call 
                        var fncCall = new AstCallExpr(asgn.Target.LeftPart, propaName.GetCopy($"set_{propaName.Name}"), new List<AstArgumentExpr>() { new AstArgumentExpr(asgn.Value) }, asgn);
                        SetScopeAndParent(fncCall, asgn.Target.NormalParent, asgn.Target.Scope);
                        PostPrepareCallExprInference(fncCall);
                        repls.Add(asgn, fncCall);
                    }
                }
                else
                {
                    PostPrepareExprInference(stmt);
                }
            }

            // begin all replacements
            foreach (var pair in repls)
            {
                // replace the assign statement
                int assignIndex = blockExpr.Statements.IndexOf(pair.Key);
                blockExpr.Statements.Remove(pair.Key);
                blockExpr.Statements.Insert(assignIndex, pair.Value);
            }
        }

        private void PostPrepareUnaryExprInference(AstUnaryExpr unExpr)
        {
            // TODO: check for the right size for an existance value (compiletime evaluated) and do some shite (set unExpr OutValue)
            PostPrepareExprInference(unExpr.SubExpr as AstExpression);
            var operators = unExpr.Scope.GetUnaryOperators(unExpr.Operator, (unExpr.SubExpr as AstExpression).OutType);
            if (operators.Count == 0)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, unExpr, 
                    [unExpr.Operator, HapetType.AsString((unExpr.SubExpr as AstExpression).OutType)], ErrorCode.Get(CTEN.UndefOpForType));
            }
            else if (operators.Count > 1)
            {
                // TODO: tell em where are the operators defined
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, unExpr, 
                    [unExpr.Operator, HapetType.AsString((unExpr.SubExpr as AstExpression).OutType)], ErrorCode.Get(CTEN.TooManyOpsForType));
            }
            else
            {
                unExpr.ActualOperator = operators[0];
                unExpr.OutType = unExpr.ActualOperator.ResultType;

                // if the value could be evaluated at the compile time
                if ((unExpr.SubExpr as AstExpression).OutValue != null && 
                    unExpr.ActualOperator.CanExecute)
                {
                    unExpr.OutValue = unExpr.ActualOperator.Execute((unExpr.SubExpr as AstExpression).OutValue);
                }
            }
        }

        private void PostPrepareBinaryExprInference(AstBinaryExpr binExpr)
        {
            // resolve the actual operator in the current scope
            PostPrepareExprInference(binExpr.Left as AstExpression);
            PostPrepareExprInference(binExpr.Right as AstExpression);
            var operators = binExpr.Scope.GetBinaryOperators(binExpr.Operator, (binExpr.Left as AstExpression).OutType, (binExpr.Right as AstExpression).OutType);
            if (operators.Count == 0)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, binExpr, 
                    [binExpr.Operator, 
                    HapetType.AsString((binExpr.Left as AstExpression).OutType), 
                    HapetType.AsString((binExpr.Right as AstExpression).OutType)], 
                    ErrorCode.Get(CTEN.BinUndefOpForTypes));
            }
            else if (operators.Count > 1)
            {
                // TODO: tell em where are the operators defined
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, binExpr, 
                    [binExpr.Operator,
                    HapetType.AsString((binExpr.Left as AstExpression).OutType),
                    HapetType.AsString((binExpr.Right as AstExpression).OutType)], 
                    ErrorCode.Get(CTEN.BinTooManyOpsForTypes));
            }
            else
            {
                binExpr.ActualOperator = operators[0];
                binExpr.OutType = binExpr.ActualOperator.ResultType;

                // making some type casts
                var leftExpr = (binExpr.Left as AstExpression);
                var rightExpr = (binExpr.Right as AstExpression);

                // CRINGE :) special cases for as/is/in
                switch (binExpr.ActualOperator.Name)
                {
                    case "as":
                        {
                            // we need to change right part to pointer to a class
                            // so bitcast would be possible
                            rightExpr.OutType = PointerType.GetPointerType(rightExpr.OutType);
                            binExpr.OutType = rightExpr.OutType;
                            // TODO: check for inheritance!!!
                            break;
                        }
                    default:
                        {
                            // if smth with pointers :(((
                            if (binExpr.OutType is PointerType)
                            {
                                // we need to multiply one of the expr by size of ptr type size
                                if (leftExpr.OutType is PointerType ptrT)
                                {
                                    // error if bin op with void*
                                    if (ptrT.TargetType is VoidType)
                                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, leftExpr, [binExpr.ActualOperator.Name], ErrorCode.Get(CTEN.OpUsedWithVoidPtr));
                                    var parent = rightExpr.NormalParent;
                                    var mulK = new AstNumberExpr((NumberData)ptrT.TargetType.GetSize(), null, null, rightExpr);
                                    SetScopeAndParent(mulK, parent);
                                    rightExpr = new AstBinaryExpr("*", rightExpr, mulK, rightExpr);
                                    SetScopeAndParent(rightExpr, parent);
                                    PostPrepareExprInference(rightExpr);
                                    binExpr.Right = rightExpr;
                                }
                                else if (rightExpr.OutType is PointerType ptrT2)
                                {
                                    // error if bin op with void*
                                    if (ptrT2.TargetType is VoidType)
                                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, rightExpr, [binExpr.ActualOperator.Name], ErrorCode.Get(CTEN.OpUsedWithVoidPtr));
                                    var parent = leftExpr.NormalParent;
                                    var mulK = new AstNumberExpr((NumberData)ptrT2.TargetType.GetSize(), null, null, leftExpr);
                                    SetScopeAndParent(mulK, parent);
                                    leftExpr = new AstBinaryExpr("*", leftExpr, mulK, leftExpr);
                                    SetScopeAndParent(leftExpr, parent);
                                    PostPrepareExprInference(leftExpr);
                                    binExpr.Left = leftExpr;
                                }
                            }

                            // creating cast to result type if it is not a bool expr
                            if (leftExpr.OutType != binExpr.OutType && 
                                binExpr.OutType is not BoolType && 
                                binExpr.OutType is not PointerType &&
                                binExpr.ActualOperator is not IUserDefinedOperator)
                            {
                                // cast if they are not the same haha
                                binExpr.Left = PostPrepareExpressionWithType(binExpr.OutType, leftExpr);
                            }
                            // creating cast to result type if it is not a bool expr
                            if (rightExpr.OutType != binExpr.OutType && 
                                binExpr.OutType is not BoolType && 
                                binExpr.OutType is not PointerType &&
                                binExpr.ActualOperator is not IUserDefinedOperator)
                            {
                                // cast if they are not the same haha
                                binExpr.Right = PostPrepareExpressionWithType(binExpr.OutType, rightExpr);
                            }

                            // creating cast to result type if it is a bool expr and left and right are not the same types
                            if (rightExpr.OutType != leftExpr.OutType && 
                                binExpr.OutType is BoolType &&
                                binExpr.ActualOperator is not IUserDefinedOperator)
                            {
                                // cast if they are not the same haha
                                HapetType castingType = HapetType.GetPreferredTypeOf(leftExpr.OutType, rightExpr.OutType, out bool tookLeft);
                                // if the left type was taken then change the right expr
                                if (tookLeft)
                                    binExpr.Right = PostPrepareExpressionWithType(castingType, rightExpr);
                                else
                                    binExpr.Left = PostPrepareExpressionWithType(castingType, leftExpr);
                            }

                            // if the value could be evaluated at the compile time
                            if (leftExpr.OutValue != null && 
                                rightExpr.OutValue != null && 
                                binExpr.ActualOperator.CanExecute)
                            {
                                binExpr.OutValue = binExpr.ActualOperator.Execute(leftExpr.OutValue, rightExpr.OutValue);
                            }

                            break;
                        }
                }

            }
        }

        private void PostPreparePointerExprInference(AstPointerExpr pointerExpr)
        {
            // prepare the right side
            PostPrepareExprInference(pointerExpr.SubExpression);
            if (pointerExpr.IsDereference)
            {
                // if it is a deref - right type has to be a ptr
                var rightType = pointerExpr.SubExpression.OutType as PointerType;
                if (rightType == null)
                {
                    // TODO: error here
                    return;
                }
                pointerExpr.OutType = rightType.TargetType;
            }
            else
            {
                // create a new pointer type from the right side and set the type to itself
                pointerExpr.OutType = PointerType.GetPointerType(pointerExpr.SubExpression.OutType);
            }
        }

        private void PostPrepareAddressOfExprInference(AstAddressOfExpr addrExpr)
        {
            // prepare the right side
            PostPrepareExprInference(addrExpr.SubExpression);
            // create a new reference type from the right side and set the type to itself
            addrExpr.OutType = ReferenceType.GetRefType(addrExpr.SubExpression.OutType);
        }

        private void PostPrepareNewExprInference(AstNewExpr newExpr)
        {
            // prepare the right side
            PostPrepareExprInference(newExpr.TypeName);
            // the type of newExpr is the same as the type of its name expr
            newExpr.OutType = newExpr.TypeName.OutType;

            // error if they trying to create an instance from interface of an abstract class
            if (newExpr.TypeName.OutType is ClassType clsType && 
                (clsType.Declaration.IsInterface || 
                clsType.Declaration.SpecialKeys.Contains(TokenType.KwAbstract)))
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, newExpr, [], ErrorCode.Get(CTEN.CreateInterfOrAbsCls));
            }

            foreach (var a in newExpr.Arguments)
            {
                PostPrepareExprInference(a);
            }
        }

        private void PostPrepareArgumentExprInference(AstArgumentExpr argumentExpr)
        {
            PostPrepareExprInference(argumentExpr.Expr);

            if (argumentExpr.Name != null)
            {
                PostPrepareExprInference(argumentExpr.Name);
            }

            // the argument type is the same as its expr type
            argumentExpr.OutType = argumentExpr.Expr.OutType;
            // if the value could be evaluated at the compile time
            if (argumentExpr.Expr.OutValue != null)
            {
                argumentExpr.OutValue = argumentExpr.Expr.OutValue;
            }
        }

        private void PostPrepareIdentifierInference(AstIdExpr idExpr, bool fromCallExpr = false)
        {
            string name = idExpr.Name;

            // kostyl to handle 'base.Anime()' calls
            if (name == "base")
            {
                idExpr.OutType = PointerType.GetPointerType(_currentClass.InheritedFrom[0].OutType);
                var smbl2 = idExpr.Scope.GetSymbol("this");
                idExpr.FindSymbol = smbl2;
                return;
            }

            var smbl = idExpr.Scope.GetSymbol(name);
            if (smbl is DeclSymbol typed)
            {
                if (!CheckIfCouldBeAccessed(idExpr, typed.Decl) && !(typed.Decl is AstBuiltInTypeDecl) && !fromCallExpr)
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [], ErrorCode.Get(CTEN.DeclCouldNotBeAccessed));
                idExpr.OutType = typed.Decl.Type.OutType;
                TryAssignConstValueToExpr(idExpr, typed.Decl);
                TrySaveClassUsage(typed.Decl);
                idExpr.FindSymbol = smbl;
                return;
            }

            // searching for the name with current class name
            // works only for functions
            string nameWithClass = $"{_currentClass.Name.Name}::{name}";
            var smblInLocalClass = idExpr.Scope.GetSymbol(nameWithClass);
            if (smblInLocalClass is DeclSymbol typed2)
            {
                if (!CheckIfCouldBeAccessed(idExpr, typed2.Decl) && !(typed2.Decl is AstBuiltInTypeDecl) && !fromCallExpr)
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [], ErrorCode.Get(CTEN.DeclCouldNotBeAccessed));
                idExpr.Name = nameWithClass;
                idExpr.OutType = typed2.Decl.Type.OutType;
                TryAssignConstValueToExpr(idExpr, typed2.Decl);
                TrySaveClassUsage(typed2.Decl);
                idExpr.FindSymbol = smblInLocalClass;
                return;
            }

            // it is a func
            if (name.Contains("::"))
            {
                // for example 'System.Attribute::Attrbute_ctor(...)'
                string[] nameAndFunc = name.Split("::");
                if (nameAndFunc.Length != 2)
                {
                    // TODO: error 
                    return;
                }

                // recursively infer left part of func call
                AstIdExpr leftPartId = idExpr.GetCopy(nameAndFunc[0]);
                PostPrepareIdentifierInference(leftPartId, fromCallExpr);

                // it has to be a class (or mb struct)
                string fullFuncName;
                ISymbol funcInAnotherClass;
                if (leftPartId.OutType is ClassType clsTp)
                {
                    fullFuncName = $"{clsTp}::{nameAndFunc[1]}";
                    funcInAnotherClass = clsTp.Declaration.SubScope.GetSymbol(fullFuncName);
                }
                else if (leftPartId.OutType is StructType strTp)
                {
                    fullFuncName = $"{strTp}::{nameAndFunc[1]}";
                    funcInAnotherClass = strTp.Declaration.SubScope.GetSymbol(fullFuncName);
                }
                else
                {
                    // TODO: error 
                    return;
                }
                
                if (funcInAnotherClass is DeclSymbol typed4)
                {
                    if (!CheckIfCouldBeAccessed(idExpr, typed4.Decl) && !(typed4.Decl is AstBuiltInTypeDecl) && !fromCallExpr)
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [], ErrorCode.Get(CTEN.DeclCouldNotBeAccessed));
                    idExpr.Name = fullFuncName;
                    idExpr.OutType = typed4.Decl.Type.OutType;
                    TryAssignConstValueToExpr(idExpr, typed4.Decl);
                    TrySaveClassUsage(typed4.Decl);
                    idExpr.FindSymbol = funcInAnotherClass;
                    return;
                }
            }

            // searching for the name with namespace
            // works only for types/objects
            string nameWithNamespace = $"{_currentSourceFile.Namespace}.{name}";
            var smblInLocalFile = idExpr.Scope.GetSymbol(nameWithNamespace);
            if (smblInLocalFile is DeclSymbol typed3)
            {
                if (!CheckIfCouldBeAccessed(idExpr, typed3.Decl) && !(typed3.Decl is AstBuiltInTypeDecl) && !fromCallExpr)
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [], ErrorCode.Get(CTEN.DeclCouldNotBeAccessed));
                idExpr.Name = nameWithNamespace;
                idExpr.OutType = typed3.Decl.Type.OutType;
                TryAssignConstValueToExpr(idExpr, typed3.Decl);
                TrySaveClassUsage(typed3.Decl);
                idExpr.FindSymbol = smblInLocalFile;
                return;
            }

            // check if it is smth like 'System.Attribute' where 'System' is ns and 'Attribute' is a class
            if (name.Split('.').Length > 1)
            {
                string[] splitted = name.Split('.');
                var leftPart = string.Join('.', splitted.SkipLast(1));
                var rightPart = splitted.Last();

                // getting a symbol from namespace
                var includedSmbl = idExpr.Scope.GetSymbolInNamespace(leftPart, rightPart);
                if (includedSmbl is DeclSymbol typed4)
                {
                    if (!CheckIfCouldBeAccessed(idExpr, typed4.Decl) && !(typed4.Decl is AstBuiltInTypeDecl) && !fromCallExpr)
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [], ErrorCode.Get(CTEN.DeclCouldNotBeAccessed));
                    // do not change name because it already contains namespace
                    idExpr.OutType = typed4.Decl.Type.OutType;
                    TryAssignConstValueToExpr(idExpr, typed4.Decl);
                    TrySaveClassUsage(typed4.Decl);
                    idExpr.FindSymbol = includedSmbl;
                    return;
                }
            }

            // go all over the usings
            foreach (var usng in _currentSourceFile.Usings)
            {
                // getting ns string
                var ns = usng.FlattenNamespace;

                // check if it is smth like 'Runtime.InteropServices.DllImportAttribute'
                // where 'Runtime.InteropServices' is PART! of ns and 'DllImportAttribute' is a class
                if (name.Split('.').Length > 1)
                {
                    string[] splitted = name.Split('.');
                    var leftPart = string.Join('.', splitted.SkipLast(1));
                    var rightPart = splitted.Last();

                    // getting a symbol from namespace
                    var includedSmbl = idExpr.Scope.GetSymbolInNamespace($"{ns}.{leftPart}", rightPart);
                    if (includedSmbl is DeclSymbol typed4)
                    {
                        if (!CheckIfCouldBeAccessed(idExpr, typed4.Decl) && !(typed4.Decl is AstBuiltInTypeDecl) && !fromCallExpr)
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [], ErrorCode.Get(CTEN.DeclCouldNotBeAccessed));
                        // do not change name because it already contains namespace
                        idExpr.OutType = typed4.Decl.Type.OutType;
                        TryAssignConstValueToExpr(idExpr, typed4.Decl);
                        TrySaveClassUsage(typed4.Decl);
                        idExpr.FindSymbol = includedSmbl;
                        return;
                    }
                }

                // try just get the name from using namespace
                string fullNameWithNs = $"{ns}.{name}";
                var usedSmbl = idExpr.Scope.GetSymbolInNamespace(ns, name);
                if (usedSmbl is DeclSymbol typed5)
                {
                    if (!CheckIfCouldBeAccessed(idExpr, typed5.Decl) && !(typed5.Decl is AstBuiltInTypeDecl) && !fromCallExpr)
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [], ErrorCode.Get(CTEN.DeclCouldNotBeAccessed));
                    idExpr.Name = fullNameWithNs;
                    idExpr.OutType = typed5.Decl.Type.OutType;
                    TryAssignConstValueToExpr(idExpr, typed5.Decl);
                    TrySaveClassUsage(typed5.Decl);
                    idExpr.FindSymbol = usedSmbl;
                    return;
                }
            }

            // TODO: really give them a error? or mb there is smth harder?
            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [], ErrorCode.Get(CTEN.TypeCouldNotBeInfered));
        }

        /// <summary>
        /// This shite helps us to move OutValue from one to another
        /// </summary>
        /// <param name="expr">The main expr</param>
        /// <param name="decl">The decl that could have OutValue</param>
        private void TryAssignConstValueToExpr(AstExpression expr, AstDeclaration decl)
        {
            // assign out value only from consts
            if (decl is AstVarDecl varDecl && varDecl.SpecialKeys.Contains(TokenType.KwConst))
            {
                // skip this shite - inferer will error it somewhere
                if (varDecl.Initializer == null)
                    return;

                // check that the initializer is not yet infered - infer it
                // TODO: possible circular access!!!
                if (varDecl.Initializer.OutType == null)
                {
                    PostPrepareExprInference(varDecl.Initializer);
                }
                expr.OutValue = varDecl.Initializer.OutValue;
            }
        }

        /// <summary>
        /// Saves class usage to know which were used by the program. 
        /// This would be used to call static ctors :)
        /// </summary>
        /// <param name="decl">The decl to check and mark</param>
        private void TrySaveClassUsage(AstDeclaration decl)
        {
            if (decl is AstClassDecl clsDecl)
            {
                _allUsedClassesInProgram.Add(clsDecl);
            }
        }

        private void PostPrepareCallExprInference(AstCallExpr callExpr)
        {
            // skip if already inferred
            if (callExpr.OutType != null)
                return;

            // the var is used to check when static method is accessed from an object
            bool accessingFromAnObject = false;

            // usually when in the same class
            if (callExpr.TypeOrObjectName != null)
            {
                // resolve the object on which func is called
                PostPrepareExprInference(callExpr.TypeOrObjectName);
            }

            // TODO: resolve functions as args directly
            // but there is a problem:
            /*
                public class Anime
                {
                    public delegate int Cringe(int a);
                    public delegate int Cringe22();

                    public void CallC(Cringe cr)
                    {
                        cr(2);
                    }

                    public void CallC(Cringe22 cr)
                    {
                        cr();
                    }

                    private int CringeFunc(int a)
                    {
                        return 1;
                    }

                    private int CringeFunc()
                    {
                        return 1;
                    }

                    private void Test()
                    {
                        CallC(CringeFunc);
                        CallC(CringeFunc);
                    }
                }
             */

            // resolve args
            foreach (var a in callExpr.Arguments)
            {
                PostPrepareExprInference(a);
            }

            string newName = string.Empty;
            // renaming func call name from 'Anime' to 'Anime(int, float)' WITH OBJECT AS FIRST PARAM
            if (callExpr.TypeOrObjectName == null)
            {
                // if the type/object name is not presented - the function is in the same class
                // but we need to know is it static or not
                newName = $"{_currentClass.Name.Name}::{callExpr.FuncName.Name}{callExpr.Arguments.GetArgsString()}";
                var smbl2 = GetFuncFromCandidates(newName, callExpr.Arguments.Select(x => x.Expr).ToList(), callExpr.FuncName.Scope, _currentClass, out var casts);
                if (smbl2 is DeclSymbol ds && ds.Decl is AstFuncDecl funcDecl)
                {
                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl))
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    // static func defined in local class
                    newName = funcDecl.Name.Name;
                    callExpr.Arguments.ReplaceWithCasts(casts);
                }
                else
                {
                    accessingFromAnObject = true;
                    // we need to create this one because code generator requires the parameter of this shite
                    callExpr.TypeOrObjectName = new AstNestedExpr(new AstIdExpr("this"), null, callExpr);
                    SetScopeAndParent(callExpr.TypeOrObjectName, callExpr);
                    PostPrepareExprScoping(callExpr.TypeOrObjectName);
                    PostPrepareExprInference(callExpr.TypeOrObjectName);

                    // if it is a non static func defined in local class
                    newName = $"{_currentClass.Name.Name}::{callExpr.FuncName.Name}{callExpr.Arguments.GetArgsString(PointerType.GetPointerType(_currentClass.Type.OutType))}";
                    List<AstExpression> argsWithClassParam = new List<AstExpression>(callExpr.Arguments);
                    argsWithClassParam.Insert(0, callExpr.TypeOrObjectName);

                    smbl2 = GetFuncFromCandidates(newName, argsWithClassParam, callExpr.FuncName.Scope, _currentClass, out var casts2);
                    if (smbl2 is DeclSymbol ds2 && ds2.Decl is AstFuncDecl funcDecl2)
                    {
                        if (!CheckIfCouldBeAccessed(callExpr, funcDecl2))
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                        newName = funcDecl2.Name.Name;
                        callExpr.Arguments.ReplaceWithCasts(casts2.Skip(1).ToList()); // skip because the first param is an object
                    }
                    else
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncWithNameNotFound));
                }
            }
            else if (callExpr.TypeOrObjectName.OutType is PointerType ptrTp && ptrTp.TargetType is ClassType clsTp)
            {
                // if we are calling like 'a.Anime()' where 'a' is an object
                // we need to rename the func name call like that:
                newName = $"{clsTp.Declaration.Name.Name}::{callExpr.FuncName.Name}{callExpr.Arguments.GetArgsString(callExpr.TypeOrObjectName.OutType)}";

                List<AstExpression> argsWithClassParam = new List<AstExpression>(callExpr.Arguments);
                argsWithClassParam.Insert(0, callExpr.TypeOrObjectName);
                var smbl2 = GetFuncFromCandidates(newName, argsWithClassParam, clsTp.Declaration.SubScope, clsTp.Declaration, out var casts);

                // check if the decl exists. if not - it could be static method call from an object
                if (smbl2 is DeclSymbol ds && ds.Decl is AstFuncDecl funcDecl)
                {
                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl))
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    newName = funcDecl.Name.Name;
                    callExpr.Arguments.ReplaceWithCasts(casts.Skip(1).ToList()); // skip because the first param is an object
                }
                else
                {
                    // getting the name but without object first param
                    newName = $"{clsTp.Declaration.Name.Name}::{callExpr.FuncName.Name}{callExpr.Arguments.GetArgsString()}";
                    smbl2 = GetFuncFromCandidates(newName, callExpr.Arguments.Select(x => x.Expr).ToList(), clsTp.Declaration.SubScope, clsTp.Declaration, out var casts2);
                    if (smbl2 is DeclSymbol ds2 && ds2.Decl is AstFuncDecl funcDecl2)
                    {
                        if (!CheckIfCouldBeAccessed(callExpr, funcDecl2))
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                        newName = funcDecl2.Name.Name;
                        callExpr.Arguments.ReplaceWithCasts(casts2);
                    }
                    else
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncWithNameNotFound));
                }
                accessingFromAnObject = true;
            }
            else if (callExpr.TypeOrObjectName.OutType is ClassType clsTpStatic)
            {
                // if we are calling like 'A.Anime()' where 'A' is a class
                // we need to rename the func name call like that:
                newName = $"{clsTpStatic.Declaration.Name.Name}::{callExpr.FuncName.Name}{callExpr.Arguments.GetArgsString()}";

                var smbl2 = GetFuncFromCandidates(newName, callExpr.Arguments.Select(x => x.Expr).ToList(), clsTpStatic.Declaration.SubScope, clsTpStatic.Declaration, out var casts);

                // check if the decl exists. if not - it could be non static method call from a class name
                if (smbl2 is DeclSymbol ds && ds.Decl is AstFuncDecl funcDecl)
                {
                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl))
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    newName = funcDecl.Name.Name;
                    callExpr.Arguments.ReplaceWithCasts(casts);
                }
                else
                {
                    // getting the name but with object first param
                    newName = $"{clsTpStatic.Declaration.Name.Name}::{callExpr.FuncName.Name}{callExpr.Arguments.GetArgsString(PointerType.GetPointerType(clsTpStatic))}";

                    List<AstExpression> argsWithClassParam = new List<AstExpression>(callExpr.Arguments);
                    var pseudoClassArg = new AstPointerExpr(callExpr.TypeOrObjectName, false, callExpr.TypeOrObjectName);
                    PostPrepareExprInference(pseudoClassArg);
                    argsWithClassParam.Insert(0, pseudoClassArg);
                    smbl2 = GetFuncFromCandidates(newName, argsWithClassParam, clsTpStatic.Declaration.SubScope, clsTpStatic.Declaration, out var _);

                    // error because user tries to access non static method from a class name
                    if (smbl2 != null)
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.NonStaticFuncFromStatic));
                    else
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncWithNameNotFound));
                }
            }
            else if (callExpr.TypeOrObjectName.OutType is StructType structType)
            {
                // if we are calling like 'A.Anime()' where 'A' is a struct
                // we need to rename the func name call like that:
                newName = $"{structType.Declaration.Name.Name}::{callExpr.FuncName.Name}{callExpr.Arguments.GetArgsString()}";

                var smbl2 = GetFuncFromCandidates(newName, callExpr.Arguments.Select(x => x.Expr).ToList(), structType.Declaration.SubScope, structType.Declaration, out var casts);

                // check if the decl exists. if not - it could be non static method call from a class name
                if (smbl2 is DeclSymbol ds && ds.Decl is AstFuncDecl funcDecl)
                {
                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl))
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    newName = funcDecl.Name.Name;
                    callExpr.Arguments.ReplaceWithCasts(casts);
                }
                else
                {
                    // getting the name but with object first param
                    newName = $"{structType.Declaration.Name.Name}::{callExpr.FuncName.Name}{callExpr.Arguments.GetArgsString(PointerType.GetPointerType(structType))}";

                    List<AstExpression> argsWithStructParam = new List<AstExpression>(callExpr.Arguments);
                    var pseudoStructArg = new AstPointerExpr(callExpr.TypeOrObjectName, false, callExpr.TypeOrObjectName);
                    PostPrepareExprInference(pseudoStructArg);
                    argsWithStructParam.Insert(0, pseudoStructArg);
                    smbl2 = GetFuncFromCandidates(newName, argsWithStructParam, structType.Declaration.SubScope, structType.Declaration, out casts);

                    var declSymbolOfLeft = (callExpr.TypeOrObjectName.RightPart as AstIdExpr).FindSymbol as DeclSymbol;

                    if (smbl2 is DeclSymbol ds2 && ds2.Decl is AstFuncDecl funcDecl2)
                    {
                        // error because user tries to access non static method from a class name
                        if (declSymbolOfLeft.Decl is AstStructDecl)
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.NonStaticFuncFromStatic));
                        else
                        {
                            if (!CheckIfCouldBeAccessed(callExpr, funcDecl2) && !funcDecl2.IsPropertyFunction)
                                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                            newName = funcDecl2.Name.Name;
                            callExpr.Arguments.ReplaceWithCasts(casts);
                        }
                    }
                    else
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncWithNameNotFound));
                }
            }
            else if (callExpr.TypeOrObjectName.OutType is PointerType ptrTp2 && ptrTp2.TargetType is StructType strTp)
            {
                // if we are calling like 'A.Anime()' where 'A' is a struct
                // we need to rename the func name call like that:
                newName = $"{strTp.Declaration.Name.Name}::{callExpr.FuncName.Name}{callExpr.Arguments.GetArgsString()}";

                var smbl2 = GetFuncFromCandidates(newName, callExpr.Arguments.Select(x => x.Expr).ToList(), strTp.Declaration.SubScope, strTp.Declaration, out var casts);

                // check if the decl exists. if not - it could be non static method call from a class name
                if (smbl2 is DeclSymbol ds && ds.Decl is AstFuncDecl funcDecl)
                {
                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl))
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    newName = funcDecl.Name.Name;
                    callExpr.Arguments.ReplaceWithCasts(casts);
                }
                else
                {
                    // getting the name but with object first param
                    newName = $"{strTp.Declaration.Name.Name}::{callExpr.FuncName.Name}{callExpr.Arguments.GetArgsString(PointerType.GetPointerType(strTp))}";

                    List<AstExpression> argsWithStructParam = new List<AstExpression>(callExpr.Arguments);
                    var pseudoStructArg = callExpr.TypeOrObjectName;
                    argsWithStructParam.Insert(0, pseudoStructArg);
                    smbl2 = GetFuncFromCandidates(newName, argsWithStructParam, strTp.Declaration.SubScope, strTp.Declaration, out casts);

                    var declSymbolOfLeft = (callExpr.TypeOrObjectName.RightPart as AstIdExpr).FindSymbol as DeclSymbol;

                    if (smbl2 is DeclSymbol ds2 && ds2.Decl is AstFuncDecl funcDecl2)
                    {
                        // error because user tries to access non static method from a class name
                        if (declSymbolOfLeft.Decl is AstStructDecl)
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.NonStaticFuncFromStatic));
                        else
                        {
                            if (!CheckIfCouldBeAccessed(callExpr, funcDecl2) && !funcDecl2.IsPropertyFunction)
                                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                            newName = funcDecl2.Name.Name;
                            callExpr.Arguments.ReplaceWithCasts(casts.Skip(1).ToList());
                        }
                    }
                    else
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncWithNameNotFound));
                }
            }
            else
            {
                // error here: the function call could not be infered
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr, [], ErrorCode.Get(CTEN.FuncNotInfered));
            }
            callExpr.FuncName = callExpr.FuncName.GetCopy(newName);
            PostPrepareIdentifierInference(callExpr.FuncName, true);

            // setting parameters
            if (callExpr.FuncName.OutType is FunctionType ft)
            {
                // checking if it is a static func
                callExpr.StaticCall = ft.Declaration.SpecialKeys.Contains(TokenType.KwStatic);
                // call expr type is the same as func return type
                callExpr.OutType = ft.Declaration.Returns.OutType;

                // warn if accessing from an object
                if (accessingFromAnObject && callExpr.StaticCall)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTWN.StaticFuncFromObject), null, ReportType.Warning);
                }
            }
            else if (callExpr.FuncName.OutType is DelegateType dt)
            {
                // call expr type is the same as func return type
                callExpr.OutType = dt.Declaration.Returns.OutType;
            }
            else
            {
                // error here
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr, [], ErrorCode.Get(CTEN.CallNotFuncOrDelegate));
            }
        }

        private void PostPrepareCastExprInference(AstCastExpr castExpr)
        {
            PostPrepareExprInference(castExpr.SubExpression as AstExpression);
            PostPrepareExprInference(castExpr.TypeExpr as AstExpression);
            castExpr.OutType = (castExpr.TypeExpr as AstExpression).OutType;
            castExpr.OutValue = castExpr.OutValue; // WARN: is it ok just to pass the value?
        }

        private void PostPrepareNestedExprInference(AstNestedExpr nestExpr, out bool itWasPropa, bool propaSet = false)
        {
            // the var is used to check when static/const field is accessed from an object
            bool accessingFromAnObject = false;

            bool foundNs = false;
            // normalizing types with their namespaces
            InternalNormalizeLeftPartIfItIsANamespaceWithType(nestExpr, ref foundNs);

            if (nestExpr.LeftPart == null)
            {
                PostPrepareExprInference(nestExpr.RightPart);
                nestExpr.OutType = nestExpr.RightPart.OutType;
                nestExpr.OutValue = nestExpr.RightPart.OutValue;

                // kostyl to add 'this' as left part 
                if (nestExpr.RightPart is AstIdExpr idExpr && 
                    idExpr.FindSymbol is DeclSymbol dS && 
                    dS.Decl is AstVarDecl vD && 
                    (vD.ContainingParent is AstClassDecl || vD.ContainingParent is AstStructDecl) &&
                    !vD.SpecialKeys.Contains(TokenType.KwStatic) &&
                    !vD.SpecialKeys.Contains(TokenType.KwConst))
                {
                    var thisArg = new AstNestedExpr(new AstIdExpr("this", nestExpr), null, nestExpr);
                    SetScopeAndParent(thisArg, nestExpr);
                    PostPrepareExprScoping(thisArg);
                    PostPrepareExprInference(thisArg);
                    nestExpr.LeftPart = thisArg;
                }
            }
            else
            {
                Scope leftSideScope = null;
                PostPrepareExprInference(nestExpr.LeftPart);
                if (nestExpr.LeftPart.OutType is PointerType ptr && ptr.TargetType is ClassType classT)
                {
                    leftSideScope = classT.Declaration.SubScope;
                    accessingFromAnObject = true;
                }
                if (nestExpr.LeftPart.OutType is PointerType ptr2 && ptr2.TargetType is StructType structT)
                {
                    leftSideScope = structT.Declaration.SubScope;
                    accessingFromAnObject = true;
                }
                // this is usually when accesing static/const values
                // like 'Attribute.CoonstField'
                else if (nestExpr.LeftPart.OutType is ClassType classTT)
                    leftSideScope = classTT.Declaration.SubScope;
                else if (nestExpr.LeftPart.OutType is StructType structt)
                    leftSideScope = structt.Declaration.SubScope;
                else if (nestExpr.LeftPart.OutType is EnumType enumT)
                    leftSideScope = enumT.Declaration.SubScope;
                else if (nestExpr.LeftPart.OutType is StringType)
                    leftSideScope = AstStringExpr.GetStringStruct(nestExpr.Scope).SubScope;
                else if (nestExpr.LeftPart.OutType is ArrayType)
                    leftSideScope = AstArrayExpr.GetArrayStruct(nestExpr.Scope).SubScope;
                // TODO: structs and other

                if (leftSideScope == null)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, nestExpr.LeftPart, [], ErrorCode.Get(CTEN.ExprNotClassOrStruct));
                    itWasPropa = false;
                    return;
                }

                // here could only be an AstIdExpr because AstCallExpr and AstExpression would be in 'if' block upper
                if (nestExpr.RightPart is not AstIdExpr idExpr)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, nestExpr.RightPart, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                    itWasPropa = false;
                    return;
                }

                // searching for the symbol in the class/struct
                var smbl = leftSideScope.GetSymbol(idExpr.Name);
                if (smbl is DeclSymbol typed)
                {
                    idExpr.OutType = typed.Decl.Type.OutType;
                    nestExpr.OutType = idExpr.OutType;
                    nestExpr.OutValue = idExpr.OutValue;

                    // check if the var is a static/const field and user is accessing it from an object
                    if (typed.Decl is AstVarDecl varDecl && (varDecl.SpecialKeys.Contains(TokenType.KwStatic) || varDecl.SpecialKeys.Contains(TokenType.KwConst)) && accessingFromAnObject) // if accessing from an object - give em a warning :)
                    {
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [], ErrorCode.Get(CTWN.StaticFieldFromObject), null, HapetFrontend.Entities.ReportType.Warning);
                    }

                    // if the ast is an access to a property
                    if (typed.Decl is AstPropertyDecl)
                    {
                        // if getting property to set smth
                        if (propaSet)
                        {
                            itWasPropa = true;
                            return;
                        }
                        else
                        {
                            // if getting propa to get
                            var fncCall = new AstCallExpr(nestExpr.LeftPart, idExpr.GetCopy($"get_{idExpr}"), null, nestExpr);
                            SetScopeAndParent(fncCall, nestExpr.RightPart.NormalParent, nestExpr.RightPart.Scope);
                            nestExpr.LeftPart = null;
                            nestExpr.RightPart = fncCall;
                            PostPrepareCallExprInference(fncCall);
                        }
                    }
                }
                else
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [HapetType.AsString(nestExpr.LeftPart.OutType)], ErrorCode.Get(CTEN.SymbolNotFoundInType));
                }
            }
            itWasPropa = false;
        }

        // :)
        /// <summary>
        /// This shite is used to join namespace with type (if exist) to a one AstIdExpr as a right part
        /// If we have AstNested like 'System.Runtime.InteropServices.DllImportAttribute.DllName'
        /// I would like to have 'System.Runtime.InteropServices.DllImportAttribute' as one AstId
        /// Because it is just a type
        /// </summary>
        /// <param name="nestExpr">The shite</param>
        private void InternalNormalizeLeftPartIfItIsANamespaceWithType(AstNestedExpr nestExpr, ref bool found)
        {
            string flatten = nestExpr.TryFlatten(null, null);
            if (string.IsNullOrWhiteSpace(flatten))
                return; // no need to normalize this shite :)

            if (nestExpr.LeftPart == null)
                return;

            InternalNormalizeLeftPartIfItIsANamespaceWithType(nestExpr.LeftPart, ref found);

            // this could be a func call or array access
            if (nestExpr.LeftPart.RightPart is not AstIdExpr idExpr)
                return;

            // check is it namespace
            string leftString = idExpr.Name;
            bool foundNs = nestExpr.Scope.IsStringNamespaceOrPart(leftString);
            // go all over the usings
            foreach (var usng in _currentSourceFile.Usings)
            {
                // getting ns string
                var ns = usng.FlattenNamespace;
                if (nestExpr.Scope.IsStringNamespaceOrPart($"{ns}.{leftString}"))
                {
                    foundNs = true;
                    break;
                }
            }

            // check is it namespace
            if (foundNs)
            {
                // if it is a namespace - join with current right side and try again
                nestExpr.RightPart = (nestExpr.RightPart as AstIdExpr).GetCopy($"{leftString}.{(nestExpr.RightPart as AstIdExpr).Name}");
                nestExpr.LeftPart = null;
            }
            else
            {
                if (!found)
                {
                    // if it is not a namespace - then probably type is done
                    nestExpr.LeftPart.LeftPart = null;
                    nestExpr.LeftPart.RightPart.Location = nestExpr.LeftPart.Location;
                    found = true;
                }
            }
        }

        private void PostPrepareArrayExprInference(AstArrayExpr arrayExpr)
        {
            PostPrepareExprInference(arrayExpr.SubExpression);
            arrayExpr.OutType = ArrayType.GetArrayType(arrayExpr.SubExpression.OutType, arrayExpr.Scope);
        }

        private void PostPrepareArrayCreateExprInference(AstArrayCreateExpr arrayExpr)
        {
            foreach (var sz in arrayExpr.SizeExprs)
            {
                PostPrepareExprInference(sz);
            }
            // TODO: you can check if the size is available at compile time and create the array on stack

            PostPrepareExprInference(arrayExpr.TypeName);

            // create an expecting elements type to be
            HapetType expectingElementType = arrayExpr.TypeName.OutType;
            int sizeAmount = arrayExpr.SizeExprs.Count;
            // preparing for ndim arrays
            while (sizeAmount > 1)
            {
                expectingElementType = ArrayType.GetArrayType(expectingElementType, arrayExpr.Scope);
                sizeAmount--;
            }

            // infer elements
            for (int i = 0; i < arrayExpr.Elements.Count; ++i)
            {
                var e = arrayExpr.Elements[i];
                PostPrepareExprInference(e);
                // try to use implicit cast if it can be used
                arrayExpr.Elements[i] = PostPrepareExpressionWithType(expectingElementType, e);
            }

            // preparing the array
            PostPrepareFullArray(arrayExpr);
        }

        private void PostPrepareArrayAccessExprInference(AstArrayAccessExpr arrayAccExpr)
        {
            PostPrepareExprInference(arrayAccExpr.ParameterExpr);
            PostPrepareExprInference(arrayAccExpr.ObjectName);

            if (arrayAccExpr.ParameterExpr.OutType is not IntType)
            {
                // error here? i cannot access array if it is not an int type
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, arrayAccExpr.ParameterExpr, [], ErrorCode.Get(CTEN.ArrayIndexNotInt));
            }

            HapetType outType = null;
            if (arrayAccExpr.ObjectName.OutType is ArrayType arrayType)
                outType = arrayType.TargetType;
            else if (arrayAccExpr.ObjectName.OutType is StringType)
                outType = CharType.DefaultType; // TODO: mb non default could be here? idk :)
            else if (arrayAccExpr.ObjectName.OutType is PointerType ptrType)
                outType = ptrType.TargetType;
            else
            {
                // error because expected an array 
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, arrayAccExpr.ObjectName, [], ErrorCode.Get(CTEN.NonStringOrArrayIndexed));
            }
            arrayAccExpr.OutType = outType;
        }

        // statements
        private void PostPrepareAssignStmtInference(AstAssignStmt assignStmt, out bool itWasPropa)
        {
            // propaSet is true only here
            PostPrepareNestedExprInference(assignStmt.Target, out itWasPropa, true);

            // cringe error when user tries to assign something directly into enum field
            if (assignStmt.Target.LeftPart != null && assignStmt.Target.LeftPart.OutType is EnumType)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, assignStmt, [], ErrorCode.Get(CTEN.EnumFieldAssigned));
                return;
            }
            // pp assign value
            if (assignStmt.Value != null)
                assignStmt.Value = PostPrepareVarValueAssign(assignStmt.Value, assignStmt.Target.OutType);
            else
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, assignStmt, [], ErrorCode.Get(CTEN.NotExprInAssignment));
        }

        private void PostPrepareForStmtInference(AstForStmt forStmt)
        {
            if (forStmt.FirstParam != null)
                PostPrepareExprInference(forStmt.FirstParam);
            if (forStmt.SecondParam != null)
            {
                PostPrepareExprInference(forStmt.SecondParam);

                // error if it is not a bool type because it has to be
                if (forStmt.SecondParam.OutType is not BoolType)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, forStmt.SecondParam, [], ErrorCode.Get(CTEN.ExprIsNotBool));
                }
            }
            if (forStmt.ThirdParam != null)
                PostPrepareExprInference(forStmt.ThirdParam);

            PostPrepareExprInference(forStmt.Body);
        }

        private void PostPrepareWhileStmtInference(AstWhileStmt whileStmt)
        {
            PostPrepareExprInference(whileStmt.ConditionParam);

            // error if it is not a bool type because it has to be
            if (whileStmt.ConditionParam.OutType is not BoolType)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, whileStmt.ConditionParam, [], ErrorCode.Get(CTEN.ExprIsNotBool));
            }

            PostPrepareExprInference(whileStmt.Body);
        }

        private void PostPrepareIfStmtInference(AstIfStmt ifStmt)
        {
            PostPrepareExprInference(ifStmt.Condition);

            // error if it is not a bool type because it has to be
            if (ifStmt.Condition.OutType is not BoolType)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, ifStmt.Condition, [], ErrorCode.Get(CTEN.ExprIsNotBool));
            }

            PostPrepareExprInference(ifStmt.BodyTrue);
            if (ifStmt.BodyFalse != null)
                PostPrepareExprInference(ifStmt.BodyFalse);
        }

        private void PostPrepareSwitchStmtInference(AstSwitchStmt switchStmt)
        {
            PostPrepareExprInference(switchStmt.SubExpression);

            // used to check that there are no more than 1 default case
            bool thereWasADefaultCase = false;

            foreach (var cc in switchStmt.Cases)
            {
                PostPrepareExprInference(cc);

                // calc default cases. if there are more than 1 - error
                if (cc.DefaultCase)
                {
                    if (thereWasADefaultCase)
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, cc.Pattern, [], ErrorCode.Get(CTEN.MultipleDefaultCases));
                    thereWasADefaultCase = true;
                    continue; // do not check for pattern in default expr...
                }

                // trying to implicitly cast cast value into switch sub expr
                cc.Pattern = PostPrepareExpressionWithType(switchStmt.SubExpression.OutType, cc.Pattern);

                // check that the value is a const 
                if (cc.Pattern.OutValue == null)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, cc.Pattern, [], ErrorCode.Get(CTEN.NonConstantCaseValue));
                }
            }
        }

        private void PostPrepareCaseStmtInference(AstCaseStmt caseStmt)
        {
            if (!caseStmt.DefaultCase)
                PostPrepareExprInference(caseStmt.Pattern);

            if (!caseStmt.FallingCase)
                PostPrepareExprInference(caseStmt.Body);
        }

        private void PostPrepareReturnStmtInference(AstReturnStmt returnStmt)
        {
            if (returnStmt.ReturnExpression != null)
            {
                // if user tries to return smth but func ret type is void =^0
                if (_currentFunction.Returns.OutType is VoidType)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, _currentFunction.Name, [], ErrorCode.Get(CTEN.EmptyReturnExpected));
                    return;
                }

                // do not infer this shite
                if (_currentFunction.Returns.OutType is not DelegateType)
                    PostPrepareExprInference(returnStmt.ReturnExpression);
                // casting to func return type
                returnStmt.ReturnExpression = PostPrepareExpressionWithType(_currentFunction.Returns.OutType, returnStmt.ReturnExpression);
            }
            else if (returnStmt.ReturnExpression == null && _currentFunction.Returns.OutType is not VoidType)
            {
                // TODO: better return stmts checks. like in if/else blocks and so on
                if (returnStmt.Location == null)
                {
                    // it is a manually added 'return' statement
                    var theFunc = returnStmt.FindContainingFunction();
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, theFunc.Name, [HapetType.AsString(_currentFunction.Returns.OutType)], ErrorCode.Get(CTEN.NotEnoughReturns));
                }
                else
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, returnStmt, [HapetType.AsString(_currentFunction.Returns.OutType)], ErrorCode.Get(CTEN.EmptyReturnStmt));
            }
        }

        private void PostPrepareAttributeStmtInference(AstAttributeStmt attrStmt)
        {
            // purified type string with namespace in it!
            // we need this so when saving the attributes into metafile 
            // we would know namespace of the attribute and so on.
            // (kostyl?)
            var newTypeAst = attrStmt.AttributeName.GetTypeAstId(_compiler.MessageHandler, _currentSourceFile);
            PostPrepareExprInference(newTypeAst);
            attrStmt.AttributeName.SetTypeAstId(newTypeAst);

            // check that the attr ast was infered properly
            if (attrStmt.AttributeName.OutType == null)
                return;

            // TODO: check that the shite is inherited from 'System.Attribute'
            // getting all the fields of attribuute class decl
            var attrDeclFields = (attrStmt.AttributeName.OutType as ClassType).Declaration.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl).ToList();
            // check that not too much params
            if (attrStmt.Parameters.Count > attrDeclFields.Count)
            {
                var beg = attrStmt.Parameters[attrDeclFields.Count].Beginning;
                var end = attrStmt.Parameters[attrStmt.Parameters.Count - 1].Ending;
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, new Location(beg, end), [attrDeclFields.Count.ToString(), attrStmt.Parameters.Count.ToString()], ErrorCode.Get(CTEN.WrongAttrArgs));
            }

            for (int i = 0; i < attrDeclFields.Count; ++i)
            {
                var theAttrField = attrDeclFields[i];

                // check that param exists for the field 
                if (i < attrStmt.Parameters.Count)
                {
                    // inferrencing the param
                    var a = attrStmt.Parameters[i];
                    PostPrepareExprInference(a);
                    // all attr params has to be const values
                    if (a.OutValue == null)
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, a, [], ErrorCode.Get(CTEN.NonComptimeAttrArg));

                    // is going to error if they are different types :)
                    attrStmt.Parameters[i] = PostPrepareExpressionWithType(theAttrField.Type.OutType, a);
                }
                else
                {
                    // this cringe is done because current attribute requires RequiredAttribute to be inferred
                    foreach (var aa in theAttrField.Attributes)
                    {
                        if (aa.AttributeName.OutType == null)
                        {
                            var savedSourceFile = _currentSourceFile;
                            _currentSourceFile = theAttrField.SourceFile;
                            PostPrepareAttributeStmtInference(aa);
                            _currentSourceFile = savedSourceFile;
                        }
                    }

                    // check if the field is required but there are no more params - error
                    string reqAttrName = "System.RequiredAttribute";
                    var reqAttr = theAttrField.Attributes.FirstOrDefault(x => x.AttributeName.TryFlatten(_compiler.MessageHandler, _currentSourceFile) == reqAttrName);
                    if (reqAttr != null)
                    {
                        // there was a required attr and no param for the field - error
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, attrStmt.Ending, [theAttrField.Name.Name], ErrorCode.Get(CTEN.NonSpecifiedRequired));
                    }
                }
            }
        }

        private void PostPrepareBaseCtorStmtInference(AstBaseCtorStmt baseStmt)
        {
            // resolve args
            foreach (var a in baseStmt.Arguments)
            {
                PostPrepareExprInference(a);
            }
        }

        private AstExpression PostPrepareVarValueAssign(AstExpression value, HapetType targetType)
        {
            if (value is AstDefaultExpr)
            {
                // get the default value for the type (no need to infer)
                value = AstDefaultExpr.GetDefaultValueForType(targetType, value);
                if (value == null)
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, value, [], ErrorCode.Get(CTEN.DefaultValueNotFound));
            }
            // do not infer the expr if target is a delegate
            else if (targetType is not DelegateType)
            {
                // if it is not a default
                PostPrepareExprInference(value);
            }
            return PostPrepareExpressionWithType(targetType, value);
        }

        private bool CheckIfCouldBeAccessed(AstStatement accessor, AstDeclaration accessee)
        {
            // could be accessed from everyone
            if (accessee.SpecialKeys.Contains(TokenType.KwPublic))
                return true;

            // TODO: check protected
            // TODO: check private protected
            // TODO: check protected internal

            if (accessee.SpecialKeys.Contains(TokenType.KwInternal))
            {
                // check just by root namespace names
                string asm1 = accessor.SourceFile.Namespace.Split(".").First();
                string asm2 = accessee.SourceFile.Namespace.Split(".").First();
                return asm1 == asm2;
            }

            // they are the same 
            if (accessee.SpecialKeys.Contains(TokenType.KwPrivate) || accessee.SpecialKeys.Contains(TokenType.KwUnreflected))
            {
                // this shite could be accessable in the same namespace
                if (accessee is AstClassDecl ||
                    accessee is AstStructDecl ||
                    accessee is AstEnumDecl ||
                    accessee is AstDelegateDecl)
                {
                    return accessor.SourceFile.Namespace == accessee.SourceFile.Namespace;
                }
                else
                {
                    // if the decl has child shite
                    if (accessee.SubScope != null)
                    {
                        if (accessee.SubScope.IsParentOf(accessor.Scope))
                            return true;
                    }

                    // if the decl is func of field 
                    // and accessed in the same class
                    var parent = accessee switch
                    {
                        AstVarDecl vd => vd.ContainingParent,
                        AstFuncDecl fd => fd.ContainingParent,
                        _ => null
                    };
                    if (parent != null)
                    {
                        return parent.Scope.IsParentOf(accessor.Scope);
                    }

                    return false;
                }
            }

            // could be a usual variable/param
            if (accessee is AstParamDecl ||
                (accessee is AstVarDecl vd2 && vd2.ContainingParent == null))
            {
                return true;
            }

            return false;
        }
    }
}
