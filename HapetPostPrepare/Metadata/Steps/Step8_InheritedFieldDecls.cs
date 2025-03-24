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
            if (stmt is AstClassDecl cls)
                cls.AllRawFields = GetPreparedFields__(cls);
            else if (stmt is AstStructDecl str)
                str.AllRawFields = GetPreparedFields__(str);
        }

        private void PostPrepareMetadataTypeInheritedFieldDeclsCopy(AstStatement stmt)
        {
            if (stmt is AstClassDecl cls)
                CopyInheritedFields__(cls, cls.AllRawFields);
            else if (stmt is AstStructDecl str)
                CopyInheritedFields__(str, str.AllRawFields);
        }

        private static void CopyInheritedFields__(AstDeclaration decl, List<AstVarDecl> preparedDecls)
        {
            List<AstDeclaration> currDecls;
            if (decl is AstClassDecl clsDecl)
                currDecls = clsDecl.Declarations;
            else if (decl is AstStructDecl strDecl)
                currDecls = strDecl.Declarations;
            else
                return;

            // remove all current fields
            foreach (var fieldDecl in currDecls.GetStructFields())
            {
                decl.SubScope.RemoveDeclSymbol(fieldDecl.Name.Name, fieldDecl);
                currDecls.Remove(fieldDecl);
            }

            List<AstDeclaration> toInsert = new List<AstDeclaration>();
            // all over the parent fields - copy them
            foreach (var fieldDecl in preparedDecls)
            {
                // change parent and scope
                var newVar = fieldDecl.GetCopyForAnotherType(decl);
                // define the symbol
                decl.SubScope.DefineDeclSymbol(newVar.Name.Name, newVar);

                toInsert.Add(newVar);
            }

            // insert them at the beginning
            currDecls.InsertRange(0, toInsert);
        }

        // to get all pure fields including inherited
        private List<AstVarDecl> GetPreparedFields__(AstDeclaration decl)
        {
            List<AstNestedExpr> inheritedFrom;
            bool isInterface = false;
            List<AstVarDecl> inheritedFieldDecls = new List<AstVarDecl>();
            List<AstVarDecl> currentFieldDecls;
            if (decl is AstClassDecl clsDecl)
            {
                currentFieldDecls = clsDecl.Declarations.GetStructFields().Select(x => x as AstVarDecl).ToList();
                inheritedFrom = clsDecl.InheritedFrom;
                isInterface = clsDecl.IsInterface;
            }
            else if (decl is AstStructDecl strDecl)
            {
                currentFieldDecls = strDecl.Declarations.GetStructFields().Select(x => x as AstVarDecl).ToList();
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
                        inheritedFieldDecls.AddRange(GetPreparedFields__(inhDecl));
                    }
                    else
                    {
                        // get all the fields of interface
                        var inhFields = GetPreparedFields__(inhDecl);
                        foreach (var inhF in inhFields)
                        {
                            // check if the interface is already implemented in parent classes
                            var definedInOneOfTheParents = inheritedFieldDecls.GetSameDeclByTypeAndName(inhF);
                            // if the field was already presented previously
                            if (definedInOneOfTheParents != null)
                            {
                                // check if the already defined field is by the interface
                                bool isInherited = definedInOneOfTheParents.ContainingParent.Type.OutType.IsInheritedFrom(inhF.ContainingParent.Type.OutType as ClassType, true);
                                // if inherited - this is a parent cls already implemented the field - no need to warn
                                if (isInherited)
                                {
                                    // need to check that we do not implement it also
                                    var currF = currentFieldDecls.FirstOrDefault(x => x.Name.Name == inhF.Name.Name);
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
                                // if we are not an interface, happens when:
                                // interface IAnime : object
                                if (!isInterface)
                                {
                                    // if the field was not presented previously
                                    // need to check that we do implement it
                                    var currF = currentFieldDecls.GetSameDeclByTypeAndName(inhF);
                                    if (currF == null)
                                    {
                                        // error - the field of the interface was not implemented
                                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, inh,
                                            [HapetType.AsString(decl.Type.OutType), inhF.Name.Name], ErrorCode.Get(CTEN.NoFieldImplementation));
                                    }
                                    else
                                    {
                                        // add it to the new dictionary
                                        currentFieldDecls.Remove(currF);
                                        inheritedFieldDecls.Add(currF);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    var parentFields = GetPreparedFields__(inhDecl);

                    // remove all parent fields when there is a 'new' in current
                    foreach (var f in currentFieldDecls.ToList())
                    {
                        var inhF = parentFields.FirstOrDefault(x => x.Name.Name == f.Name.Name);

                        // error if the current field is without 'new' and parent has the same named field
                        if (!f.SpecialKeys.Contains(TokenType.KwNew))
                        {
                            if (inhF != null)
                            {
                                // the field is implemented in parent class and current class
                                // we need to error
                                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, f,
                                    [HapetType.AsString(f.ContainingParent.Type.OutType)], ErrorCode.Get(CTEN.FieldAlreadyDefined));
                            }
                            continue; // skip
                        }

                        // check if there is a 'new' kw but no fields in parent
                        if (inhF == null)
                        {
                            // we need to error
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, 
                                f.SpecialKeys.GetType(TokenType.KwNew).Location,
                                [], ErrorCode.Get(CTEN.PureUnexpectedToken));
                            continue;
                        }

                        // shadowing :)
                    }

                    // just add parent fields if it is a class
                    inheritedFieldDecls.AddRange(parentFields);
                }
            }

            inheritedFieldDecls.AddRange(currentFieldDecls);
            return inheritedFieldDecls;
        }
    }
}
