using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using HapetFrontend.Extensions;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareMetadataTypeInheritedPropsDecls(AstStatement stmt)
        {
            // this is needed just to check that all virtual/abstract props are implemented
            GetDeclarationProps__(stmt as AstDeclaration);
        }

        // to get all pure props including inherited
        private List<AstPropertyDecl> GetDeclarationProps__(AstDeclaration decl)
        {
            // all virtual/abstract props
            List<AstPropertyDecl> allPropDecls = new List<AstPropertyDecl>();
            List<AstNestedExpr> inheritedFrom;
            bool isInterface = false;
            List<AstPropertyDecl> currentPropDecls;
            if (decl is AstClassDecl clsDecl)
            {
                currentPropDecls = clsDecl.Declarations.Where(x => x is AstPropertyDecl).Select(x => x as AstPropertyDecl).ToList();
                inheritedFrom = clsDecl.InheritedFrom;
                isInterface = clsDecl.IsInterface;
            }
            else if (decl is AstStructDecl strDecl)
            {
                currentPropDecls = strDecl.Declarations.Where(x => x is AstPropertyDecl).Select(x => x as AstPropertyDecl).ToList();
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
                                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, currF,
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
    }
}
