using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using System;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareMetadataInheritedFunctions(AstStatement stmt)
        {
            if (stmt is AstClassDecl cls)
                cls.AllVirtualMethods = GetPreparedVirtualMethods__(cls);
            else if (stmt is AstStructDecl str)
                str.AllVirtualMethods = GetPreparedVirtualMethods__(str);
        }

        // to get all virtual methods including inherited
        private List<AstFuncDecl> GetPreparedVirtualMethods__(AstDeclaration decl)
        {
            List<AstFuncDecl> inheritedFuncDecls = new List<AstFuncDecl>();
            List<AstFuncDecl> currentClassMethods;
            List<AstNestedExpr> inheritedFrom;
            bool isInterface = false;
            if (decl is AstClassDecl clsDecl)
            {
                currentClassMethods = clsDecl.Declarations.Where(x => x is AstFuncDecl fnc && !(fnc.HasGenericTypes && !fnc.IsImplOfGeneric)).Select(x => x as AstFuncDecl).ToList();
                inheritedFrom = clsDecl.InheritedFrom;
                isInterface = clsDecl.IsInterface;
            }
            else if (decl is AstStructDecl strDecl)
            {
                currentClassMethods = strDecl.Declarations.Where(x => x is AstFuncDecl fnc && !(fnc.HasGenericTypes && !fnc.IsImplOfGeneric)).Select(x => x as AstFuncDecl).ToList();
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
                if (inh.OutType is not ClassType)
                    break;

                var inhDecl = (inh.OutType as ClassType).Declaration;
                // if the inh type is an interface
                if (inhDecl.IsInterface)
                {
                    // if we are also an interface - just add, no need to check implementations
                    if (isInterface)
                    {
                        // TODO: warn!!! check that method already exists in the list. it is possible probably
                        inheritedFuncDecls.AddRange(GetPreparedVirtualMethods__(inhDecl));
                    }
                    else
                    {
                        // get all the methods of interface
                        var inhMethods = GetPreparedVirtualMethods__(inhDecl);
                        foreach (var inhF in inhMethods)
                        {
                            // check if the interface is already implemented in parent classes
                            var definedInOneOfTheParents = inheritedFuncDecls.GetSameByNameAndTypes(inhF, out int _);
                            // if the field was already presented previously
                            if (definedInOneOfTheParents != null)
                            {
                                // check if the already defined method is by the interface
                                bool isInherited = definedInOneOfTheParents.ContainingParent.Type.OutType.IsInheritedFrom(inhF.ContainingParent.Type.OutType as ClassType, true);
                                // if inherited - this is a parent cls already implemented the method - no need to warn
                                if (isInherited)
                                {
                                    // need to check that we do not implement it also
                                    var currF = currentClassMethods.GetSameByNameAndTypes(inhF, out int _);
                                    if (currF != null && !currF.SpecialKeys.Contains(TokenType.KwNew))
                                    {
                                        // the method is implemented in parent class and current class
                                        // we need to error
                                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, currF,
                                            [HapetType.AsString(definedInOneOfTheParents.ContainingParent.Type.OutType)], ErrorCode.Get(CTEN.MethodAlreadyDefined));
                                        continue;
                                    }
                                    // else - everything is ok probably
                                }
                                else
                                {
                                    // TODO: not todo. but C# allows shite like this:
                                    /*
                                        public interface Anime322
                                        {
                                            void Test();
                                        }

                                        public class Animal
                                        {
                                            public void Test() { }
                                        }

                                        public class Cat : Animal, Anime322
                                        {
                                        }
                                     */
                                    // but we can't because of interface offset calcs. md could be fixed somehow?

                                    // we need to error
                                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, inh,
                                        [HapetType.AsString(definedInOneOfTheParents.ContainingParent.Type.OutType),
                                            HapetType.AsString(definedInOneOfTheParents.Type.OutType),
                                            HapetType.AsString(decl.Type.OutType),
                                            HapetType.AsString(inh.OutType)],
                                        ErrorCode.Get(CTEN.DoubleInterfaceCringeMeth));
                                    continue;
                                }
                            }
                            else
                            {
                                // if the method was not presented previously

                                // need to check that we do implement it
                                var currF = currentClassMethods.GetSameByNameAndTypes(inhF, out int _);
                                if (currF == null)
                                {
                                    // error - the method of the interface was not implemented
                                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, inh,
                                        [HapetType.AsString(decl.Type.OutType), inhF.Name.Name], ErrorCode.Get(CTEN.NoMethodImplementation));
                                }
                                else
                                {
                                    // add it to the new dictionary
                                    currentClassMethods.Remove(currF);
                                    inheritedFuncDecls.Add(currF);
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
                        // just add parent funcs if it is a class
                        inheritedFuncDecls.AddRange(GetPreparedVirtualMethods__(inhDecl));

                        // search for overrides in the current class 
                        // and replace parent methods with our
                        foreach (var fCurr in currentClassMethods.Where(x => x.SpecialKeys.Contains(TokenType.KwOverride)).ToArray())
                        {
                            // check for signatures
                            var overridedFnc = inheritedFuncDecls.GetSameByNameAndTypes(fCurr, out int fncIndex);
                            // TODO: error here? we go all over the override funcs and found no func to be overriden?
                            if (overridedFnc == null)
                                continue;
                            inheritedFuncDecls[fncIndex] = fCurr;
                            // we need to remove it so it won't mess with us
                            currentClassMethods.Remove(fCurr);
                        }
                    }
                }
            }

            // check for shadowing
            foreach (var currM in currentClassMethods)
            {
                // skip virtual shite
                if (currM.SpecialKeys.Contains(TokenType.KwOverride))
                    continue;
                var parentFnc = inheritedFuncDecls.GetSameByNameAndTypes(currM, out int _);
                if (parentFnc != null && !currM.SpecialKeys.Contains(TokenType.KwNew))
                {
                    // skip property functions - property would error by its own
                    if (parentFnc.IsPropertyFunction)
                        continue;

                    // error - function shadowing
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, currM.Name,
                        [HapetType.AsString(parentFnc.Type.OutType)], ErrorCode.Get(CTEN.FunctionShadowing));
                }
            }

            // check if all implemented
            if (!isInterface && !decl.SpecialKeys.Contains(TokenType.KwAbstract))
            {
                // if it is not an interface nor abstract class - all abstract shite has to be implemented
                foreach (var m in inheritedFuncDecls)
                {
                    // skip overrided-abstract methods
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
                inheritedFuncDecls.AddRange(currentClassMethods);
            else
                // add here only virtual shite
                inheritedFuncDecls.AddRange(currentClassMethods.Where(x => x.SpecialKeys.Contains(TokenType.KwAbstract) ||
                                                                           x.SpecialKeys.Contains(TokenType.KwVirtual)));
            return inheritedFuncDecls;
        }
    }
}
