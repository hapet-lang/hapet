using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Errors;
using HapetFrontend.Types;
using HapetFrontend.Enums;
using HapetFrontend.Parsing;
using System.Collections.Generic;
using HapetFrontend.Extensions;
using HapetFrontend.Entities;
using HapetFrontend.Helpers;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        /// names of some funcs/vars has to be the same as in <see cref="RenameFromGenericToRealType"/>
        private void PostPrepareClassMethods()
        {
            foreach (var (_, file) in _compiler.GetFiles())
            {
                PostPrepareClassMethodsInFile(file);
            }
        }

        public void PostPrepareClassMethodsInFile(ProgramFile file)
        {
            _currentSourceFile = file;
            foreach (var stmt in file.Statements)
            {
                PostPrepareDeclMethodsInternal(stmt as AstDeclaration, file);
            }
        }

        private void PostPrepareDeclMethodsInternal(AstDeclaration decl, ProgramFile file)
        {
            if (decl is not AstClassDecl && decl is not AstStructDecl)
                return;

            PostPrepareStructMethodsInternal__(decl, file.IsImported);
            foreach (var d in decl.GetDeclarations())
                PostPrepareDeclMethodsInternal(d, file); // probably nested decls
        }

        private void PostPrepareStructMethodsInternal__(AstDeclaration decl, bool forImported = false)
        {
            // check that all decls in the class are also static
            // except nested classes and structs
            if (decl is AstClassDecl && decl.SpecialKeys.Contains(TokenType.KwStatic))
            {
                var foundNonStatic = decl.GetDeclarations().FirstOrDefault(dd => 
                    !dd.SpecialKeys.Contains(TokenType.KwStatic) && 
                    !dd.SpecialKeys.Contains(TokenType.KwConst) && 
                    dd is not AstClassDecl && dd is not AstStructDecl);
                if (foundNonStatic != null)
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, foundNonStatic.Name, [], ErrorCode.Get(CTEN.ClassStaticMemStatic));
            }

            // getting all functions in the class
            var allFuncs = decl.GetDeclarations().Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl);

            // error if user created a func with the get/set name
            var propFuncs = allFuncs.Where(x => x.Name.Name.StartsWith($"get_") || x.Name.Name.StartsWith($"set_"));
            foreach (var fnc in propFuncs)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, fnc.Name, [], ErrorCode.Get(CTEN.ClassFuncGetSetName));
            }

            // getting all props in the class
            var allProps = decl.GetDeclarations().Where(x => x is AstPropertyDecl).Select(x => x as AstPropertyDecl);
            var allFields = decl.GetDeclarations().Where(x => x is AstVarDecl varD && x is not AstPropertyDecl).Select(x => x as AstVarDecl);
            foreach (var pp in allProps)
            {
                // check if there is already a field named like 'field_Prop'
                // error in this situation because we probably going to generate the field
                // also check if the prop is really going to gen field
                var theField = allFields.FirstOrDefault(x => x.Name.Name == $"field_{pp.Name.Name}");
                if (theField != null)
                {
                    // also check if the prop is really going to gen field
                    if (pp.GetBlock == null && pp.SetBlock == null)
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, theField, [pp.Name.Name], ErrorCode.Get(CTEN.ClassPropFieldExists));
                }
            }
            PrepareEventFields(allFields);

            // getting all fields and props and error if there are equal names
            var allFieldsAndProps = decl.GetDeclarations().Where(x => x is AstVarDecl).Select(x => x as AstVarDecl).ToList();
            for (int i = 0; i < allFieldsAndProps.Count; ++i)
            {
                for (int j = i; j < allFieldsAndProps.Count; ++j)
                {
                    if (j == i)
                        continue;
                    // if have the same names and NOT explicits
                    if (allFieldsAndProps[i].Name.Name == allFieldsAndProps[j].Name.Name &&
                        allFieldsAndProps[i].Name.AdditionalData == null &&
                        allFieldsAndProps[j].Name.AdditionalData == null)
                    {
                        // TODO: show previous field decl
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, allFieldsAndProps[j], [], ErrorCode.Get(CTEN.ClassPropsFieldsSame));
                    }
                }
            }

            // if not for imported - generate other shite
            if (!forImported)
            {
                var decls = new List<AstDeclaration>();
                decls.AddRange(allFields);
                decls.AddRange(allProps);
                decls.AddRange(allFuncs);
                // getting all fields and mark them abstract if it is an interface
                if (decl is AstClassDecl classDecl && classDecl.IsInterface)
                {
                    foreach (var f in decls)
                    {
                        // add abstract key to the field if it is an interface
                        SpecialKeysHelper.AddSpecialKeyToDecl(f, Lexer.CreateToken(TokenType.KwAbstract, f.Location.Beginning),
                            _compiler.MessageHandler, _currentSourceFile);
                        // and public :)
                        SpecialKeysHelper.AddSpecialKeyToDecl(f, Lexer.CreateToken(TokenType.KwPublic, f.Location.Beginning),
                            _compiler.MessageHandler, _currentSourceFile);
                    }
                }
                // add kw private if there is no one
                else
                {
                    // 1 - is access special key type!!!
                    if (!SpecialKeysHelper.HasSpecialKeyType(decl, 1, out int _))
                        SpecialKeysHelper.AddSpecialKeyToDecl(decl, Lexer.CreateToken(TokenType.KwPrivate, decl.Location.Beginning),
                            _compiler.MessageHandler, _currentSourceFile);
                    foreach (var f in decls)
                    {
                        // skip static ctors
                        if (f is AstFuncDecl fnccc && fnccc.ClassFunctionType == ClassFunctionType.StaticCtor)
                            continue;
                        // 1 - is access special key type!!!
                        if (!SpecialKeysHelper.HasSpecialKeyType(f, 1, out int _))
                            SpecialKeysHelper.AddSpecialKeyToDecl(f, Lexer.CreateToken(TokenType.KwPrivate, f.Location.Beginning),
                                _compiler.MessageHandler, _currentSourceFile);
                    }
                }

                // get funcs again after this :) sorry
                allFuncs = decl.GetDeclarations().Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl);

                // error if user created a func with the initializer name
                var specialFuncs = allFuncs.Where(x => (x.Name.Name.EndsWith($"::{decl.Name.Name}_ini") ||
                                                        x.Name.Name.EndsWith($"::{decl.Name.Name}_ctor") ||
                                                        x.Name.Name.EndsWith($"::{decl.Name.Name}_stor") || // static ctor
                                                        x.Name.Name.EndsWith($"::{decl.Name.Name}_dtor")));
                foreach (var fnc in specialFuncs)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, fnc.Name, [decl.Name.Name], ErrorCode.Get(CTEN.ClassFuncNameNotAllowed));
                }

                // static ctor is always generated
                PostPrepareGenerateClassStaticConstructor(decl, allFuncs.Where(x => x.ClassFunctionType == ClassFunctionType.StaticCtor).ToList());
                // generating all the shite only if the class is not static
                if (!decl.SpecialKeys.Contains(TokenType.KwStatic))
                {
                    PostPrepareGenerateClassInitializer(decl);
                    // passing all the existing ctors
                    PostPrepareGenerateClassConstructor(decl, allFuncs.Where(x => x.ClassFunctionType == ClassFunctionType.Ctor).ToList());
                    PostPrepareGenerateClassDestructor(decl, allFuncs.Where(x => x.ClassFunctionType == ClassFunctionType.Dtor).ToList());
                }

                // 
                foreach (var d in decl.GetDeclarations())
                {
                    FuncPrepareAfterAll(d, decl);
                }
            }
            else
            {
                var specialFuncs = decl.GetDeclarations().Where(x => x is AstFuncDecl &&
                                                               (x.Name.Name == ($"{decl.Name.Name}_ini") ||
                                                                x.Name.Name == ($"{decl.Name.Name}_ctor") ||
                                                                x.Name.Name == ($"{decl.Name.Name}_stor") || // static ctor
                                                                x.Name.Name == ($"{decl.Name.Name}_dtor")));
                foreach (var f in specialFuncs)
                {
                    if (f.Name.Name.EndsWith("_ini"))
                        (f as AstFuncDecl).ClassFunctionType = ClassFunctionType.Initializer;
                    else if (f.Name.Name.EndsWith("_ctor"))
                        (f as AstFuncDecl).ClassFunctionType = ClassFunctionType.Ctor;
                    else if (f.Name.Name.EndsWith("_stor"))
                        (f as AstFuncDecl).ClassFunctionType = ClassFunctionType.StaticCtor;
                    else if (f.Name.Name.EndsWith("_dtor"))
                        (f as AstFuncDecl).ClassFunctionType = ClassFunctionType.Dtor;
                }
            }
        }

        private void PostPrepareGenerateClassInitializer(AstDeclaration decl)
        {
            // skip if it is an interface
            if (decl is AstClassDecl clsDecl && clsDecl.IsInterface)
                return;

            // location for all the things
            var comLoc = decl.Name.Location;

            // the block with all field inits
            var iniBlock = GetFieldsToInitialize(decl, false);

            // the ini func
            var iniDecl = new AstFuncDecl(new List<AstParamDecl>(),
            new AstNestedExpr(new AstIdExpr("void", comLoc), null, comLoc),
            iniBlock,
            new AstIdExpr($"{decl.Name.Name}_ini", comLoc),
            "", comLoc);
            iniDecl.SpecialKeys.Insert(0, Lexer.CreateToken(TokenType.KwPrivate, decl.Location.Beginning)); // ini is private because it is called inside ctors
            iniDecl.ClassFunctionType = ClassFunctionType.Initializer;
            iniDecl.ContainingParent = decl;
            iniDecl.IsSyntheticStatement = true;

            decl.GetDeclarations().Insert(0, iniDecl);
        }

        private void PostPrepareGenerateClassConstructor(AstDeclaration decl, List<AstFuncDecl> ctors)
        {
            // skip if it is an interface
            if (decl is AstClassDecl clsDecl && clsDecl.IsInterface)
                return;

            // location for all the things
            var comLoc = decl.Name.Location;

            if (ctors.Count == 0)
            {
                // there is no ctor. need to create one
                List<AstStatement> ctorBlockStatements = new List<AstStatement>();
                // creating ini func call
                ctorBlockStatements.Add(new AstCallExpr(
                    new AstNestedExpr(new AstIdExpr("this"), null, comLoc),
                    new AstIdExpr($"{decl.Name.Name}_ini", comLoc), null, comLoc)
                {
                    IsSyntheticStatement = true,
                });
                // the block with call of ini func
                var ctorBlock = new AstBlockExpr(ctorBlockStatements, comLoc);

                // the ctor func
                var ctorDecl = new AstFuncDecl(new List<AstParamDecl>(),
                    new AstNestedExpr(new AstIdExpr("void", comLoc), null, comLoc),
                    ctorBlock,
                    new AstIdExpr($"{decl.Name.Name}_ctor", comLoc),
                    "", comLoc);
                ctorDecl.BaseCtorCall = new AstBaseCtorStmt(location: ctorDecl.Name)
                {
                    IsSyntheticStatement = true,
                };
                ctorDecl.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwPublic, decl.Location.Beginning)); // default ctor is public
                ctorDecl.ClassFunctionType = ClassFunctionType.Ctor;
                ctorDecl.ContainingParent = decl;
                ctorDecl.IsSyntheticStatement = true;

                decl.GetDeclarations().Insert(1, ctorDecl); // the first one has to be ini func
            }
            else
            {
                foreach (var ct in ctors)
                {
                    ct.Name = ct.Name.GetCopy($"{ct.Name.Name}_ctor");
                    // insert ini func call at the beginning of the func body
                    /// make sure that this shite is the same as in <see cref="RenameFromGenericToRealType"/>
                    ct.Body.Statements.Insert(0, new AstCallExpr(
                        new AstNestedExpr(new AstIdExpr("this"), null, comLoc),
                        new AstIdExpr($"{decl.Name.Name}_ini", comLoc), null, comLoc)
                    {
                        IsSyntheticStatement = true,
                    });

                    // if the base ctor call is empty - create one with no params
                    if (ct.BaseCtorCall == null)
                        ct.BaseCtorCall = new AstBaseCtorStmt(location: ct.Name)
                        {
                            IsSyntheticStatement = true,
                        };
                }
            }
        }

        private void PostPrepareGenerateClassDestructor(AstDeclaration decl, List<AstFuncDecl> dtors)
        {
            // skip if it is an interface
            if (decl is AstClassDecl clsDecl && clsDecl.IsInterface)
                return;

            // location for all the things
            var comLoc = decl.Name.Location;

            if (dtors.Count == 0)
            {
                // there is no dtor. need to create one
                List<AstStatement> dtorBlockStatements = new List<AstStatement>();

                // TODO: do i need to place here something?

                // the block with 
                var dtorBlock = new AstBlockExpr(dtorBlockStatements, comLoc);

                // the ctor func
                var dtorDecl = new AstFuncDecl(new List<AstParamDecl>(),
                new AstNestedExpr(new AstIdExpr("void", comLoc), null, comLoc),
                dtorBlock,
                new AstIdExpr($"{decl.Name.Name}_dtor", comLoc),
                "", comLoc);
                dtorDecl.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwPublic, decl.Location.Beginning)); // default dtor is public
                dtorDecl.ClassFunctionType = ClassFunctionType.Dtor;
                dtorDecl.ContainingParent = decl;
                dtorDecl.IsSyntheticStatement = true;

                decl.GetDeclarations().Add(dtorDecl);
            }
            else if (dtors.Count == 1)
            {
                var dtorFunc = dtors[0];
                dtorFunc.Name = dtorFunc.Name.GetCopy($"{dtorFunc.Name.Name}_dtor");
            }
            else
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, dtors[1], [], ErrorCode.Get(CTEN.ClassDtorOnlyOne));
            }
        }

        private void PostPrepareGenerateClassStaticConstructor(AstDeclaration decl, List<AstFuncDecl> ctors)
        {
            // skip interfaces
            if (decl is AstClassDecl clsDecl && clsDecl.IsInterface)
                return;

            // location for all the things
            var comLoc = decl.Name.Location;

            List<AstDeclaration> decls = decl.GetDeclarations();

            // creating the ini block for fields
            var iniBlock = GetFieldsToInitialize(decl, true);

            // we need to add a static var to check that the stor was called
            int genericAmount = 0;
            if (decl.Name is AstIdGenericExpr genId)
                genericAmount = genId.GenericRealTypes.Count;

            string theVarName = $"__is_{_currentSourceFile.Namespace.Replace('.', '_')}_{decl.Name.Name}_stor_called{genericAmount}";
            var theVar = new AstVarDecl(new AstNestedExpr(new AstIdExpr("bool", comLoc), null, comLoc), new AstIdExpr(theVarName, comLoc), null, "", comLoc);
            theVar.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwStatic, decl.Location.Beginning));
            theVar.SpecialKeys.Insert(0, Lexer.CreateToken(TokenType.KwUnreflected, decl.Location.Beginning));
            theVar.ContainingParent = decl;
            theVar.IsStaticCtorField = true;
            theVar.IsSyntheticStatement = true;
            decls.Add(theVar);

            // set 'true' to the var
            /// make sure that this shite is the same as in <see cref="RenameFromGenericToRealType"/>
            var varAssign = new AstAssignStmt(new AstNestedExpr(new AstIdExpr(theVarName, comLoc), null, comLoc), new AstBoolExpr(true, comLoc), comLoc);
            iniBlock.Statements.Insert(0, varAssign); // should be the first statement
            AstIfStmt checkForInited = new AstIfStmt(new AstUnaryExpr("!", new AstIdExpr(theVarName, comLoc), comLoc), iniBlock, null, comLoc);

            if (ctors.Count == 0)
            {
                // there is no stor. need to create one
                List<AstStatement> storBlockStatements = new List<AstStatement>();
                storBlockStatements.Add(checkForInited);

                // the block with 
                var storBlock = new AstBlockExpr(storBlockStatements, comLoc);

                // the ctor func
                var storDecl = new AstFuncDecl(new List<AstParamDecl>(),
                new AstNestedExpr(new AstIdExpr("void", comLoc), null, comLoc),
                storBlock,
                new AstIdExpr($"{decl.Name.Name}_stor"),
                "", comLoc);
                storDecl.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwPublic, decl.Location.Beginning)); // stor is public
                storDecl.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwStatic, decl.Location.Beginning)); // stor is static
                storDecl.ClassFunctionType = ClassFunctionType.StaticCtor;
                storDecl.ContainingParent = decl;
                storDecl.IsSyntheticStatement = true;
                decls.Add(storDecl);

                storDecl.IsSyntheticStatement = true;
                storDecl.Attributes.Add(new AstAttributeStmt(new AstNestedExpr(new AstIdExpr("System.SuppressStackTraceAttribute"), null), [], comLoc));
            }
            else if (ctors.Count == 1)
            {
                var ctorFunc = ctors[0];
                ctorFunc.Name = ctorFunc.Name.GetCopy($"{ctorFunc.Name.Name}_stor");

                // stor can only have 'static' kw
                if (ctorFunc.SpecialKeys.Count > 1)
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, ctorFunc.Name, [], ErrorCode.Get(CTWN.StaticCtorKwsIgnored), null, HapetFrontend.Entities.ReportType.Warning);

                // move all user code under 'if' stmt
                checkForInited.BodyTrue.Statements.AddRange(ctorFunc.Body.Statements);
                ctorFunc.Body.Statements.Clear();

                // add check into user defined stor
                ctorFunc.Body.Statements.Add(checkForInited);

                ctorFunc.Attributes.Add(new AstAttributeStmt(new AstNestedExpr(new AstIdExpr("System.SuppressStackTraceAttribute"), null), [], ctorFunc.Location));
            }
            else
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, ctors[1], [], ErrorCode.Get(CTEN.ClassStorOnlyOne));
            }
        }        

        private AstBlockExpr GetFieldsToInitialize(AstDeclaration declB, bool forStatic)
        {
            // gettings all field decls and init them
            IEnumerable<AstVarDecl> allVarDecls;
            if (declB is AstClassDecl || declB is AstStructDecl)
            {
                allVarDecls = declB.GetDeclarations().Where(x => x is AstVarDecl && x is not AstIndexerDecl).Select(x => x as AstVarDecl);
            }
            else
            {
                // compiler error
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, declB.Name, [declB.AAAName], ErrorCode.Get(CTEN.NoFieldsInNonTypes));
                return null;
            }

            List<AstStatement> iniBlockStatements = new List<AstStatement>();
            foreach (AstVarDecl decl in allVarDecls)
            {
                // check if the var itself is a propa - skip them
                if (decl is AstPropertyDecl propD)
                    continue;

                // for static we need to get only static fields/props
                if (forStatic)
                {
                    // need to do this for statics
                    if (!decl.SpecialKeys.Contains(TokenType.KwStatic))
                        continue;
                }
                else
                {
                    // no need to do this for consts and statics
                    if (decl.SpecialKeys.Contains(TokenType.KwStatic) || decl.SpecialKeys.Contains(TokenType.KwConst))
                        continue;
                }

                // creating field assing statement
                var objectName = forStatic ? null : new AstNestedExpr(new AstIdExpr("this")
                {
                    Location = decl.Name.Location,
                    Scope = decl.Name.Scope,
                    IsSyntheticStatement = true,
                }, null)
                {
                    Location = decl.Name.Location,
                    Scope = decl.Name.Scope,
                    IsSyntheticStatement = true,
                };
                var target = new AstNestedExpr(decl.Name.GetCopy(), objectName, decl)
                {
                    Location = decl.Name.Location,
                    Scope = decl.Name.Scope,
                };
                AstExpression fieldInitializer;
                if (decl.Initializer != null)
                    fieldInitializer = decl.Initializer;
                else
                    fieldInitializer = new AstDefaultExpr(decl);

                /// this is a kostyl that is described here <see cref="Parser.ParseClassDeclaration"/>
                if (fieldInitializer is AstBlockExpr blckExpr)
                {
                    // skip last because the last one is the real value to be applied into variable 
                    iniBlockStatements.AddRange(blckExpr.Statements.SkipLast(1));
                    fieldInitializer = blckExpr.Statements.Last() as AstExpression;
                }

                // TODO: !!! check that non-static functions are not used in field initializers!!!
                // creating the assign
                var assign = new AstAssignStmt(target, fieldInitializer, decl);
                iniBlockStatements.Add(assign);
            }
            // the block with all field inits
            return new AstBlockExpr(iniBlockStatements);
        }

        public void FuncPrepareAfterAll(AstDeclaration decl, AstDeclaration parentDecl)
        {
            if (decl is not AstFuncDecl funcDecl)
            {
                return;
            }

            // adding 'this' param to func params
            if (!funcDecl.SpecialKeys.Contains(TokenType.KwStatic))
            {
                // for generic type - need to create an AstIdGenericExpr
                AstIdExpr thisParamType;
                if (parentDecl.HasGenericTypes)
                {
                    // getting pure generics from decl
                    var pureGenerics = GenericsHelper.GetGenericsFromName(parentDecl.Name as AstIdGenericExpr, _compiler.MessageHandler);
                    thisParamType = AstIdGenericExpr.FromAstIdExpr(parentDecl.Name.GetCopy(),
                        pureGenerics.Select(x => x as AstExpression).ToList());
                }
                else
                    thisParamType = parentDecl.Name.GetCopy();
                // creating the class instance 'this' param
                AstIdExpr paramName = new AstIdExpr("this")
                {
                    Location = decl.Name.Location,
                    Scope = decl.SubScope,
                    IsSyntheticStatement = true,
                };
                AstParamDecl thisParam = new AstParamDecl(new AstNestedExpr(thisParamType, null)
                {
                    Location = decl.Name.Location,
                    Scope = decl.SubScope,
                    IsSyntheticStatement = true,
                }, paramName)
                {
                    Location = decl.Name.Location,
                    Scope = decl.SubScope,
                    IsSyntheticStatement = true,
                };

                // we need to add ref to struct first param
                if (funcDecl.ContainingParent is AstStructDecl || funcDecl.ContainingParent is AstDelegateDecl)
                    thisParam.ParameterModificator = ParameterModificator.Ref;

                // adding the param as the func first param
                funcDecl.Parameters.Insert(0, thisParam);
            }

            // adding virtual key to all overrides
            if (funcDecl.SpecialKeys.Contains(TokenType.KwOverride))
                funcDecl.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwVirtual, funcDecl.SpecialKeys.GetType(TokenType.KwOverride).Location.Beginning));

            // abs has to not have impl
            if (funcDecl.SpecialKeys.Contains(TokenType.KwAbstract) &&
                funcDecl.Body != null &&
                !funcDecl.IsPropertyFunction)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, funcDecl.Name, [], ErrorCode.Get(CTEN.AbsMethodWithBody));
            }
        }

        private void PrepareEventFields(IEnumerable<AstVarDecl> fields)
        {
            foreach (var field in fields) 
            {
                if (!field.IsEvent)
                    continue;

                field.Type = new AstNestedExpr(new AstIdGenericExpr("System.Event", [field.Type], field.Type), null, field.Type);
                if (field.Initializer != null)
                {
                    // TODO: error - events should not have initializers, we need to create them by our own
                }

                // create our own initer
                field.Initializer = new AstNewExpr(field.Type.GetDeepCopy() as AstNestedExpr, new List<AstArgumentExpr>(), field.Type.Location);
            }
        }
    }
}
