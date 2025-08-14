using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using HapetFrontend.Extensions;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareInheritedShite()
        {
            AllPostPrepareMetadataInheritedFunctions();
            AllPostPrepareMetadataTypeInheritedFieldDecls();
            AllPostPrepareMetadataTypeInheritedPropsDecls();
        }

        #region Methods
        private void AllPostPrepareMetadataInheritedFunctions()
        {
            foreach (var cls in AllClassesMetadata.ToList())
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(cls))
                {
                    cls.AllVirtualMethods = new List<AstFuncDecl>();
                    continue;
                }

                _currentSourceFile = cls.SourceFile;
                if (cls.IsNestedDecl)
                    _currentParentStack.AddParent(cls.ParentDecl);
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataInheritedFunctions(cls);
                _currentParentStack.RemoveParent();
                if (cls.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            foreach (var str in AllStructsMetadata.ToList())
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(str))
                {
                    str.AllVirtualMethods = new List<AstFuncDecl>();
                    continue;
                }

                _currentSourceFile = str.SourceFile;
                if (str.IsNestedDecl)
                    _currentParentStack.AddParent(str.ParentDecl);
                _currentParentStack.AddParent(str);
                PostPrepareMetadataInheritedFunctions(str);
                _currentParentStack.RemoveParent();
                if (str.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
        }

        private void PostPrepareMetadataInheritedFunctions(AstStatement stmt)
        {
            if (stmt is AstDeclaration decl)
                GetPreparedVirtualMethodsOnce(decl);
        }

        // to get all virtual methods including inherited
        private List<AstFuncDecl> GetPreparedVirtualMethodsOnce(AstDeclaration decl)
        {
            if (decl is AstClassDecl cls)
            {
                if (cls.AllVirtualMethods == null)
                    cls.AllVirtualMethods = GetPreparedVirtualMethodsInternal(cls);
                return cls.AllVirtualMethods;
            }
            else if (decl is AstStructDecl str)
            {
                if (str.AllVirtualMethods == null)
                    str.AllVirtualMethods = GetPreparedVirtualMethodsInternal(str);
                return str.AllVirtualMethods;
            }
            return new List<AstFuncDecl>();
        }
        private List<AstFuncDecl> GetPreparedVirtualMethodsInternal(AstDeclaration decl)
        {
            List<AstFuncDecl> allVirtualFunctions = new List<AstFuncDecl>();
            List<AstFuncDecl> currentClassMethods;
            List<AstNestedExpr> inheritedFrom;
            bool isInterface = false;
            if (decl is AstClassDecl clsDecl)
            {
                currentClassMethods = clsDecl.Declarations.Where(x => x is AstFuncDecl fnc && !fnc.IsImplOfGeneric &&
                    !fnc.SpecialKeys.Contains(TokenType.KwStatic)).Select(x => x as AstFuncDecl).ToList();
                inheritedFrom = clsDecl.InheritedFrom;
                isInterface = clsDecl.IsInterface;
            }
            else if (decl is AstStructDecl strDecl)
            {
                currentClassMethods = strDecl.Declarations.Where(x => x is AstFuncDecl fnc && !fnc.IsImplOfGeneric &&
                    !fnc.SpecialKeys.Contains(TokenType.KwStatic)).Select(x => x as AstFuncDecl).ToList();
                inheritedFrom = strDecl.InheritedFrom;
            }
            else
            {
                return new List<AstFuncDecl>();
            }

            // all over the inherited shite
            foreach (var inh in inheritedFrom)
            {
                // it has to be a class type. if it is not - there was a error previously
                if (inh.OutType is not ClassType inhType)
                    break;

                var inhDecl = inhType.Declaration;
                if (inhDecl.HasGenericTypes)
                {
                    // TODO: compiler error - unexpected generic decl
                }

                // if the inh type is an interface
                if (inhDecl.IsInterface)
                {
                    // if we are also an interface - just add, no need to check implementations
                    if (isInterface)
                    {
                        // TODO: warn!!! check that method already exists in the list. it is possible probably
                        allVirtualFunctions.AddRange(GetPreparedVirtualMethodsOnce(inhDecl));
                    }
                    else
                    {
                        // get all the methods of interface
                        var inhMethods = GetPreparedVirtualMethodsOnce(inhDecl);
                        foreach (var inhF in inhMethods)
                        {
                            // skip first if is non static func
                            bool skipFirst = !inhF.SpecialKeys.Contains(TokenType.KwStatic);
                            // check if the interface is already implemented in parent classes
                            var definedInOneOfTheParents = allVirtualFunctions.GetSameByNameAndTypes(inhF, out int _, skipFirst);
                            // if the field was already presented previously
                            if (definedInOneOfTheParents != null)
                            {
                                // need to check that we do not implement it also
                                var currF = currentClassMethods.GetSameByNameAndTypes(inhF, out int _, skipFirst);

                                // check that the parent's function is virtual or abstract and 
                                // our func has override word - then allow
                                if (currF != null && (definedInOneOfTheParents.SpecialKeys.Contains(TokenType.KwVirtual) ||
                                    definedInOneOfTheParents.SpecialKeys.Contains(TokenType.KwAbstract)) &&
                                    currF.SpecialKeys.Contains(TokenType.KwOverride))
                                {
                                    // add it to the new dictionary
                                    currentClassMethods.Remove(currF);
                                    allVirtualFunctions.Add(currF);
                                    continue;
                                }
                                // if the func is in parent and in our and wihtout 'new' kw - error
                                else if (currF != null && !currF.SpecialKeys.Contains(TokenType.KwNew))
                                {
                                    // the method is implemented in parent class and current class
                                    // we need to error
                                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, currF.Name,
                                        [HapetType.AsString(definedInOneOfTheParents.ContainingParent.Type.OutType)], ErrorCode.Get(CTEN.MethodAlreadyDefined));
                                    continue;
                                }
                                // else - everything is ok probably
                            }
                            else
                            {
                                // if the method was not presented previously

                                // need to check that we do implement it
                                var currF = currentClassMethods.GetSameByNameAndTypes(inhF, out int _, skipFirst);
                                if (currF == null)
                                {
                                    if (!inhF.IsPropertyFunction)
                                        // error - the method of the interface was not implemented
                                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, inh,
                                            [HapetType.AsString(decl.Type.OutType),
                                                inhF.Name.Name], ErrorCode.Get(CTEN.NoMethodImplementation));
                                }
                                else
                                {
                                    // add it to the new dictionary
                                    currentClassMethods.Remove(currF);
                                    allVirtualFunctions.Add(currF);

                                    // add to it override special key
                                    SpecialKeysHelper.AddSpecialKeyToDecl(currF, Lexer.CreateToken(TokenType.KwOverride, currF.Location.Beginning),
                                        _compiler.MessageHandler, _currentSourceFile, false);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // if we are not an interface

                    // check that smth like
                    // class Pivo : object
                    if (!isInterface)
                    {
                        var parentFuncs = GetPreparedVirtualMethodsOnce(inhDecl);

                        // just add parent funcs if it is a class
                        allVirtualFunctions.AddRange(parentFuncs);

                        // search for overrides in the current class 
                        // and replace parent methods with our
                        foreach (var fCurr in currentClassMethods.Where(x => x.SpecialKeys.Contains(TokenType.KwOverride)).ToArray())
                        {
                            // skip first if is non static func
                            bool skipFirst = !fCurr.SpecialKeys.Contains(TokenType.KwStatic);
                            // check for signatures
                            var overridedFnc = allVirtualFunctions.GetSameByNameAndTypes(fCurr, out int fncIndex, skipFirst);
                            // TODO: error here? we go all over the override funcs and found no func to be overriden?
                            if (overridedFnc == null)
                                continue;
                            allVirtualFunctions[fncIndex] = fCurr;
                            // we need to remove it so it won't mess with us
                            currentClassMethods.Remove(fCurr);
                        }
                    }
                    else
                    {
                        // TODO: probably need to handle shite like:
                        // interface IPivo : object 
                        // implicit inheritance
                    }
                }
            }

            // check for shadowing
            foreach (var currM in currentClassMethods)
            {
                // skip virtual shite
                if (currM.SpecialKeys.Contains(TokenType.KwOverride))
                {
                    // TODO: probably error here - all overrides should be removed upper
                    continue;
                }
                // skip first if is non static func
                bool skipFirst = !currM.SpecialKeys.Contains(TokenType.KwStatic);
                var parentFnc = allVirtualFunctions.GetSameByNameAndTypes(currM, out int _, skipFirst);
                if (parentFnc != null && !currM.SpecialKeys.Contains(TokenType.KwNew))
                {
                    // skip property functions - property would error by its own
                    if (parentFnc.IsPropertyFunction)
                        continue;

                    // error - function shadowing
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, currM.Name,
                        [HapetType.AsString(parentFnc.Type.OutType)], ErrorCode.Get(CTEN.FunctionShadowing));
                }
                else if (parentFnc != null && currM.SpecialKeys.Contains(TokenType.KwNew))
                {
                    // all is ok
                    // to set brkp here
                }
            }

            // check if all implemented
            if (!isInterface && !decl.SpecialKeys.Contains(TokenType.KwAbstract))
            {
                // if it is not an interface nor abstract class - all abstract shite has to be implemented
                foreach (var m in allVirtualFunctions)
                {
                    // skip overrided-abstract methods
                    // the parent would be our cls if overrided
                    if (m.ContainingParent == decl)
                        continue;

                    if (m.SpecialKeys.Contains(TokenType.KwAbstract))
                    {
                        // skip property functions - property would error by its own
                        if (m.IsPropertyFunction)
                            continue;

                        // error - implementation of method not found in curr class
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name,
                            [m.Name.Name], ErrorCode.Get(CTEN.NoAbsMethodImpl));
                    }
                }
            }

            if (isInterface)
                // add here all shite because it is an interface
                allVirtualFunctions.AddRange(currentClassMethods);
            else
                // add here only virtual shite
                allVirtualFunctions.AddRange(currentClassMethods.Where(x => x.SpecialKeys.Contains(TokenType.KwAbstract) ||
                                                                           x.SpecialKeys.Contains(TokenType.KwVirtual)));
            return allVirtualFunctions;
        }
        #endregion

        #region Fields
        private void AllPostPrepareMetadataTypeInheritedFieldDecls()
        {
            var classes = AllClassesMetadata.ToList();
            var structures = AllStructsMetadata.ToList();

            // resolve all inherited fields of classes
            foreach (var cls in classes)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(cls))
                    continue;

                _currentSourceFile = cls.SourceFile;
                if (cls.IsNestedDecl)
                    _currentParentStack.AddParent(cls.ParentDecl);
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataTypeInheritedFieldDecls(cls);
                _currentParentStack.RemoveParent();
                if (cls.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            foreach (var str in structures)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(str))
                    continue;

                _currentSourceFile = str.SourceFile;
                if (str.IsNestedDecl)
                    _currentParentStack.AddParent(str.ParentDecl);
                _currentParentStack.AddParent(str);
                PostPrepareMetadataTypeInheritedFieldDecls(str);
                _currentParentStack.RemoveParent();
                if (str.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
        }

        private void PostPrepareMetadataTypeInheritedFieldDecls(AstStatement stmt)
        {
            // this is needed just to check that all virtual/abstract fields are implemented
            GetDeclarationFields__(stmt as AstDeclaration);
        }

        // to check all virtual/abstract fields including inherited
        private List<AstVarDecl> GetDeclarationFields__(AstDeclaration decl)
        {
            List<AstNestedExpr> inheritedFrom;
            bool isInterface = false;
            List<AstVarDecl> allFieldDecls = new List<AstVarDecl>();
            List<AstVarDecl> currentFieldDecls;
            if (decl is AstClassDecl clsDecl)
            {
                currentFieldDecls = clsDecl.Declarations.GetStructFields();
                inheritedFrom = clsDecl.InheritedFrom;
                isInterface = clsDecl.IsInterface;
            }
            else if (decl is AstStructDecl strDecl)
            {
                currentFieldDecls = strDecl.Declarations.GetStructFields();
                inheritedFrom = strDecl.InheritedFrom;
            }
            else
            {
                return new List<AstVarDecl>();
            }

            // all over the inherited shite
            foreach (var inh in inheritedFrom)
            {
                // it has to be a class type. if it is not - there was a error previously
                if (inh.OutType is not ClassType)
                    break;

                var inhDecl = (inh.OutType as ClassType).Declaration;
                if (inhDecl.HasGenericTypes)
                {
                    // TODO: compiler error - unexpected generic decl
                }

                // if the inh type is an interface
                if (inhDecl.IsInterface)
                {
                    // if we are also an interface - just add, no need to check implementations
                    if (isInterface)
                    {
                        allFieldDecls.AddRange(GetDeclarationFields__(inhDecl));
                    }
                    else
                    {
                        // get all the fields of interface
                        var inhFields = GetDeclarationFields__(inhDecl);
                        foreach (var inhF in inhFields)
                        {
                            // check if the interface is already implemented in parent classes
                            var definedInOneOfTheParents = allFieldDecls.GetSameDeclByTypeAndName(inhF, out var _);
                            // if the field was already presented previously
                            if (definedInOneOfTheParents != null)
                            {
                                // need to check that we do not implement it also
                                var currF = currentFieldDecls.GetSameDeclByTypeAndName(inhF, out int _);

                                // check that the parent's field is virtual or abstract and 
                                // our field has override word - then allow
                                if (currF != null && (definedInOneOfTheParents.SpecialKeys.Contains(TokenType.KwVirtual) ||
                                    definedInOneOfTheParents.SpecialKeys.Contains(TokenType.KwAbstract)) &&
                                    currF.SpecialKeys.Contains(TokenType.KwOverride))
                                {
                                    // add it to the new dictionary
                                    currentFieldDecls.Remove(currF);
                                    allFieldDecls.Add(currF);
                                    continue;
                                }
                                // if the prop is in parent and in our and wihtout 'new' kw - error
                                if (currF != null && !currF.SpecialKeys.Contains(TokenType.KwNew))
                                {
                                    // the field is implemented in parent class and current class
                                    // we need to error
                                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, currF,
                                        [HapetType.AsString(definedInOneOfTheParents.ContainingParent.Type.OutType)], ErrorCode.Get(CTEN.FieldAlreadyDefined));
                                    continue;
                                }
                                // else - everything is ok probably
                            }
                            else
                            {
                                // if the field was not presented previously

                                // need to check that we do implement it
                                var currF = currentFieldDecls.GetSameDeclByTypeAndName(inhF, out var _);
                                if (currF == null)
                                {
                                    if (!inhF.IsPropertyField)
                                        // error - the field of the interface was not implemented
                                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, inh,
                                            [HapetType.AsString(decl.Type.OutType), inhF.Name.Name], ErrorCode.Get(CTEN.NoFieldImplementation));
                                }
                                else
                                {
                                    // add it to the new dictionary
                                    currentFieldDecls.Remove(currF);
                                    allFieldDecls.Add(currF);

                                    // add to it override special key
                                    SpecialKeysHelper.AddSpecialKeyToDecl(currF, Lexer.CreateToken(TokenType.KwOverride, currF.Location.Beginning),
                                        _compiler.MessageHandler, _currentSourceFile, false);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // if we are not an interface

                    // check that smth like
                    // class Pivo : object
                    if (!isInterface)
                    {
                        var parentFields = GetDeclarationFields__(inhDecl);
                        // just add parent fields if it is a class
                        allFieldDecls.AddRange(parentFields);

                        // search for overrides in the current class 
                        // and replace parent methods with our
                        foreach (var fCurr in currentFieldDecls.Where(x => x.SpecialKeys.Contains(TokenType.KwOverride)).ToArray())
                        {
                            // check for signatures
                            var overridedProp = allFieldDecls.GetSameDeclByTypeAndName(fCurr, out int index);
                            // TODO: error here? we go all over the override fields and found no field to be overriden?
                            if (overridedProp == null)
                                continue;
                            allFieldDecls[index] = fCurr;
                            // we need to remove it so it won't mess with us
                            currentFieldDecls.Remove(fCurr);
                        }
                    }
                    else
                    {
                        // TODO: probably need to handle shite like:
                        // interface IPivo : object 
                        // implicit inheritance
                    }
                }
            }

            // check for shadowing
            foreach (var currP in currentFieldDecls)
            {
                // skip virtual shite
                if (currP.SpecialKeys.Contains(TokenType.KwOverride))
                {
                    // TODO: probably error here - all overrides should be removed upper
                    continue;
                }
                var parentProp = allFieldDecls.GetSameDeclByTypeAndName(currP, out int _);
                if (parentProp != null && !currP.SpecialKeys.Contains(TokenType.KwNew))
                {
                    // error - property shadowing
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, currP.Name,
                        [$"{parentProp.ContainingParent.Name.Name}::{parentProp.Name.Name}"], ErrorCode.Get(CTEN.PropertyShadowing));
                }
                else if (parentProp != null && currP.SpecialKeys.Contains(TokenType.KwNew))
                {
                    // all is ok
                    // to set brkp here
                }
            }

            // check if all implemented
            if (!isInterface && !decl.SpecialKeys.Contains(TokenType.KwAbstract))
            {
                // if it is not an interface nor abstract class - all abstract shite has to be implemented
                foreach (var p in allFieldDecls)
                {
                    // skip overrided-abstract methods
                    // the parent would be our cls if overrided
                    if (p.ContainingParent == decl)
                        continue;

                    if (p.SpecialKeys.Contains(TokenType.KwAbstract))
                    {
                        // error - implementation of method not found in curr class
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name,
                            [$"{p.ContainingParent.Name.Name}::{p.Name.Name}"], ErrorCode.Get(CTEN.NoAbsPropertyImpl));
                    }
                }
            }

            if (isInterface)
                // add here all shite because it is an interface
                allFieldDecls.AddRange(currentFieldDecls);
            else
                // add here only virtual shite
                allFieldDecls.AddRange(currentFieldDecls.Where(x => x.SpecialKeys.Contains(TokenType.KwAbstract) ||
                                                                           x.SpecialKeys.Contains(TokenType.KwVirtual)));
            return allFieldDecls;
        }
        #endregion

        #region Properties
        private void AllPostPrepareMetadataTypeInheritedPropsDecls()
        {
            var classes = AllClassesMetadata.ToList();
            var structures = AllStructsMetadata.ToList();

            // resolve all inherited props of classes
            foreach (var cls in classes)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(cls))
                {
                    cls.AllVirtualProps = new List<AstPropertyDecl>();
                    continue;
                }

                _currentSourceFile = cls.SourceFile;
                if (cls.IsNestedDecl)
                    _currentParentStack.AddParent(cls.ParentDecl);
                _currentParentStack.AddParent(cls);
                PostPrepareMetadataTypeInheritedPropsDecls(cls);
                _currentParentStack.RemoveParent();
                if (cls.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
            foreach (var str in structures)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(str))
                {
                    str.AllVirtualProps = new List<AstPropertyDecl>();
                    continue;
                }

                _currentSourceFile = str.SourceFile;
                if (str.IsNestedDecl)
                    _currentParentStack.AddParent(str.ParentDecl);
                _currentParentStack.AddParent(str);
                PostPrepareMetadataTypeInheritedPropsDecls(str);
                _currentParentStack.RemoveParent();
                if (str.IsNestedDecl)
                    _currentParentStack.RemoveParent();
            }
        }

        // TODO: remove the step - it is the same as step 8
        private void PostPrepareMetadataTypeInheritedPropsDecls(AstStatement stmt)
        {
            // this is needed just to check that all virtual/abstract props are implemented
            var virtProps = GetDeclarationProps__(stmt as AstDeclaration);
            if (stmt is AstClassDecl cls)
                cls.AllVirtualProps = virtProps;
            else if (stmt is AstStructDecl str)
                str.AllVirtualProps = virtProps;
        }

        // to check all virtual/abstract props including inherited
        private List<AstPropertyDecl> GetDeclarationProps__(AstDeclaration decl)
        {
            if (decl.Name.Name.Contains("List"))
            {

            }
            // all virtual/abstract props
            List<AstPropertyDecl> allPropDecls = new List<AstPropertyDecl>();
            List<AstNestedExpr> inheritedFrom;
            bool isInterface = false;
            List<AstPropertyDecl> currentPropDecls;
            if (decl is AstClassDecl clsDecl)
            {
                currentPropDecls = clsDecl.Declarations.Where(x => x is AstPropertyDecl && !x.IsImplOfGeneric &&
                    !x.SpecialKeys.Contains(TokenType.KwStatic)).Select(x => x as AstPropertyDecl).ToList();
                inheritedFrom = clsDecl.InheritedFrom;
                isInterface = clsDecl.IsInterface;
            }
            else if (decl is AstStructDecl strDecl)
            {
                currentPropDecls = strDecl.Declarations.Where(x => x is AstPropertyDecl && !x.IsImplOfGeneric &&
                    !x.SpecialKeys.Contains(TokenType.KwStatic)).Select(x => x as AstPropertyDecl).ToList();
                inheritedFrom = strDecl.InheritedFrom;
            }
            else
            {
                return new List<AstPropertyDecl>();
            }

            // all over the inherited shite
            foreach (var inh in inheritedFrom)
            {
                // it has to be a class type. if it is not - there was a error previously
                if (inh.OutType is not ClassType)
                    break;

                var inhDecl = (inh.OutType as ClassType).Declaration;
                if (inhDecl.HasGenericTypes)
                {
                    // TODO: compiler error - unexpected generic decl
                }

                // if the inh type is an interface
                if (inhDecl.IsInterface)
                {
                    // if we are also an interface - just add, no need to check implementations
                    if (isInterface)
                    {
                        allPropDecls.AddRange(GetDeclarationProps__(inhDecl));
                    }
                    else
                    {
                        // get all the props of interface
                        var inhProps = GetDeclarationProps__(inhDecl);
                        foreach (var inhF in inhProps)
                        {
                            // check if the interface is already implemented in parent classes
                            var definedInOneOfTheParents = allPropDecls.GetSameDeclByTypeAndName(inhF, out int _);
                            // if the prop was already presented previously
                            if (definedInOneOfTheParents != null)
                            {
                                // need to check that we do not implement it also
                                var currF = currentPropDecls.GetSameDeclByTypeAndName(inhF, out int _);

                                // check that the parent's prop is virtual or abstract and 
                                // our prop has override word - then allow
                                if (currF != null && (definedInOneOfTheParents.SpecialKeys.Contains(TokenType.KwVirtual) ||
                                    definedInOneOfTheParents.SpecialKeys.Contains(TokenType.KwAbstract)) &&
                                    currF.SpecialKeys.Contains(TokenType.KwOverride))
                                {
                                    // add it to the new dictionary
                                    currentPropDecls.Remove(currF);
                                    allPropDecls.Add(currF);
                                    continue;
                                }
                                // if the prop is in parent and in our and wihtout 'new' kw - error
                                else if (currF != null && !currF.SpecialKeys.Contains(TokenType.KwNew))
                                {
                                    // the prop is implemented in parent class and current class
                                    // we need to error
                                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, currF.Name,
                                        [HapetType.AsString(definedInOneOfTheParents.ContainingParent.Type.OutType)], ErrorCode.Get(CTEN.PropaAlreadyDefined));
                                    continue;
                                }
                                // else - everything is ok probably
                            }
                            else
                            {
                                // if the prop was not presented previously

                                // need to check that we do implement it
                                var currF = currentPropDecls.GetSameDeclByTypeAndName(inhF, out int _);
                                if (currF == null)
                                {
                                    // error - the prop of the interface was not implemented
                                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, inh,
                                        [HapetType.AsString(decl.Type.OutType), inhF.Name.Name], ErrorCode.Get(CTEN.NoPropaImplementation));
                                }
                                else
                                {
                                    // add it to the new dictionary
                                    currentPropDecls.Remove(currF);
                                    allPropDecls.Add(currF);

                                    // add to it override special key
                                    SpecialKeysHelper.AddSpecialKeyToDecl(currF, Lexer.CreateToken(TokenType.KwOverride, currF.Location.Beginning),
                                        _compiler.MessageHandler, _currentSourceFile, false);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // if we are not an interface

                    // check that smth like
                    // class Pivo : object
                    if (!isInterface)
                    {
                        var parentProps = GetDeclarationProps__(inhDecl);
                        // just add parent props if it is a class
                        allPropDecls.AddRange(parentProps);

                        // search for overrides in the current class 
                        // and replace parent methods with our
                        foreach (var fCurr in currentPropDecls.Where(x => x.SpecialKeys.Contains(TokenType.KwOverride)).ToArray())
                        {
                            // check for signatures
                            var overridedProp = allPropDecls.GetSameDeclByTypeAndName(fCurr, out int index);
                            // TODO: error here? we go all over the override props and found no prop to be overriden?
                            if (overridedProp == null)
                                continue;
                            allPropDecls[index] = fCurr;
                            // we need to remove it so it won't mess with us
                            currentPropDecls.Remove(fCurr);
                        }
                    }
                    else
                    {
                        // TODO: probably need to handle shite like:
                        // interface IPivo : object 
                        // implicit inheritance
                    }
                }
            }

            // check for shadowing
            foreach (var currP in currentPropDecls)
            {
                // skip virtual shite
                if (currP.SpecialKeys.Contains(TokenType.KwOverride))
                {
                    // TODO: probably error here - all overrides should be removed upper
                    continue;
                }
                var parentProp = allPropDecls.GetSameDeclByTypeAndName(currP, out int _);
                if (parentProp != null && !currP.SpecialKeys.Contains(TokenType.KwNew))
                {
                    // error - property shadowing
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, currP.Name,
                        [$"{parentProp.ContainingParent.Name.Name}::{parentProp.Name.Name}"], ErrorCode.Get(CTEN.PropertyShadowing));
                }
                else if (parentProp != null && currP.SpecialKeys.Contains(TokenType.KwNew))
                {
                    // all is ok
                    // to set brkp here
                }
            }

            // check if all implemented
            if (!isInterface && !decl.SpecialKeys.Contains(TokenType.KwAbstract))
            {
                // if it is not an interface nor abstract class - all abstract shite has to be implemented
                foreach (var p in allPropDecls)
                {
                    // skip overrided-abstract methods
                    // the parent would be our cls if overrided
                    if (p.ContainingParent == decl)
                        continue;

                    if (p.SpecialKeys.Contains(TokenType.KwAbstract))
                    {
                        // error - implementation of method not found in curr class
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name,
                            [$"{p.ContainingParent.Name.Name}::{p.Name.Name}"], ErrorCode.Get(CTEN.NoAbsPropertyImpl));
                    }
                }
            }

            if (isInterface)
                // add here all shite because it is an interface
                allPropDecls.AddRange(currentPropDecls);
            else
                // add here only virtual shite
                allPropDecls.AddRange(currentPropDecls.Where(x => x.SpecialKeys.Contains(TokenType.KwAbstract) ||
                                                                           x.SpecialKeys.Contains(TokenType.KwVirtual)));
            return allPropDecls;
        }
        #endregion
    }
}
