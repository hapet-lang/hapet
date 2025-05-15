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

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareCallExprInference(AstCallExpr callExpr, InInfo inInfo, ref OutInfo outInfo)
        {
            // it wants to be infered again - reset its name and allow it
            if (callExpr.OutType != null && callExpr.FuncName.Name.Contains("::"))
            {
                var theName = callExpr.FuncName.Name;
                var resetedName = theName.GetPureFuncName();
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

            AstIdExpr newName = null;
            SearchForFunctionByCall(callExpr, funcName, inInfo, ref outInfo, ref accessingFromAnObject, ref newName);

            callExpr.FuncName = newName.GetCopy();
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
                callExpr.OutType = dt.TargetDeclaration.Returns.OutType;

                callExpr.StaticCall = true; // doesn;t mean anything when calling delegates
            }
            else
            {
                // error here
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr, [], ErrorCode.Get(CTEN.CallNotFuncOrDelegate));
            }
        }

        private void SearchForFunctionByCall(AstCallExpr callExpr, AstIdExpr funcName, InInfo inInfo, ref OutInfo outInfo, ref bool accessingFromAnObject, ref AstIdExpr newName)
        {
            // renaming func call name from 'Anime' to 'Anime(int, float)' WITH OBJECT AS FIRST PARAM
            if (callExpr.TypeOrObjectName == null)
            {
                // if the type/object name is not presented - the function could be in local variable as delegate
                var smbl1 = callExpr.Scope.GetSymbol(funcName);
                if (smbl1 is DeclSymbol ds4 && ds4.Decl is AstDeclaration varDecl && varDecl.Type.OutType is DelegateType)
                {
                    return;
                }

                // getting parent of the func
                var currentParent = _currentParentStack.GetNearestParentClassOrStruct();

                // if the type/object name is not presented - the function is in the same class
                // but we need to know is it static or not
                newName = funcName.GetCopy($"{currentParent.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString()}");
                var smbl2 = GetFuncFromCandidates(newName, callExpr, callExpr.Arguments, currentParent, false, out var casts);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                if (smbl2 is DeclSymbol ds && ds.Decl is AstFuncDecl funcDecl)
                {
                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl))
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    // static func defined in local class
                    newName = funcDecl.Name.GetCopy();
                    callExpr.Arguments.ReplaceWithCasts(casts);
                    return;
                }

                accessingFromAnObject = true;
                // we need to create this one because code generator requires the parameter of this shite
                callExpr.TypeOrObjectName = new AstNestedExpr(new AstIdExpr("this", callExpr), null, callExpr);
                SetScopeAndParent(callExpr.TypeOrObjectName, callExpr);
                PostPrepareExprScoping(callExpr.TypeOrObjectName);
                PostPrepareExprInference(callExpr.TypeOrObjectName, inInfo, ref outInfo);

                // if it is a non static func defined in local class
                newName = funcName.GetCopy($"{currentParent.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString(PointerType.GetPointerType(currentParent.Type.OutType))}");
                List<AstArgumentExpr> argsWithClassParam = new List<AstArgumentExpr>(callExpr.Arguments);
                argsWithClassParam.Insert(0, new AstArgumentExpr(callExpr.TypeOrObjectName) { OutType = callExpr.TypeOrObjectName.OutType });

                smbl2 = GetFuncFromCandidates(newName, callExpr, argsWithClassParam, currentParent, true, out var casts2);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                if (smbl2 is DeclSymbol ds2 && ds2.Decl is AstFuncDecl funcDecl2)
                {
                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl2))
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    newName = funcDecl2.Name.GetCopy();
                    callExpr.Arguments.ReplaceWithCasts(casts2.Skip(1).ToList()); // skip because the first param is an object
                    return;
                }
                accessingFromAnObject = false;
                if (_compiler.MessageHandler.HasErrors)
                    return;

                // check for nested shite
                newName = funcName.GetCopy($"{callExpr.FuncName.Name}{callExpr.Arguments.GetArgsString()}"); // USE REAL FUNC NAME HERE
                smbl2 = GetFuncFromCandidates(newName, callExpr, callExpr.Arguments, currentParent, false, out var casts3);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                if (smbl2 is DeclSymbol ds3 && ds3.Decl is AstFuncDecl funcDecl3)
                {
                    newName = funcDecl3.Name.GetCopy();
                    return;
                }

                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncWithNameNotFound));
            }
            else if (callExpr.TypeOrObjectName.OutType is PointerType ptrTp && ptrTp.TargetType is ClassType clsTp)
            {
                accessingFromAnObject = true;

                // if we are calling like 'a.Anime()' where 'a' is an object
                // we need to rename the func name call like that:
                newName = funcName.GetCopy($"{clsTp.Declaration.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString(callExpr.TypeOrObjectName.OutType)}");
                List<AstArgumentExpr> argsWithClassParam = new List<AstArgumentExpr>(callExpr.Arguments);
                argsWithClassParam.Insert(0, new AstArgumentExpr(callExpr.TypeOrObjectName) { OutType = callExpr.TypeOrObjectName.OutType });
                var smbl2 = GetFuncFromCandidates(newName, callExpr, argsWithClassParam, clsTp.Declaration, true, out var casts);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);

                // check if the decl exists. if not - it could be static method call from an object
                if (smbl2 is DeclSymbol ds && ds.Decl is AstFuncDecl funcDecl)
                {
                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl))
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    newName = funcDecl.Name.GetCopy();
                    callExpr.Arguments.ReplaceWithCasts(casts.Skip(1).ToList()); // skip because the first param is an object
                    return;
                }
                if (_compiler.MessageHandler.HasErrors)
                    return;

                // getting the name but without object first param
                newName = funcName.GetCopy($"{clsTp.Declaration.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString()}");
                smbl2 = GetFuncFromCandidates(newName, callExpr, callExpr.Arguments, clsTp.Declaration, false, out var casts2);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                if (smbl2 is DeclSymbol ds2 && ds2.Decl is AstFuncDecl funcDecl2)
                {
                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl2))
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    newName = funcDecl2.Name.GetCopy();
                    callExpr.Arguments.ReplaceWithCasts(casts2);
                    return;
                }
                if (_compiler.MessageHandler.HasErrors)
                    return;

                // check for generic shite
                newName = funcName.GetCopy($"{clsTp.Declaration.Name.Name}::{callExpr.FuncName.Name}"); // USE REAL FUNC NAME HERE
                smbl2 = GetFuncFromCandidates(newName, callExpr, argsWithClassParam, clsTp.Declaration, false, out var casts3);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                if (smbl2 is DeclSymbol ds3 && ds3.Decl is AstFuncDecl funcDecl3)
                {
                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl3))
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    newName = funcDecl3.Name.GetCopy();
                    return;
                }

                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncWithNameNotFound));
            }
            else if (callExpr.TypeOrObjectName.OutType is ClassType clsTpStatic)
            {
                // if we are calling like 'A.Anime()' where 'A' is a class
                // we need to rename the func name call like that:
                newName = funcName.GetCopy($"{clsTpStatic.Declaration.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString()}");

                var smbl2 = GetFuncFromCandidates(newName, callExpr, callExpr.Arguments, clsTpStatic.Declaration, false, out var casts);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                // check if the decl exists. if not - it could be non static method call from a class name
                if (smbl2 is DeclSymbol ds && ds.Decl is AstFuncDecl funcDecl)
                {
                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl))
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    newName = funcDecl.Name.GetCopy();
                    callExpr.Arguments.ReplaceWithCasts(casts);
                    return;
                }
                if (_compiler.MessageHandler.HasErrors)
                    return;

                // getting the name but with object first param
                newName = funcName.GetCopy($"{clsTpStatic.Declaration.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString(PointerType.GetPointerType(clsTpStatic))}");

                List<AstArgumentExpr> argsWithClassParam = new List<AstArgumentExpr>(callExpr.Arguments);
                var pseudoClassArg = new AstPointerExpr(callExpr.TypeOrObjectName, false, callExpr.TypeOrObjectName)
                {
                    Scope = funcName.Scope,
                };
                PostPrepareExprInference(pseudoClassArg, inInfo, ref outInfo);
                argsWithClassParam.Insert(0, new AstArgumentExpr(pseudoClassArg) { OutType = callExpr.TypeOrObjectName.OutType });
                smbl2 = GetFuncFromCandidates(newName, callExpr, argsWithClassParam, clsTpStatic.Declaration, true, out var _);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                // error because user tries to access non static method from a class name
                if (smbl2 != null)
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.NonStaticFuncFromStatic));
                else
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncWithNameNotFound));
                return;
            }
            else if (callExpr.TypeOrObjectName.OutType is StructType structType)
            {
                // if we are calling like 'A.Anime()' where 'A' is a struct
                // we need to rename the func name call like that:
                newName = funcName.GetCopy($"{structType.Declaration.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString()}");

                var smbl2 = GetFuncFromCandidates(newName, callExpr, callExpr.Arguments, structType.Declaration, false, out var casts);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);
                // check if the decl exists. if not - it could be non static method call from a class name
                if (smbl2 is DeclSymbol ds && ds.Decl is AstFuncDecl funcDecl)
                {
                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl))
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    newName = funcDecl.Name.GetCopy();
                    callExpr.Arguments.ReplaceWithCasts(casts);
                    return;
                }

                // getting the name but with object first param
                newName = funcName.GetCopy($"{structType.Declaration.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString(PointerType.GetPointerType(structType))}");

                List<AstArgumentExpr> argsWithStructParam = new List<AstArgumentExpr>(callExpr.Arguments);
                var pseudoStructArg = new AstPointerExpr(callExpr.TypeOrObjectName, false, callExpr.TypeOrObjectName)
                {
                    Scope = funcName.Scope,
                };
                PostPrepareExprInference(pseudoStructArg, inInfo, ref outInfo);
                argsWithStructParam.Insert(0, new AstArgumentExpr(pseudoStructArg) { OutType = callExpr.TypeOrObjectName.OutType });
                smbl2 = GetFuncFromCandidates(newName, callExpr, argsWithStructParam, structType.Declaration, true, out casts);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);

                var declSymbolOfLeft = callExpr.TypeOrObjectName.TryGetDeclSymbol();
                if (smbl2 is DeclSymbol ds2 && ds2.Decl is AstFuncDecl funcDecl2)
                {
                    // error because user tries to access non static method from a class name
                    if (declSymbolOfLeft.Decl is AstStructDecl)
                    {
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.NonStaticFuncFromStatic));
                        return;
                    }

                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl2) && !funcDecl2.IsPropertyFunction)
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    newName = funcDecl2.Name.GetCopy();
                    callExpr.Arguments.ReplaceWithCasts(casts.Skip(1).ToList());
                    return;
                }

                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncWithNameNotFound));
            }
            else if (callExpr.TypeOrObjectName.OutType is PointerType ptrTp2 && ptrTp2.TargetType is StructType strTp)
            {
                // if we are calling like 'A.Anime()' where 'A' is a struct
                // we need to rename the func name call like that:
                newName = funcName.GetCopy($"{strTp.Declaration.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString()}");

                var smbl2 = GetFuncFromCandidates(newName, callExpr, callExpr.Arguments, strTp.Declaration, false, out var casts);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);

                // check if the decl exists. if not - it could be non static method call from a class name
                if (smbl2 is DeclSymbol ds && ds.Decl is AstFuncDecl funcDecl)
                {
                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl))
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    newName = funcDecl.Name.GetCopy();
                    callExpr.Arguments.ReplaceWithCasts(casts);
                    return;
                }

                // getting the name but with object first param
                newName = funcName.GetCopy($"{strTp.Declaration.Name.Name}::{funcName}{callExpr.Arguments.GetArgsString(PointerType.GetPointerType(strTp))}");

                List<AstArgumentExpr> argsWithStructParam = new List<AstArgumentExpr>(callExpr.Arguments);
                var pseudoStructArg = callExpr.TypeOrObjectName;
                argsWithStructParam.Insert(0, new AstArgumentExpr(pseudoStructArg) { OutType = callExpr.TypeOrObjectName.OutType });

                smbl2 = GetFuncFromCandidates(newName, callExpr, argsWithStructParam, strTp.Declaration, true, out casts);
                smbl2 = OnFoundSymbol(smbl2, callExpr.FuncName);

                var declSymbolOfLeft = callExpr.TypeOrObjectName.TryGetDeclSymbol();
                if (smbl2 is DeclSymbol ds2 && ds2.Decl is AstFuncDecl funcDecl2)
                {
                    // error because user tries to access non static method from a class name
                    if (declSymbolOfLeft.Decl is AstStructDecl)
                    {
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.NonStaticFuncFromStatic));
                        return;
                    }

                    if (!CheckIfCouldBeAccessed(callExpr, funcDecl2) && !funcDecl2.IsPropertyFunction)
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncCouldNotBeAccessed));
                    newName = funcDecl2.Name.GetCopy();
                    callExpr.Arguments.ReplaceWithCasts(casts.Skip(1).ToList());
                    return;
                }

                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr.FuncName, [], ErrorCode.Get(CTEN.FuncWithNameNotFound));
            }
            else
            {
                // error here: the function call could not be infered
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, callExpr, [], ErrorCode.Get(CTEN.FuncNotInfered));
            }

            DeclSymbol OnFoundSymbol(DeclSymbol typed, AstIdExpr idExpr)
            {
                if (typed == null)
                    return typed;

                return CheckForGenericType(typed, idExpr);
            }
        }
    }
}
