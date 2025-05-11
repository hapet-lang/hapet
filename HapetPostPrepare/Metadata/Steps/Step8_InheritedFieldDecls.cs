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
    }
}
