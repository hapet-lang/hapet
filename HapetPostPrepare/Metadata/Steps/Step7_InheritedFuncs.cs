using System;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Types;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
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
                // check that the inheritance is generic
                // need to create a specific type of the inheritance
                if (inh.RightPart is AstIdGenericExpr genInhId)
                {
                    if (inhDecl.Name.Name.Contains("IList") && genInhId.GenericRealTypes.Count == 1 && genInhId.GenericRealTypes[0].OutType is IntType)
                    {

                    }
                    // WARN: allow genericTypes here - because we want to purely check the types
                    inhDecl = CreateRealTypeFromGeneric(inhDecl, genInhId, null, out var _, true) as AstClassDecl;
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

                                if (inhF.Name.Name.Contains("CopyTo") && decl.Name.Name.Contains("Array") &&
                                    inhF.ContainingParent.Name is AstIdGenericExpr gen && gen.GenericRealTypes.Count == 1 &&
                                    gen.GenericRealTypes[0].OutType is IntType)
                                {

                                }

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
                                    SpecialKeysHelper.AddSpecialKeyToDecl(currF, Lexer.CreateToken(TokenType.KwOverride, currF.Beginning), null, null);

                                    // add it to the new dictionary
                                    currentClassMethods.Remove(currF);
                                    allVirtualFunctions.Add(currF);
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
    }
}
