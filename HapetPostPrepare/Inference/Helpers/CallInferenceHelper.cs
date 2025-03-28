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
    }
}
