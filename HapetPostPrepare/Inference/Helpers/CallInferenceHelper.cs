using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;
using HapetFrontend.Extensions;
using System.Runtime;
using System;
using System.Security.Cryptography;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public void PostPrepareCallExprInference(AstCallExpr callExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // it wants to be infered again - reset its name and allow it
            if (callExpr.OutType != null && callExpr.FuncName.Name.Contains("::"))
            {
                var theName = callExpr.FuncName.Name;
                var resetedName = theName;
                callExpr.FuncName = callExpr.FuncName.GetCopy(resetedName);
            }

            // inferencing generic shite
            var funcName = callExpr.FuncName;
            if (callExpr.FuncName is AstIdGenericExpr genId)
            {
                for (int i = 0; i < genId.GenericRealTypes.Count; ++i)
                {
                    var g = genId.GenericRealTypes[i];
                    PostPrepareExprInference(g, inInfo, ref outInfo);
                }
            }

            // the var is used to check when static method is accessed from an object
            bool accessingFromAnObject = false;

            // usually null when in the same class
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

            SearchForFunctionByCall(callExpr, funcName, inInfo, ref outInfo, ref accessingFromAnObject, 
                out var declToSearch, out var foundFuncSymbol);

            // func not found...
            if (foundFuncSymbol == null)
                return;

            inInfo.FromCallExpr = true;
            IdentifierOnFoundSymbol(callExpr.FuncName, foundFuncSymbol, foundFuncSymbol.Name.Name, inInfo, ref outInfo);
            // PostPrepareIdentifierInference(callExpr.FuncName, inInfo, ref outInfo, declToSearch);
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
                callExpr.OutType = dt.TargetDeclaration.Returns.OutType;

                callExpr.StaticCall = true; // doesn;t mean anything when calling delegates
            }
            /// if it is null - it would be errored in <see cref="SearchForFunctionByCall"/>
            else if (callExpr.FuncName.OutType != null)
            {
                // error here
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr, [], ErrorCode.Get(CTEN.CallNotFuncOrDelegate));
            }
        }

        private void SearchForFunctionByCall(AstCallExpr callExpr, AstIdExpr funcName, InInfo inInfo, ref OutInfo outInfo, 
            ref bool accessingFromAnObject, out AstDeclaration declToSearch, out DeclSymbol foundSymbol)
        {
            // renaming func call name from 'Anime' to 'Anime(int, float)' WITH OBJECT AS FIRST PARAM
            if (callExpr.TypeOrObjectName == null)
            {
                // if the type/object name is not presented - the function could be in local variable as delegate
                var smbl1 = callExpr.Scope.GetSymbol(funcName);
                if (smbl1 is DeclSymbol ds4 && ds4.Decl is AstDeclaration varDecl && 
                    varDecl.Type.OutType is DelegateType)
                {
                    declToSearch = null;
                    foundSymbol = ds4;
                    return;
                }

                // getting parent of the func
                var currentParent = _currentParentStack.GetNearestParentClassOrStruct();

                // if the type/object name is not presented - the function is in the same class
                // but we need to know is it static or not
                var smbl2 = GetFuncFromCandidates(funcName, callExpr.Arguments, currentParent, false, out var casts);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                if (smbl2 is DeclSymbol ds && ds.Decl is AstFuncDecl funcDecl)
                {
                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl))
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    callExpr.Arguments.ReplaceWithCasts(casts);
                    declToSearch = currentParent;
                    foundSymbol = ds;
                    return;
                }

                accessingFromAnObject = true;
                // we need to create this one because code generator requires the parameter of this shite
                callExpr.TypeOrObjectName = new AstNestedExpr(new AstIdExpr("this", callExpr), null, callExpr);
                SetScopeAndParent(callExpr.TypeOrObjectName, callExpr);
                PostPrepareExprScoping(callExpr.TypeOrObjectName);
                PostPrepareExprInference(callExpr.TypeOrObjectName, inInfo, ref outInfo);

                // if it is a non static func defined in local class
                List<AstArgumentExpr> argsWithClassParam = new List<AstArgumentExpr>(callExpr.Arguments);
                var firstParArg = new AstArgumentExpr(callExpr.TypeOrObjectName) { OutType = callExpr.TypeOrObjectName.OutType };
                argsWithClassParam.Insert(0, firstParArg);

                // we need to set it to ref if it is a StructType
                if (currentParent is AstStructDecl)
                    firstParArg.ArgumentModificator = HapetFrontend.Enums.ParameterModificator.Ref;

                smbl2 = GetFuncFromCandidates(funcName, argsWithClassParam, currentParent, true, out var casts2);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                if (smbl2 is DeclSymbol ds2 && ds2.Decl is AstFuncDecl funcDecl2)
                {
                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl2))
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    callExpr.Arguments.ReplaceWithCasts(casts2.Skip(1).ToList()); // skip because the first param is an object
                    declToSearch = currentParent;
                    foundSymbol = ds2;
                    return;
                }
                accessingFromAnObject = false;
                if (_compiler.MessageHandler.HasErrors)
                {
                    declToSearch = null;
                    foundSymbol = null;
                    return;
                }

                // check for nested shite
                smbl2 = GetFuncFromCandidates(funcName, callExpr.Arguments, currentParent, false, out var casts3);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                if (smbl2 is DeclSymbol ds3 && ds3.Decl is AstFuncDecl funcDecl3)
                {
                    declToSearch = currentParent;
                    foundSymbol = ds3;
                    return;
                }

                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncWithNameNotFound));
            }
            else if (callExpr.TypeOrObjectName.OutType is ClassType clsTp)
            {
                AstDeclaration declToSearchLocal = clsTp is DelegateType delT ? delT.TargetDeclaration : clsTp.Declaration;

                // this check is done to handle static-call
                if (callExpr.TypeOrObjectName.TryGetDeclSymbol(true) is DeclSymbol dds2 && dds2.Decl is AstClassDecl)
                {
                    // if we are calling like 'A.Anime()' where 'A' is a class

                    var smbl2 = GetFuncFromCandidates(funcName, callExpr.Arguments, declToSearchLocal, false, out var casts);
                    smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                    // check if the decl exists. if not - it could be non static method call from a class name
                    if (smbl2 is DeclSymbol ds && ds.Decl is AstFuncDecl funcDecl)
                    {
                        if (!CheckIfCouldBeAccessed(callExpr, funcDecl))
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                        callExpr.Arguments.ReplaceWithCasts(casts);
                        declToSearch = declToSearchLocal;
                        foundSymbol = ds;
                        return;
                    }

                    List<AstArgumentExpr> argsWithClassParam = new List<AstArgumentExpr>(callExpr.Arguments);
                    argsWithClassParam.Insert(0, new AstArgumentExpr(callExpr.TypeOrObjectName) { OutType = callExpr.TypeOrObjectName.OutType });
                    smbl2 = GetFuncFromCandidates(funcName, argsWithClassParam, declToSearchLocal, true, out var _);
                    smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                    // error because user tries to access non static method from a class name
                    if (smbl2 != null)
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.NonStaticFuncFromStatic));
                    else
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncWithNameNotFound));

                    declToSearch = null;
                    foundSymbol = null;
                    return;
                }
                else
                {
                    accessingFromAnObject = true;

                    // if we are calling like 'a.Anime()' where 'a' is an object

                    List<AstArgumentExpr> argsWithClassParam = new List<AstArgumentExpr>(callExpr.Arguments);
                    argsWithClassParam.Insert(0, new AstArgumentExpr(callExpr.TypeOrObjectName) { OutType = callExpr.TypeOrObjectName.OutType });
                    var smbl2 = GetFuncFromCandidates(funcName, argsWithClassParam, declToSearchLocal, true, out var casts);
                    smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);

                    // check if the decl exists. if not - it could be static method call from an object
                    if (smbl2 is DeclSymbol ds && ds.Decl is AstFuncDecl funcDecl)
                    {
                        if (!CheckIfCouldBeAccessed(callExpr, funcDecl))
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                        callExpr.Arguments.ReplaceWithCasts(casts.Skip(1).ToList()); // skip because the first param is an object
                        declToSearch = declToSearchLocal;
                        foundSymbol = ds;
                        return;
                    }
                    if (_compiler.MessageHandler.HasErrors)
                    {
                        declToSearch = null;
                        foundSymbol = null;
                        return;
                    }

                    smbl2 = GetFuncFromCandidates(funcName, callExpr.Arguments, declToSearchLocal, false, out var casts2);
                    smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                    if (smbl2 is DeclSymbol ds2 && ds2.Decl is AstFuncDecl funcDecl2)
                    {
                        if (!CheckIfCouldBeAccessed(callExpr, funcDecl2))
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                        callExpr.Arguments.ReplaceWithCasts(casts2);
                        declToSearch = declToSearchLocal;
                        foundSymbol = ds2;
                        return;
                    }
                    if (_compiler.MessageHandler.HasErrors)
                    {
                        declToSearch = null;
                        foundSymbol = null;
                        return;
                    }

                    // check for generic shite
                    smbl2 = GetFuncFromCandidates(funcName, argsWithClassParam, declToSearchLocal, true, out var casts3);
                    smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                    if (smbl2 is DeclSymbol ds3 && ds3.Decl is AstFuncDecl funcDecl3)
                    {
                        if (!CheckIfCouldBeAccessed(callExpr, funcDecl3))
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                        declToSearch = declToSearchLocal;
                        foundSymbol = ds3;
                        return;
                    }

                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncWithNameNotFound));
                }
                    
            }
            else if (callExpr.TypeOrObjectName.OutType is StructType structType)
            {
                // if we are calling like 'A.Anime()' where 'A' is a struct

                var smbl2 = GetFuncFromCandidates(funcName, callExpr.Arguments, structType.Declaration, false, out var casts);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                // check if the decl exists. if not - it could be non static method call
                if (smbl2 is DeclSymbol ds && ds.Decl is AstFuncDecl funcDecl)
                {
                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl))
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    callExpr.Arguments.ReplaceWithCasts(casts);
                    declToSearch = structType.Declaration;
                    foundSymbol = ds;
                    return;
                }

                List<AstArgumentExpr> argsWithStructParam = new List<AstArgumentExpr>(callExpr.Arguments);
                argsWithStructParam.Insert(0, new AstArgumentExpr(callExpr.TypeOrObjectName) 
                { 
                    OutType = callExpr.TypeOrObjectName.OutType,
                    ArgumentModificator = HapetFrontend.Enums.ParameterModificator.Ref,
                });
                smbl2 = GetFuncFromCandidates(funcName, argsWithStructParam, structType.Declaration, true, out casts);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);

                var declSymbolOfLeft = callExpr.TypeOrObjectName.TryGetDeclSymbol();
                if (smbl2 is DeclSymbol ds2 && ds2.Decl is AstFuncDecl funcDecl2)
                {
                    // error because user tries to access non static method from a struct name
                    // it should be AstVarDecl at least
                    if (declSymbolOfLeft.Decl is AstStructDecl)
                    {
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.NonStaticFuncFromStatic));
                        declToSearch = null;
                        foundSymbol = null;
                        return;
                    }

                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl2) && !funcDecl2.IsPropertyFunction)
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    callExpr.Arguments.ReplaceWithCasts(casts.Skip(1).ToList());
                    declToSearch = structType.Declaration;
                    foundSymbol = ds2;
                    return;
                }

                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncWithNameNotFound));
            }
            else if (callExpr.TypeOrObjectName.OutType is GenericType genericType)
            {
                // if we are calling like 'T.Anime()' where 'T' is a generic type

                var smbl2 = GetFuncFromCandidates(funcName, callExpr.Arguments, genericType.Declaration, false, out var casts);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                // check if the decl exists. if not - it could be non static method call
                if (smbl2 is DeclSymbol ds && ds.Decl is AstFuncDecl funcDecl)
                {
                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl))
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    callExpr.Arguments.ReplaceWithCasts(casts);
                    declToSearch = genericType.Declaration;
                    foundSymbol = ds;
                    return;
                }

                List<AstArgumentExpr> argsWithStructParam = new List<AstArgumentExpr>(callExpr.Arguments);
                var pseudoStructArg = new AstPointerExpr(callExpr.TypeOrObjectName, false, callExpr.TypeOrObjectName)
                {
                    Scope = funcName.Scope,
                };
                PostPrepareExprInference(pseudoStructArg, inInfo, ref outInfo);
                argsWithStructParam.Insert(0, new AstArgumentExpr(pseudoStructArg) { OutType = callExpr.TypeOrObjectName.OutType });
                smbl2 = GetFuncFromCandidates(funcName, argsWithStructParam, genericType.Declaration, true, out casts);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);

                var declSymbolOfLeft = callExpr.TypeOrObjectName.TryGetDeclSymbol();
                if (smbl2 is DeclSymbol ds2 && ds2.Decl is AstFuncDecl funcDecl2)
                {
                    // error because user tries to access non static method from a gen name
                    // it should be AstVarDecl at least
                    if (declSymbolOfLeft.Decl is AstGenericDecl)
                    {
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.NonStaticFuncFromStatic));
                        declToSearch = null;
                        foundSymbol = null;
                        return;
                    }

                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl2) && !funcDecl2.IsPropertyFunction)
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    callExpr.Arguments.ReplaceWithCasts(casts.Skip(1).ToList());
                    declToSearch = genericType.Declaration;
                    foundSymbol = ds2;
                    return;
                }

                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncWithNameNotFound));
            }
            else
            {
                // error here: the function call could not be infered
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr, [], ErrorCode.Get(CTEN.FuncNotInfered));
            }

            declToSearch = null;
            foundSymbol = null;

            DeclSymbol OnFoundSymbol(DeclSymbol typed, AstIdExpr idExpr)
            {
                if (typed == null)
                    return typed;

                return CheckForGenericType(typed, idExpr);
            }
        }
    }
}
