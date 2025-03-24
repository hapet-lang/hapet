using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;
using System;
using System.Threading;
using System.Xml.Linq;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareTypeInference()
        {
            _currentPreparationStep = PreparationStep.Inferencing;

            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            var classes = AllClassesMetadata.ToList();
            var structs = AllStructsMetadata.ToList();
            var enums = AllEnumsMetadata.ToList();
            var delegates = AllDelegatesMetadata.ToList();

            foreach (var classDecl in classes)
            {
                _currentSourceFile = classDecl.SourceFile;
                PostPrepareClassInference(classDecl, inInfo, ref outInfo);
            }
            foreach (var structDecl in structs)
            {
                _currentSourceFile = structDecl.SourceFile;
                PostPrepareStructInference(structDecl, inInfo, ref outInfo);
            }
            foreach (var enumDecl in enums)
            {
                _currentSourceFile = enumDecl.SourceFile;
                PostPrepareEnumInference(enumDecl, inInfo, ref outInfo);
            }
            foreach (var delegateDecl in delegates)
            {
                _currentSourceFile = delegateDecl.SourceFile;
                PostPrepareDelegateInference(delegateDecl, inInfo, ref outInfo);
            }
        }

        private void PostPrepareClassInference(AstClassDecl classDecl, InInfo inInfo, ref OutInfo outInfo)
        {
            _currentClass = classDecl;

            /// WARN: attributes are inferrenced in <see cref="PostPrepareMetadataAttributes"/>

            /// fields should be already inferred in <see cref="PostPrepareMetadataTypes"/> and <see cref="PostPrepareMetadataTypeFields"/>
            foreach (var decl in classDecl.Declarations.Where(x => x is AstFuncDecl fnc && !(fnc.HasGenericTypes && !fnc.IsImplOfGeneric)).Select(x => x as AstFuncDecl))
            {
                PostPrepareFunctionInference(decl, inInfo, ref outInfo);
            }

            /// some shite is already inferrenced in <see cref="PostPrepareMetadataTypeFieldDecls"/>
        }

        private void PostPrepareStructInference(AstStructDecl structDecl, InInfo inInfo, ref OutInfo outInfo)
        {
            /// WARN: should be already inferred in <see cref="PostPrepareMetadataTypes"/> and <see cref="PostPrepareMetadataTypeFields"/>
            /// WARN: attributes are inferrenced in <see cref="PostPrepareMetadataAttributes"/>
            /// 
            foreach (var decl in structDecl.Declarations.Where(x => x is AstFuncDecl fnc && !(fnc.HasGenericTypes && !fnc.IsImplOfGeneric)).Select(x => x as AstFuncDecl))
            {
                PostPrepareFunctionInference(decl, inInfo, ref outInfo);
            }

            /// some shite is already inferrenced in <see cref="PostPrepareMetadataTypeFields"/>
        }

        private void PostPrepareEnumInference(AstEnumDecl enumDecl, InInfo inInfo, ref OutInfo outInfo)
        {
            /// WARN: should be already inferred in <see cref="PostPrepareMetadataTypes"/> and <see cref="PostPrepareMetadataTypeFields"/>
            /// WARN: attributes are inferrenced in <see cref="PostPrepareMetadataAttributes"/>
        }

        private void PostPrepareDelegateInference(AstDelegateDecl delegateDecl, InInfo inInfo, ref OutInfo outInfo)
        {
            /// WARN: should be already inferred in <see cref="PostPrepareMetadataTypes"/> and <see cref="PostPrepareMetadataTypeFields"/>
            /// WARN: attributes are inferrenced in <see cref="PostPrepareMetadataAttributes"/>

            // inferencing parameters 
            foreach (var p in delegateDecl.Parameters)
            {
                PostPrepareParamInference(p, inInfo, ref outInfo);
            }

            // inferencing return type 
            {
                PostPrepareExprInference(delegateDecl.Returns, inInfo, ref outInfo);
            }
        }

        private void PostPrepareFunctionInference(AstFuncDecl funcDecl, InInfo inInfo, ref OutInfo outInfo)
        {
            /// WARN: attributes are inferrenced in <see cref="PostPrepareMetadataAttributes"/>

            _currentFunction = funcDecl;

            // if the function inference is for metadata - infer everything except body
            // if not - infer only body because func decl already infered from metadata :)
            if (inInfo.ForMetadata)
            {
                // inferencing parameters 
                // mute all inference errors for param types of property func. 
                // if has to be errored somewhere else
                var savedMute = inInfo.MuteErrors;
                if (funcDecl.IsPropertyFunction)
                    inInfo.MuteErrors = true;
                foreach (var p in funcDecl.Parameters)
                {
                    PostPrepareParamInference(p, inInfo, ref outInfo);
                }
                if (funcDecl.IsPropertyFunction)
                    inInfo.MuteErrors = savedMute;

                // inferencing return type 
                {
                    // mute all inference errors for return type of property get_ func. 
                    // if has to be errored somewhere else
                    savedMute = inInfo.MuteErrors;
                    if (funcDecl.IsPropertyFunction)
                        inInfo.MuteErrors = true;
                    PostPrepareExprInference(funcDecl.Returns, inInfo, ref outInfo);
                    if (funcDecl.IsPropertyFunction)
                        inInfo.MuteErrors = savedMute;

                    // don't do this for generic shite
                    if (funcDecl.Returns.OutType is ClassType)
                    {
                        // the return type is actually a pointer to the class
                        var astPtr = new AstPointerExpr(funcDecl.Returns, false, funcDecl.Returns.Location);
                        astPtr.OutType = PointerType.GetPointerType(astPtr.SubExpression.OutType);
                        astPtr.Scope = funcDecl.Returns.Scope;
                        funcDecl.Returns = new AstNestedExpr(astPtr, null, funcDecl.Returns.Location) { OutType = astPtr.OutType };
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
                        if (overDecl2.OverloadType == OverloadType.UnaryOperator)
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
                // if it is a ini_func and cls contains Generic shite - do not infer
                bool allowInfer = funcDecl.ContainingParent is not AstClassDecl clsDecl22 || !(clsDecl22.HasGenericTypes && !clsDecl22.IsImplOfGeneric);
                // inferring body
                if (funcDecl.Body != null && allowInfer)
                    PostPrepareBlockInference(funcDecl.Body, inInfo, ref outInfo);

                // check if the class if inherited from smth
                // and call base ctor
                if (funcDecl.ClassFunctionType == HapetFrontend.Enums.ClassFunctionType.Ctor &&
                    funcDecl.ContainingParent is AstClassDecl clsDecl &&
                    clsDecl.InheritedFrom.Count > 0 &&
                    funcDecl.BaseCtorCall != null &&
                    clsDecl.InheritedFrom[0].OutType is ClassType baseType &&
                    !baseType.Declaration.IsInterface)
                {
                    PostPrepareExprInference(funcDecl.BaseCtorCall, inInfo, ref outInfo);

                    // preparing shite for easier code gen
                    funcDecl.BaseCtorCall.BaseType = baseType;
                    var thisArg = new AstIdExpr("this", funcDecl.BaseCtorCall);
                    SetScopeAndParent(thisArg, funcDecl.Body, funcDecl.Body.SubScope);
                    PostPrepareExprInference(thisArg, inInfo, ref outInfo);
                    funcDecl.BaseCtorCall.ThisArgument = thisArg;

                    // we need to insert it into block so it would be generated normally
                    // but why to the index 1? - https://stackoverflow.com/questions/140490/base-constructor-in-c-sharp-which-gets-called-first
                    funcDecl.Body.Statements.Insert(1, funcDecl.BaseCtorCall);
                }
            }
        }

        private void PostPrepareVarInference(AstVarDecl varDecl, InInfo inInfo, ref OutInfo outInfo)
        {
            // mute all inference errors for var type of property. 
            // if has to be errored somewhere else
            var savedMute = inInfo.MuteErrors;
            if (varDecl.IsPropertyField)
                inInfo.MuteErrors = true;
            PostPrepareExprInference(varDecl.Type, inInfo, ref outInfo);
            if (varDecl.IsPropertyField)
                inInfo.MuteErrors = savedMute;

            if (varDecl.Initializer != null)
                PostPrepareExprInference(varDecl.Initializer, inInfo, ref outInfo);

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

            // don't do this for generic shite
            if (varDecl.Type.OutType is ClassType)
            {
                // the var is actually a pointer to the class
                var astPtr = new AstPointerExpr(varDecl.Type, false, varDecl.Type.Location);
                astPtr.OutType = PointerType.GetPointerType(astPtr.SubExpression.OutType);
                astPtr.Scope = varDecl.Type.Scope;
                varDecl.Type = new AstNestedExpr(astPtr, null, varDecl.Type.Location) { OutType = astPtr.OutType };
            }

            // pp assign value
            if (varDecl.Initializer != null)
                varDecl.Initializer = PostPrepareVarValueAssign(varDecl.Initializer, varDecl.Type.OutType, inInfo, ref outInfo);

            // special keys could not be allowed when the var is declared in BlockExpr
            if (!inInfo.AllowSpecialKeys)
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

        private void PostPrepareParamInference(AstParamDecl paramDecl, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(paramDecl.Type, inInfo, ref outInfo);

            // don't do this for generic shite
            if (paramDecl.Type.OutType is ClassType)
            {
                // the var is actually a pointer to the class
                var astPtr = new AstPointerExpr(paramDecl.Type, false, paramDecl.Type.Location);
                astPtr.Scope = paramDecl.Type.Scope;
                paramDecl.Type = new AstNestedExpr(astPtr, null, paramDecl.Type.Location) { OutType = astPtr.OutType };
                PostPrepareExprInference(paramDecl.Type, inInfo, ref outInfo);
            }

            if (paramDecl.DefaultValue != null)
                PostPrepareExprInference(paramDecl.DefaultValue, inInfo, ref outInfo);
        }

        private void PostPrepareExprInference(AstStatement expr, InInfo inInfo, ref OutInfo outInfo)
        {
            switch (expr)
            {
                // special case at least for 'for' loop
                // when 'for (int i = 0;...)' where 'int i' 
                // would not be handled by blockExpr
                case AstVarDecl varDecl:
                    PostPrepareVarInference(varDecl, inInfo, ref outInfo);
                    break;

                case AstBlockExpr blockExpr:
                    PostPrepareBlockInference(blockExpr, inInfo, ref outInfo);
                    break;
                case AstUnaryExpr unExpr:
                    PostPrepareUnaryExprInference(unExpr, inInfo, ref outInfo);
                    break;
                case AstBinaryExpr binExpr:
                    PostPrepareBinaryExprInference(binExpr, inInfo, ref outInfo);
                    break;
                case AstPointerExpr pointerExpr:
                    PostPreparePointerExprInference(pointerExpr, inInfo, ref outInfo);
                    break;
                case AstAddressOfExpr addrExpr:
                    PostPrepareAddressOfExprInference(addrExpr, inInfo, ref outInfo);
                    break;
                case AstNewExpr newExpr:
                    PostPrepareNewExprInference(newExpr, inInfo, ref outInfo);
                    break;
                case AstArgumentExpr argumentExpr:
                    PostPrepareArgumentExprInference(argumentExpr, inInfo, ref outInfo);
                    break;
                case AstIdExpr idExpr:
                    PostPrepareIdentifierInference(idExpr, inInfo, ref outInfo);
                    return;
                case AstCallExpr callExpr:
                    PostPrepareCallExprInference(callExpr, inInfo, ref outInfo);
                    break;
                case AstCastExpr castExpr:
                    PostPrepareCastExprInference(castExpr, inInfo, ref outInfo);
                    break;
                case AstNestedExpr nestExpr:
                    PostPrepareNestedExprInference(nestExpr, inInfo, ref outInfo);
                    break;
                case AstDefaultExpr defaultExpr:
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, defaultExpr, [], ErrorCode.Get(CTEN.DefaultWasNotInfered));
                    break;
                case AstArrayExpr arrayExpr:
                    PostPrepareArrayExprInference(arrayExpr, inInfo, ref outInfo);
                    break;
                case AstArrayCreateExpr arrayCreateExpr:
                    PostPrepareArrayCreateExprInference(arrayCreateExpr, inInfo, ref outInfo);
                    break;
                case AstArrayAccessExpr arrayAccExpr:
                    PostPrepareArrayAccessExprInference(arrayAccExpr, inInfo, ref outInfo);
                    break;
                case AstStringExpr stringExpr:
                    stringExpr.OutType = StringType.GetInstance(stringExpr.Scope);
                    break;

                // statements
                case AstAssignStmt assignStmt:
                    // _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, assignStmt, "(Compiler exception) The statement has to be handled by block expr");
                    PostPrepareAssignStmtInference(assignStmt, inInfo, ref outInfo);
                    break;
                case AstForStmt forStmt:
                    PostPrepareForStmtInference(forStmt, inInfo, ref outInfo);
                    break;
                case AstWhileStmt whileStmt:
                    PostPrepareWhileStmtInference(whileStmt, inInfo, ref outInfo);
                    break;
                case AstIfStmt ifStmt:
                    PostPrepareIfStmtInference(ifStmt, inInfo, ref outInfo);
                    break;
                case AstSwitchStmt switchStmt:
                    PostPrepareSwitchStmtInference(switchStmt, inInfo, ref outInfo);
                    break;
                case AstCaseStmt caseStmt:
                    PostPrepareCaseStmtInference(caseStmt, inInfo, ref outInfo);
                    break;
                case AstBreakContStmt _:
                    break;
                case AstReturnStmt returnStmt:
                    PostPrepareReturnStmtInference(returnStmt, inInfo, ref outInfo);
                    break;
                case AstAttributeStmt attrStmt:
                    PostPrepareAttributeStmtInference(attrStmt, inInfo, ref outInfo);
                    break;
                case AstBaseCtorStmt baseStmt:
                    PostPrepareBaseCtorStmtInference(baseStmt, inInfo, ref outInfo);
                    break;
                // TODO: check other expressions

                default:
                    {
                        // TODO: anything to do here?
                        break;
                    }
            }
        }

        private void PostPrepareBlockInference(AstBlockExpr blockExpr, InInfo inInfo, ref OutInfo outInfo)
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
                    PostPrepareAssignStmtInference(asgn, inInfo, ref outInfo);
                    if (outInfo.ItWasProperty)
                    {
                        // reset
                        outInfo.ItWasProperty = false;

                        AstIdExpr propaName = (asgn.Target.RightPart as AstIdExpr);
                        // creating a call 
                        var fncVal = new AstArgumentExpr(asgn.Value, null, asgn.Target);
                        var fncCall = new AstCallExpr(asgn.Target.LeftPart, propaName.GetCopy($"set_{propaName.Name}"), new List<AstArgumentExpr>() { fncVal }, asgn);
                        SetScopeAndParent(fncCall, asgn.Target.NormalParent, asgn.Target.Scope);
                        PostPrepareCallExprScoping(fncCall);
                        PostPrepareCallExprInference(fncCall, inInfo, ref outInfo);
                        repls.Add(asgn, fncCall);
                    }
                    else if (outInfo.ItWasIndexer)
                    {
                        // reset
                        outInfo.ItWasIndexer = false;

                        // if getting indexer to get
                        var fncName = new AstIdExpr("set_indexer__", asgn.Target);
                        fncName.Scope = asgn.Target.Scope;
                        var fncArg = new AstArgumentExpr(outInfo.IndexedIndex, null, asgn.Target);
                        var fncVal = new AstArgumentExpr(asgn.Value, null, asgn.Target);
                        var fncCall = new AstCallExpr(outInfo.IndexedObject, fncName, new List<AstArgumentExpr>() { fncArg, fncVal }, asgn);
                        SetScopeAndParent(fncCall, asgn.Target.NormalParent, asgn.Target.Scope);
                        PostPrepareCallExprScoping(fncCall);
                        PostPrepareCallExprInference(fncCall, inInfo, ref outInfo);
                        repls.Add(asgn, fncCall);
                    }
                }
                else
                {
                    PostPrepareExprInference(stmt, inInfo, ref outInfo);
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

        private void PostPrepareUnaryExprInference(AstUnaryExpr unExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // TODO: check for the right size for an existance value (compiletime evaluated) and do some shite (set unExpr OutValue)
            PostPrepareExprInference(unExpr.SubExpr as AstExpression, inInfo, ref outInfo);
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

        private void PostPrepareBinaryExprInference(AstBinaryExpr binExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // resolve the actual operator in the current scope
            PostPrepareExprInference(binExpr.Left as AstExpression, inInfo, ref outInfo);
            PostPrepareExprInference(binExpr.Right as AstExpression, inInfo, ref outInfo);
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
                                    PostPrepareExprInference(rightExpr, inInfo, ref outInfo);
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
                                    PostPrepareExprInference(leftExpr, inInfo, ref outInfo);
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
                                binExpr.Left = PostPrepareExpressionWithType(GetPreparedAst(binExpr.OutType, binExpr), leftExpr);
                            }
                            // creating cast to result type if it is not a bool expr
                            if (rightExpr.OutType != binExpr.OutType && 
                                binExpr.OutType is not BoolType && 
                                binExpr.OutType is not PointerType &&
                                binExpr.ActualOperator is not IUserDefinedOperator)
                            {
                                // cast if they are not the same haha
                                binExpr.Right = PostPrepareExpressionWithType(GetPreparedAst(binExpr.OutType, binExpr), rightExpr);
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
                                    binExpr.Right = PostPrepareExpressionWithType(GetPreparedAst(castingType, binExpr), rightExpr);
                                else
                                    binExpr.Left = PostPrepareExpressionWithType(GetPreparedAst(castingType, binExpr), leftExpr);
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

        private void PostPreparePointerExprInference(AstPointerExpr pointerExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // prepare the right side
            PostPrepareExprInference(pointerExpr.SubExpression, inInfo, ref outInfo);
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

        private void PostPrepareAddressOfExprInference(AstAddressOfExpr addrExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // prepare the right side
            PostPrepareExprInference(addrExpr.SubExpression, inInfo, ref outInfo);
            // create a new reference type from the right side and set the type to itself
            addrExpr.OutType = ReferenceType.GetRefType(addrExpr.SubExpression.OutType);
        }

        private void PostPrepareNewExprInference(AstNewExpr newExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // prepare the right side
            PostPrepareExprInference(newExpr.TypeName, inInfo, ref outInfo);
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
                PostPrepareExprInference(a, inInfo, ref outInfo);
            }
        }

        private void PostPrepareArgumentExprInference(AstArgumentExpr argumentExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(argumentExpr.Expr, inInfo, ref outInfo);

            if (argumentExpr.Name != null)
            {
                PostPrepareExprInference(argumentExpr.Name, inInfo, ref outInfo);
            }

            // the argument type is the same as its expr type
            argumentExpr.OutType = argumentExpr.Expr.OutType;
            // if the value could be evaluated at the compile time
            if (argumentExpr.Expr.OutValue != null)
            {
                argumentExpr.OutValue = argumentExpr.Expr.OutValue;
            }
        }

        private void PostPrepareIdentifierInference(AstIdExpr idExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            string name = idExpr.Name;

            // infer generic names
            if (idExpr is AstIdGenericExpr genId)
            {
                for (int i = 0; i < genId.GenericRealTypes.Count; ++i)
                {
                    var g = genId.GenericRealTypes[i];
                    PostPrepareExprInference(g, inInfo, ref outInfo);
                }
            }

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
                OnFoundSymbol(typed, string.Empty, ref outInfo);
                return;
            }

            // searching for the name with current class name
            // works only for functions
            string nameWithClass = $"{_currentClass.Name.Name}::{name}";
            var smblInLocalClass = idExpr.Scope.GetSymbol(nameWithClass);
            if (smblInLocalClass is DeclSymbol typed2)
            {
                OnFoundSymbol(typed2, nameWithClass, ref outInfo);
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
                PostPrepareIdentifierInference(leftPartId, inInfo, ref outInfo);

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
                    OnFoundSymbol(typed4, fullFuncName, ref outInfo);
                    return;
                }
            }

            // searching for the name with namespace
            // works only for types/objects
            string nameWithNamespace = $"{_currentSourceFile.Namespace}.{name}";
            var smblInLocalFile = idExpr.Scope.GetSymbol(nameWithNamespace);
            if (smblInLocalFile is DeclSymbol typed3)
            {
                OnFoundSymbol(typed3, nameWithNamespace, ref outInfo);
                return;
            }

            // check if it is smth like 'System.Attribute' where 'System' is ns and 'Attribute' is a class
            if (!string.IsNullOrWhiteSpace(name.GetNamespaceWithoutClassName()))
            {
                var leftPart = name.GetNamespaceWithoutClassName();
                var rightPart = name.GetClassNameWithoutNamespace();

                // getting a symbol from namespace
                var includedSmbl = idExpr.Scope.GetSymbolInNamespace(leftPart, rightPart);
                if (includedSmbl is DeclSymbol typed4)
                {
                    OnFoundSymbol(typed4, string.Empty, ref outInfo);
                    return;
                }
            }
            
            // check for shite inside current class
            if (_currentClass != null)
            {
                var symbolsInsideCls = _currentClass.SubScope.SymbolTable;
                foreach (var smbl2 in symbolsInsideCls)
                {
                    if (smbl2.Key == idExpr.Name && smbl2.Value is DeclSymbol ds1)
                    {
                        OnFoundSymbol(ds1, string.Empty, ref outInfo);
                        return;
                    }
                    // check for func
                    else if (smbl2.Key.StartsWith($"{_currentClass.Name.Name}::{idExpr.Name}(") && smbl2.Value is DeclSymbol ds2)
                    {
                        OnFoundSymbol(ds2, string.Empty, ref outInfo);
                        return;
                    }
                }
            }

            // go all over the usings
            foreach (var usng in _currentSourceFile.Usings)
            {
                // getting ns string
                var ns = usng.FlattenNamespace;

                // check if it is smth like 'Runtime.InteropServices.DllImportAttribute'
                // where 'Runtime.InteropServices' is PART! of ns and 'DllImportAttribute' is a class
                if (!string.IsNullOrWhiteSpace(name.GetNamespaceWithoutClassName()))
                {
                    var leftPart = name.GetNamespaceWithoutClassName();
                    var rightPart = name.GetClassNameWithoutNamespace();

                    // getting a symbol from namespace
                    var includedSmbl = idExpr.Scope.GetSymbolInNamespace($"{ns}.{leftPart}", rightPart);
                    if (includedSmbl is DeclSymbol typed4)
                    {
                        OnFoundSymbol(typed4, string.Empty, ref outInfo);
                        return;
                    }
                }

                // try just get the name from using namespace
                string fullNameWithNs = $"{ns}.{name}";
                var usedSmbl = idExpr.Scope.GetSymbolInNamespace(ns, name);
                if (usedSmbl is DeclSymbol typed5)
                {
                    OnFoundSymbol(typed5, fullNameWithNs, ref outInfo);
                    return;
                }
            }

            if (!inInfo.MuteErrors)
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [], ErrorCode.Get(CTEN.TypeCouldNotBeInfered));

            void OnFoundSymbol(DeclSymbol typed, string name, ref OutInfo outInfo2)
            {
                if (!CheckIfCouldBeAccessed(idExpr, typed.Decl) && !inInfo.FromCallExpr && !inInfo.MuteErrors)
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [], ErrorCode.Get(CTEN.DeclCouldNotBeAccessed));
                typed = CheckForGenericType(typed, idExpr);
                if (!string.IsNullOrWhiteSpace(name))
                    idExpr.Name = name;
                idExpr.OutType = typed.Decl.Type.OutType;
                TryAssignConstValueToExpr(idExpr, typed.Decl, inInfo, ref outInfo2);
                TrySaveClassUsage(typed.Decl);
                idExpr.FindSymbol = typed;
            }
        }

        /// <summary>
        /// This shite helps us to move OutValue from one to another
        /// </summary>
        /// <param name="expr">The main expr</param>
        /// <param name="decl">The decl that could have OutValue</param>
        private void TryAssignConstValueToExpr(AstExpression expr, AstDeclaration decl, InInfo inInfo, ref OutInfo outInfo)
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
                    PostPrepareExprInference(varDecl.Initializer, inInfo, ref outInfo);
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

        private DeclSymbol CheckForGenericType(DeclSymbol decl, AstIdExpr idExpr)
        {
            if (idExpr is not AstIdGenericExpr genId)
                return decl;

            if (!decl.Decl.HasGenericTypes)
                return decl;

            var theDecl = decl.Decl;

            // this is to get REAL PURE GENERIC. not the fcking T-like
            if (theDecl.IsImplOfGeneric)
            {
                theDecl = theDecl.OriginalGenericDecl;
            }

            // generating generic shite name
            string realName = GenericsHelper.GetRealFromGenericName(theDecl, genId.GenericRealTypes.GetNestedList());
            if (theDecl.Scope.SymbolTable.TryGetValue(realName, out var realDcl) && realDcl is DeclSymbol realDclDecl)
            {
                // return if exists
                return realDclDecl;
            }

            // create a new shite with real types
            var realCls = GetRealTypeFromGeneric(theDecl, genId.GenericRealTypes.GetNestedList(), realName);

            // define the real decl in the same scope where generic one exists
            realDclDecl = new DeclSymbol(realName, realCls);
            theDecl.Scope.DefineSymbol(realDclDecl);
            return realDclDecl;
        }

        private void PostPrepareCallExprInference(AstCallExpr callExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // it wants to be infered again - reset its name and allow it
            if (callExpr.OutType != null && callExpr.FuncName.Name.Contains("::"))
            {
                var theName = callExpr.FuncName.Name;
                var resetedName = theName.GetPureFuncName();
                // also reset generic appendings
                if (resetedName.Contains(GenericsHelper.GENERIC_BEGIN))
                    resetedName = resetedName.Split(GenericsHelper.GENERIC_BEGIN)[0];
                callExpr.FuncName = callExpr.FuncName.GetCopy(resetedName);
            }

            string funcName = callExpr.FuncName.Name;
            if (callExpr.FuncName is AstIdGenericExpr genId)
            {
                for (int i = 0; i < genId.GenericRealTypes.Count; ++i)
                {
                    var g = genId.GenericRealTypes[i];
                    PostPrepareExprInference(g, inInfo, ref outInfo);
                }
                funcName = GenericsHelper.GetRealFromGenericName(callExpr.FuncName.Name, genId.GenericRealTypes.GetNestedList());
            }

            // the var is used to check when static method is accessed from an object
            bool accessingFromAnObject = false;

            // usually when in the same class
            if (callExpr.TypeOrObjectName != null)
            {
                // resolve the object on which func is called
                PostPrepareExprInference(callExpr.TypeOrObjectName, inInfo, ref outInfo);
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
                PostPrepareExprInference(a, inInfo, ref outInfo);
            }

            string newName = string.Empty;
            // renaming func call name from 'Anime' to 'Anime(int, float)' WITH OBJECT AS FIRST PARAM
            if (callExpr.TypeOrObjectName == null)
            {
                // if the type/object name is not presented - the function is in the same class
                // but we need to know is it static or not
                newName = $"{_currentClass.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString()}";
                var smbl2 = GetFuncFromCandidates(newName, callExpr.Arguments.Select(x => x.Expr).ToList(), callExpr.FuncName.Scope, _currentClass, out var casts);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
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
                    PostPrepareExprInference(callExpr.TypeOrObjectName, inInfo, ref outInfo);

                    // if it is a non static func defined in local class
                    newName = $"{_currentClass.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString(PointerType.GetPointerType(_currentClass.Type.OutType))}";
                    List<AstExpression> argsWithClassParam = new List<AstExpression>(callExpr.Arguments);
                    argsWithClassParam.Insert(0, callExpr.TypeOrObjectName);

                    smbl2 = GetFuncFromCandidates(newName, argsWithClassParam, callExpr.FuncName.Scope, _currentClass, out var casts2);
                    smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
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
                newName = $"{clsTp.Declaration.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString(callExpr.TypeOrObjectName.OutType)}";

                List<AstExpression> argsWithClassParam = new List<AstExpression>(callExpr.Arguments);
                argsWithClassParam.Insert(0, callExpr.TypeOrObjectName);
                var smbl2 = GetFuncFromCandidates(newName, argsWithClassParam, clsTp.Declaration.SubScope, clsTp.Declaration, out var casts);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);

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
                    newName = $"{clsTp.Declaration.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString()}";
                    smbl2 = GetFuncFromCandidates(newName, callExpr.Arguments.Select(x => x.Expr).ToList(), clsTp.Declaration.SubScope, clsTp.Declaration, out var casts2);
                    smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                    if (smbl2 is DeclSymbol ds2 && ds2.Decl is AstFuncDecl funcDecl2)
                    {
                        if (!CheckIfCouldBeAccessed(callExpr, funcDecl2))
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                        newName = funcDecl2.Name.Name;
                        callExpr.Arguments.ReplaceWithCasts(casts2);
                    }
                    else
                    {
                        // check for generic shite
                        newName = $"{callExpr.FuncName.Name}"; // USE REAL FUNC NAME HERE
                        smbl2 = GetFuncFromCandidates(newName, argsWithClassParam, clsTp.Declaration.SubScope, clsTp.Declaration, out var casts3);
                        smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                        if (smbl2 is DeclSymbol ds3 && ds3.Decl is AstFuncDecl funcDecl3)
                        {
                            if (!CheckIfCouldBeAccessed(callExpr, funcDecl3))
                                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                            newName = funcDecl3.Name.Name;
                            //callExpr.Arguments.ReplaceWithCasts(casts2);
                        }
                        else
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncWithNameNotFound));
                    }
                }
                accessingFromAnObject = true;
            }
            else if (callExpr.TypeOrObjectName.OutType is ClassType clsTpStatic)
            {
                // if we are calling like 'A.Anime()' where 'A' is a class
                // we need to rename the func name call like that:
                newName = $"{clsTpStatic.Declaration.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString()}";

                var smbl2 = GetFuncFromCandidates(newName, callExpr.Arguments.Select(x => x.Expr).ToList(), clsTpStatic.Declaration.SubScope, clsTpStatic.Declaration, out var casts);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
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
                    newName = $"{clsTpStatic.Declaration.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString(PointerType.GetPointerType(clsTpStatic))}";

                    List<AstExpression> argsWithClassParam = new List<AstExpression>(callExpr.Arguments);
                    var pseudoClassArg = new AstPointerExpr(callExpr.TypeOrObjectName, false, callExpr.TypeOrObjectName);
                    PostPrepareExprInference(pseudoClassArg, inInfo, ref outInfo);
                    argsWithClassParam.Insert(0, pseudoClassArg);
                    smbl2 = GetFuncFromCandidates(newName, argsWithClassParam, clsTpStatic.Declaration.SubScope, clsTpStatic.Declaration, out var _);
                    smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
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
                newName = $"{structType.Declaration.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString()}";

                var smbl2 = GetFuncFromCandidates(newName, callExpr.Arguments.Select(x => x.Expr).ToList(), structType.Declaration.SubScope, structType.Declaration, out var casts);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
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
                    newName = $"{structType.Declaration.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString(PointerType.GetPointerType(structType))}";

                    List<AstExpression> argsWithStructParam = new List<AstExpression>(callExpr.Arguments);
                    var pseudoStructArg = new AstPointerExpr(callExpr.TypeOrObjectName, false, callExpr.TypeOrObjectName);
                    PostPrepareExprInference(pseudoStructArg, inInfo, ref outInfo);
                    argsWithStructParam.Insert(0, pseudoStructArg);
                    smbl2 = GetFuncFromCandidates(newName, argsWithStructParam, structType.Declaration.SubScope, structType.Declaration, out casts);
                    smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);

                    var declSymbolOfLeft = callExpr.TypeOrObjectName.TryGetDeclSymbol();
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
            else if (callExpr.TypeOrObjectName.OutType is PointerType ptrTp2 && ptrTp2.TargetType is StructType strTp)
            {
                // if we are calling like 'A.Anime()' where 'A' is a struct
                // we need to rename the func name call like that:
                newName = $"{strTp.Declaration.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString()}";

                var smbl2 = GetFuncFromCandidates(newName, callExpr.Arguments.Select(x => x.Expr).ToList(), strTp.Declaration.SubScope, strTp.Declaration, out var casts);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);

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
                    newName = $"{strTp.Declaration.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString(PointerType.GetPointerType(strTp))}";

                    List<AstExpression> argsWithStructParam = new List<AstExpression>(callExpr.Arguments);
                    var pseudoStructArg = callExpr.TypeOrObjectName;
                    argsWithStructParam.Insert(0, pseudoStructArg);

                    smbl2 = GetFuncFromCandidates(newName, argsWithStructParam, strTp.Declaration.SubScope, strTp.Declaration, out casts);
                    smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);

                    var declSymbolOfLeft = callExpr.TypeOrObjectName.TryGetDeclSymbol();
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
            inInfo.FromCallExpr = true;
            PostPrepareIdentifierInference(callExpr.FuncName, inInfo, ref outInfo);
            inInfo.FromCallExpr = false;

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

            DeclSymbol OnFoundSymbol(DeclSymbol typed, AstIdExpr idExpr)
            {
                if (typed == null)
                    return typed;

                return CheckForGenericType(typed, idExpr);
            }
        }

        private void PostPrepareCastExprInference(AstCastExpr castExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(castExpr.SubExpression as AstExpression, inInfo, ref outInfo);
            PostPrepareExprInference(castExpr.TypeExpr as AstExpression, inInfo, ref outInfo);
            castExpr.OutType = (castExpr.TypeExpr as AstExpression).OutType;
            castExpr.OutValue = castExpr.OutValue; // WARN: is it ok just to pass the value?
        }

        private void PostPrepareNestedExprInference(AstNestedExpr nestExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // the var is used to check when static/const field is accessed from an object
            bool accessingFromAnObject = false;

            bool foundNs = false;
            // normalizing types with their namespaces
            InternalNormalizeLeftPartIfItIsANamespaceWithType(nestExpr, ref foundNs);

            if (nestExpr.LeftPart == null)
            {
                PostPrepareExprInference(nestExpr.RightPart, inInfo, ref outInfo);
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
                    PostPrepareExprInference(thisArg, inInfo, ref outInfo);
                    nestExpr.LeftPart = thisArg;

                    // if true - found set propa
                    if (CheckForProperty(dS.Decl, idExpr, inInfo, ref outInfo))
                        return;
                }

                // if getting indexer to set smth
                if (outInfo.ItWasIndexer && inInfo.PropertySet)
                {
                    // just skip - it should be handled by AssignInferencer
                    return;
                }
                else if (outInfo.ItWasIndexer)
                {
                    // reset
                    outInfo.ItWasIndexer = false;

                    // if getting indexer to get
                    var fncName = new AstIdExpr("get_indexer__", nestExpr);
                    fncName.Scope = nestExpr.Scope;
                    var fncArg = new AstArgumentExpr(outInfo.IndexedIndex, null, nestExpr);
                    var fncCall = new AstCallExpr(outInfo.IndexedObject, fncName, new List<AstArgumentExpr>() { fncArg }, nestExpr);
                    SetScopeAndParent(fncCall, nestExpr.RightPart.NormalParent, nestExpr.RightPart.Scope);
                    nestExpr.LeftPart = null;
                    nestExpr.RightPart = fncCall;
                    PostPrepareCallExprInference(fncCall, inInfo, ref outInfo);
                }
            }
            else
            {
                Scope leftSideScope = null;
                PostPrepareExprInference(nestExpr.LeftPart, inInfo, ref outInfo);
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
                    outInfo.ItWasProperty = false;
                    return;
                }

                // here could only be an AstIdExpr because AstCallExpr and AstExpression would be in 'if' block upper
                if (nestExpr.RightPart is not AstIdExpr idExpr)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, nestExpr.RightPart, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                    outInfo.ItWasProperty = false;
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

                    // if true - found set propa
                    if (CheckForProperty(typed.Decl, idExpr, inInfo, ref outInfo))
                        return;
                }
                else
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, idExpr, [HapetType.AsString(nestExpr.LeftPart.OutType)], ErrorCode.Get(CTEN.SymbolNotFoundInType));
                }
            }
            outInfo.ItWasProperty = false;

            bool CheckForProperty(AstDeclaration decl, AstIdExpr propaName, InInfo inInfoInside, ref OutInfo outInfoInside)
            {
                // if the ast is an access to a property
                if (decl is AstPropertyDecl)
                {
                    // if getting property to set smth
                    if (inInfoInside.PropertySet)
                    {
                        outInfoInside.ItWasProperty = true;
                        return true;
                    }
                    else
                    {
                        // if getting propa to get
                        var fncCall = new AstCallExpr(nestExpr.LeftPart, propaName.GetCopy($"get_{propaName.Name}"), null, nestExpr);
                        SetScopeAndParent(fncCall, nestExpr.RightPart.NormalParent, nestExpr.RightPart.Scope);
                        nestExpr.LeftPart = null;
                        nestExpr.RightPart = fncCall;
                        PostPrepareCallExprInference(fncCall, inInfoInside, ref outInfoInside);
                    }
                }
                return false;
            }
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

        private void PostPrepareArrayExprInference(AstArrayExpr arrayExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(arrayExpr.SubExpression, inInfo, ref outInfo);
            arrayExpr.OutType = ArrayType.GetArrayType(arrayExpr.SubExpression.OutType, arrayExpr.Scope);
        }

        private void PostPrepareArrayCreateExprInference(AstArrayCreateExpr arrayExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            foreach (var sz in arrayExpr.SizeExprs)
            {
                PostPrepareExprInference(sz, inInfo, ref outInfo);
            }
            // TODO: you can check if the size is available at compile time and create the array on stack

            PostPrepareExprInference(arrayExpr.TypeName, inInfo, ref outInfo);

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
                PostPrepareExprInference(e, inInfo, ref outInfo);
                // try to use implicit cast if it can be used
                arrayExpr.Elements[i] = PostPrepareExpressionWithType(GetPreparedAst(expectingElementType, arrayExpr), e);
            }

            // preparing the array
            PostPrepareFullArray(arrayExpr);
        }

        private void PostPrepareArrayAccessExprInference(AstArrayAccessExpr arrayAccExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // set propertySet to false because if we are in ArrayAccess - then ObjectName if it is property - has to be 'get_prop'
            var savedPropSet = inInfo.PropertySet;
            inInfo.PropertySet = false;
            PostPrepareExprInference(arrayAccExpr.ParameterExpr, inInfo, ref outInfo);
            PostPrepareExprInference(arrayAccExpr.ObjectName, inInfo, ref outInfo);
            inInfo.PropertySet = savedPropSet;

            // at first try to find indexer overload
            string typeName = null;
            HapetType firstParamType = null;
            AstExpression pseudoFirstArg = null;
            Scope subScope = null;
            AstDeclaration declItself = null;
            if (arrayAccExpr.ObjectName.OutType is PointerType ptrT && ptrT.TargetType is ClassType clsT)
            {
                typeName = clsT.Declaration.Name.Name;
                firstParamType = arrayAccExpr.ObjectName.OutType;
                pseudoFirstArg = arrayAccExpr.ObjectName;
                subScope = clsT.Declaration.SubScope;
                declItself = clsT.Declaration;
            }
            else if (arrayAccExpr.ObjectName.OutType is StructType strT)
            {
                typeName = strT.Declaration.Name.Name;
                firstParamType = PointerType.GetPointerType(arrayAccExpr.ObjectName.OutType);
                pseudoFirstArg = new AstPointerExpr(arrayAccExpr.ObjectName, false, arrayAccExpr.ObjectName);
                PostPrepareExprInference(pseudoFirstArg, inInfo, ref outInfo);
                subScope = strT.Declaration.SubScope;
                declItself = strT.Declaration;
            }
            if (typeName != null)
            {
                // getting the name but with object first param
                var newName = $"{typeName}::get_indexer__({HapetType.AsString(firstParamType)}:{HapetType.AsString(arrayAccExpr.ParameterExpr.OutType)})";

                List<AstExpression> argsWithStructParam = new List<AstExpression>() { arrayAccExpr.ParameterExpr };
                argsWithStructParam.Insert(0, pseudoFirstArg);
                var smbl = GetFuncFromCandidates(newName, argsWithStructParam, subScope, declItself, out var casts);

                if (smbl != null && smbl.Decl is AstFuncDecl funcDecl)
                {
                    arrayAccExpr.OutType = funcDecl.Returns.OutType;
                    outInfo.ItWasIndexer = true;
                    outInfo.IndexedIndex = arrayAccExpr.ParameterExpr;
                    outInfo.IndexedObject = arrayAccExpr.ObjectName as AstNestedExpr;
                    return; // everything is ok :)
                }
            }
            

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
        private void PostPrepareAssignStmtInference(AstAssignStmt assignStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            // propaSet is true only here
            inInfo.PropertySet = true;
            PostPrepareNestedExprInference(assignStmt.Target, inInfo, ref outInfo);
            inInfo.PropertySet = false;

            // cringe error when user tries to assign something directly into enum field
            if (assignStmt.Target.LeftPart != null && assignStmt.Target.LeftPart.OutType is EnumType)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, assignStmt, [], ErrorCode.Get(CTEN.EnumFieldAssigned));
                return;
            }
            // pp assign value
            if (assignStmt.Value != null)
            {
                // save previous
                var saved1 = outInfo.ItWasIndexer;
                var saved2 = outInfo.ItWasProperty;
                outInfo.ItWasIndexer = false;
                outInfo.ItWasProperty = false;
                assignStmt.Value = PostPrepareVarValueAssign(assignStmt.Value, assignStmt.Target.OutType, inInfo, ref outInfo);
                outInfo.ItWasIndexer = saved1;
                outInfo.ItWasProperty = saved2;
            }
            else
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, assignStmt, [], ErrorCode.Get(CTEN.NotExprInAssignment));
        }

        private void PostPrepareForStmtInference(AstForStmt forStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            if (forStmt.FirstArgument != null)
                PostPrepareExprInference(forStmt.FirstArgument, inInfo, ref outInfo);
            if (forStmt.SecondArgument != null)
            {
                PostPrepareExprInference(forStmt.SecondArgument, inInfo, ref outInfo);

                // error if it is not a bool type because it has to be
                if (forStmt.SecondArgument.OutType is not BoolType)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, forStmt.SecondArgument, [], ErrorCode.Get(CTEN.ExprIsNotBool));
                }
            }
            if (forStmt.ThirdArgument != null)
                PostPrepareExprInference(forStmt.ThirdArgument, inInfo, ref outInfo);

            PostPrepareExprInference(forStmt.Body, inInfo, ref outInfo);
        }

        private void PostPrepareWhileStmtInference(AstWhileStmt whileStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(whileStmt.Condition, inInfo, ref outInfo);

            // error if it is not a bool type because it has to be
            if (whileStmt.Condition.OutType is not BoolType)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, whileStmt.Condition, [], ErrorCode.Get(CTEN.ExprIsNotBool));
            }

            PostPrepareExprInference(whileStmt.Body, inInfo, ref outInfo);
        }

        private void PostPrepareIfStmtInference(AstIfStmt ifStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(ifStmt.Condition, inInfo, ref outInfo);

            // error if it is not a bool type because it has to be
            if (ifStmt.Condition.OutType is not BoolType)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, ifStmt.Condition, [], ErrorCode.Get(CTEN.ExprIsNotBool));
            }

            PostPrepareExprInference(ifStmt.BodyTrue, inInfo, ref outInfo);
            if (ifStmt.BodyFalse != null)
                PostPrepareExprInference(ifStmt.BodyFalse, inInfo, ref outInfo);
        }

        private void PostPrepareSwitchStmtInference(AstSwitchStmt switchStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            PostPrepareExprInference(switchStmt.SubExpression, inInfo, ref outInfo);

            // used to check that there are no more than 1 default case
            bool thereWasADefaultCase = false;

            foreach (var cc in switchStmt.Cases)
            {
                PostPrepareExprInference(cc, inInfo, ref outInfo);

                // calc default cases. if there are more than 1 - error
                if (cc.IsDefaultCase)
                {
                    if (thereWasADefaultCase)
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, cc.Pattern, [], ErrorCode.Get(CTEN.MultipleDefaultCases));
                    thereWasADefaultCase = true;
                    continue; // do not check for pattern in default expr...
                }

                // trying to implicitly cast cast value into switch sub expr
                cc.Pattern = PostPrepareExpressionWithType(GetPreparedAst(switchStmt.SubExpression.OutType, switchStmt.SubExpression), cc.Pattern);

                // check that the value is a const 
                if (cc.Pattern.OutValue == null)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, cc.Pattern, [], ErrorCode.Get(CTEN.NonConstantCaseValue));
                }
            }
        }

        private void PostPrepareCaseStmtInference(AstCaseStmt caseStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            if (!caseStmt.IsDefaultCase)
                PostPrepareExprInference(caseStmt.Pattern, inInfo, ref outInfo);

            if (!caseStmt.IsFallingCase)
                PostPrepareExprInference(caseStmt.Body, inInfo, ref outInfo);
        }

        private void PostPrepareReturnStmtInference(AstReturnStmt returnStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            if (returnStmt.ReturnExpression != null)
            {
                // if user tries to return smth but func ret type is void =^0
                if (_currentFunction.Returns.OutType is VoidType)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, _currentFunction.Name, [], ErrorCode.Get(CTEN.EmptyReturnExpected));
                    return;
                }

                // casting to func return type
                returnStmt.ReturnExpression = PostPrepareVarValueAssign(returnStmt.ReturnExpression, _currentFunction.Returns.OutType, inInfo, ref outInfo);
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

        private void PostPrepareAttributeStmtInference(AstAttributeStmt attrStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            // purified type string with namespace in it!
            // we need this so when saving the attributes into metafile 
            // we would know namespace of the attribute and so on.
            // (kostyl?)
            var newTypeAst = attrStmt.AttributeName.GetTypeAstId(_compiler.MessageHandler, _currentSourceFile);
            PostPrepareExprInference(newTypeAst, inInfo, ref outInfo);
            attrStmt.AttributeName.SetTypeAstId(newTypeAst);

            // check that the attr ast was infered properly
            if (attrStmt.AttributeName.OutType == null)
                return;

            // TODO: check that the shite is inherited from 'System.Attribute'
            // getting all the fields of attribuute class decl
            var attrDeclFields = (attrStmt.AttributeName.OutType as ClassType).Declaration.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl).ToList();
            // check that not too much params
            if (attrStmt.Arguments.Count > attrDeclFields.Count)
            {
                var beg = attrStmt.Arguments[attrDeclFields.Count].Beginning;
                var end = attrStmt.Arguments[attrStmt.Arguments.Count - 1].Ending;
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, new Location(beg, end), [attrDeclFields.Count.ToString(), attrStmt.Arguments.Count.ToString()], ErrorCode.Get(CTEN.WrongAttrArgs));
            }

            for (int i = 0; i < attrDeclFields.Count; ++i)
            {
                var theAttrField = attrDeclFields[i];

                // check that param exists for the field 
                if (i < attrStmt.Arguments.Count)
                {
                    // inferrencing the param
                    var arg = attrStmt.Arguments[i];
                    PostPrepareExprInference(arg, inInfo, ref outInfo);

                    var a = arg.Expr;
                    // all attr params has to be const values
                    if (a.OutValue == null)
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, a, [], ErrorCode.Get(CTEN.NonComptimeAttrArg));

                    // is going to error if they are different types :)
                    attrStmt.Arguments[i].Expr = PostPrepareExpressionWithType(GetPreparedAst(theAttrField.Type.OutType, theAttrField.Type), a);
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
                            PostPrepareAttributeStmtInference(aa, inInfo, ref outInfo);
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

        private void PostPrepareBaseCtorStmtInference(AstBaseCtorStmt baseStmt, InInfo inInfo, ref OutInfo outInfo)
        {
            // resolve args
            foreach (var a in baseStmt.Arguments)
            {
                PostPrepareExprInference(a, inInfo, ref outInfo);
            }
        }

        private AstExpression PostPrepareVarValueAssign(AstExpression value, HapetType targetType, InInfo inInfo, ref OutInfo outInfo)
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
                PostPrepareExprInference(value, inInfo, ref outInfo);
            }
            return PostPrepareExpressionWithType(GetPreparedAst(targetType, value), value);
        }

        private bool CheckIfCouldBeAccessed(AstStatement accessor, AstDeclaration accessee)
        {
            // could be accessed from everyone
            if (accessee.SpecialKeys.Contains(TokenType.KwPublic))
                return true;

            // built in also could be accessed from everywhere
            if (accessee is AstBuiltInTypeDecl)
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
