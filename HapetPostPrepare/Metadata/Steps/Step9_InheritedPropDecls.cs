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
            if (stmt is AstClassDecl cls)
                cls.AllRawProps = GetPreparedProps__(cls);
            else if (stmt is AstStructDecl str)
                str.AllRawProps = GetPreparedProps__(str);
        }

        private void PostPrepareMetadataTypeInheritedPropsDeclsCopy(AstStatement stmt)
        {
            if (stmt is AstClassDecl cls)
                CopyInheritedProps__(cls, cls.AllRawProps);
            else if (stmt is AstStructDecl str)
                CopyInheritedProps__(str, str.AllRawProps);
        }

        private static void CopyInheritedProps__(AstDeclaration decl, List<AstPropertyDecl> preparedDecls)
        {
            List<AstDeclaration> currDecls;
            if (decl is AstClassDecl clsDecl)
                currDecls = clsDecl.Declarations;
            else if (decl is AstStructDecl strDecl)
                currDecls = strDecl.Declarations;
            else
                return;

            // remove all current props
            foreach (var fieldDecl in currDecls.Where(x => x is AstPropertyDecl).Select(x => x as AstPropertyDecl).ToList())
            {
                decl.SubScope.RemoveDeclSymbol(fieldDecl.Name.Name, fieldDecl);
                currDecls.Remove(fieldDecl);
            }

            List<AstDeclaration> toInsert = new List<AstDeclaration>();
            // all over the parent fields - copy them
            foreach (var propDecl in preparedDecls)
            {
                // change parent and scope
                var newVar = propDecl.GetCopyForAnotherType(decl);
                // define the symbol
                decl.SubScope.DefineDeclSymbol(newVar.Name.Name, newVar);

                toInsert.Add(newVar);
            }

            // insert them to the end
            currDecls.AddRange(toInsert);
        }

        // to get all pure props including inherited
        private List<AstPropertyDecl> GetPreparedProps__(AstDeclaration decl)
        {
            List<AstPropertyDecl> inheritedPropDecls = new List<AstPropertyDecl>();
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
                        inheritedPropDecls.AddRange(GetPreparedProps__(inhDecl));
                    }
                    else
                    {
                        // get all the props of interface
                        var inhFields = GetPreparedProps__(inhDecl);
                        foreach (var inhF in inhFields)
                        {
                            // check if the interface is already implemented in parent classes
                            var definedInOneOfTheParents = inheritedPropDecls.GetSameDeclByTypeAndName(inhF, out int _);
                            // if the prop was already presented previously
                            if (definedInOneOfTheParents != null)
                            {
                                // check if the already defined prop is by the interface
                                bool isInherited = definedInOneOfTheParents.ContainingParent.Type.OutType.IsInheritedFrom(inhF.ContainingParent.Type.OutType as ClassType, true);
                                // if inherited - this is a parent cls already implemented the prop - no need to warn
                                if (isInherited)
                                {
                                    // need to check that we do not implement it also
                                    var currF = currentPropDecls.GetSameDeclByTypeAndName(inhF, out int _);
                                    if (currF != null)
                                    {
                                        // the prop is implemented in parent class and current class
                                        // we need to error
                                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, currF,
                                            [HapetType.AsString(definedInOneOfTheParents.ContainingParent.Type.OutType)], ErrorCode.Get(CTEN.FieldAlreadyDefined));
                                        continue;
                                    }
                                    // else - everything is ok probably
                                }
                                else
                                {
                                    // TODO: not todo. but C# allows shite like this:
                                    /*
                                        public interface IAnime
                                        {
                                            short Field111 { get; set; }
                                        }

                                        public class BaseCls
                                        {
                                            public short Field111 { get; set; }
                                        }

                                        public class Derived : BaseCls, IAnime
                                        {

                                        }
                                     */
                                    // but we can't because of interface offset calcs. md could be fixed somehow?

                                    // we need to error
                                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, definedInOneOfTheParents,
                                        [HapetType.AsString(definedInOneOfTheParents.ContainingParent.Type.OutType),
                                            HapetType.AsString(decl.Type.OutType),
                                            HapetType.AsString(inh.OutType)],
                                        ErrorCode.Get(CTEN.DoubleInterfaceCringe));
                                    continue;
                                }
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
                                        [HapetType.AsString(decl.Type.OutType), inhF.Name.Name], ErrorCode.Get(CTEN.NoFieldImplementation));
                                }
                                else
                                {
                                    // add it to the new dictionary
                                    currentPropDecls.Remove(currF);
                                    inheritedPropDecls.Add(currF);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // if we are not an interface, happens when:
                    // interface IAnime : object
                    if (!isInterface)
                    {
                        var parentProps = GetPreparedProps__(inhDecl);
                        // just add parent props if it is a class
                        inheritedPropDecls.AddRange(parentProps);

                        // search for overrides in the current class 
                        // and replace parent methods with our
                        foreach (var fCurr in currentPropDecls.Where(x => x.SpecialKeys.Contains(TokenType.KwOverride)).ToArray())
                        {
                            // check for signatures
                            var overridedProp = inheritedPropDecls.GetSameDeclByTypeAndName(fCurr, out int index);
                            // TODO: error here? we go all over the override props and found no prop to be overriden?
                            if (overridedProp == null)
                                continue;
                            inheritedPropDecls[index] = fCurr;
                            // we need to remove it so it won't mess with us
                            currentPropDecls.Remove(fCurr);
                        }
                    }
                }
            }

            // check for shadowing
            foreach (var currP in currentPropDecls)
            {
                // skip virtual shite
                if (currP.SpecialKeys.Contains(TokenType.KwOverride))
                    continue;
                var parentProp = inheritedPropDecls.GetSameDeclByTypeAndName(currP, out int _);
                if (parentProp != null)
                {
                    // error - property shadowing
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, currP.Name,
                        [$"{parentProp.ContainingParent.Name.Name}::{parentProp.Name.Name}"], ErrorCode.Get(CTEN.PropertyShadowing));
                }
            }

            // check if all implemented
            if (!isInterface && !decl.SpecialKeys.Contains(TokenType.KwAbstract))
            {
                // if it is not an interface nor abstract class - all abstract shite has to be implemented
                foreach (var p in inheritedPropDecls)
                {
                    // skip overrided-abstract methods
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

            inheritedPropDecls.AddRange(currentPropDecls);
            return inheritedPropDecls;
        }
    }
}
