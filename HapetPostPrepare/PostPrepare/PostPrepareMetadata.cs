using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using Newtonsoft.Json;
using System;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public List<AstClassDecl> AllClassesMetadata { get; } = new List<AstClassDecl>();
        public List<AstStructDecl> AllStructsMetadata { get; } = new List<AstStructDecl>();
        public List<AstEnumDecl> AllEnumsMetadata { get; } = new List<AstEnumDecl>();
        public List<AstDelegateDecl> AllDelegatesMetadata { get; } = new List<AstDelegateDecl>();
        public List<AstFuncDecl> AllFunctionsMetadata { get; } = new List<AstFuncDecl>();

        private List<AstClassDecl> _serializeClassesMetadata { get; } = new List<AstClassDecl>();
        private List<AstStructDecl> _serializeStructsMetadata { get; } = new List<AstStructDecl>();
        private List<AstEnumDecl> _serializeEnumsMetadata { get; } = new List<AstEnumDecl>();
        private List<AstDelegateDecl> _serializeDelegatesMetadata { get; } = new List<AstDelegateDecl>();
        private List<AstFuncDecl> _serializeFunctionsMetadata { get; } = new List<AstFuncDecl>();

        public static void PostPrepareAliases(string typeName, Scope scope, AstDeclaration decl)
        {
            // kostyl to create aliases :)
            if (typeName == "System.Object")
            {
                scope.DefineDeclSymbol("System.object", decl);
            }
            else if (typeName == "System.String")
            {
                decl.Type.OutType = StringType.GetInstance(decl as AstStructDecl);
                scope.DefineDeclSymbol("System.string", decl);
            }
            else if (typeName == "System.Array")
            {
                decl.Type.OutType = ArrayType.GetArrayType(PointerType.NullLiteralType, decl as AstStructDecl);
            }
        }

        private int PostPrepareMetadata()
        {
            PostPrepareMetadataTypes();
            PostPrepareMetadataInheritance();
            PostPrepareMetadataDelegates();
            PostPrepareMetadataFunctions();
            PostPrepareMetadataInheritedFunctions();
            PostPrepareMetadataTypeFieldDecls();
            PostPrepareMetadataTypeInheritedFieldDecls();
            PostPrepareMetadataTypeInheritedPropsDecls();
            PostPrepareMetadataTypeFieldInits();
            PostPrepareMetadataAttributes();

            // if there were errors while preparing for metafile
            if (_compiler.MessageHandler.HasErrors)
            {
                return (int)CompilerErrors.PostPrepareMetafileError; // post prepare errors
            }

            // creating the file
            PostPrepareMetadataCreate();

            // WARN: removing all properties after saving to file
            // removing them only now because we need them to be presented in metadata
            /// unwrapping props is done in <see cref="PostPrepareClassProperties"/>
            RemoveAllProperties();

            return 0;
        }

        private void PostPrepareMetadataTypes()
        {
            foreach (var (path, file) in _compiler.GetFiles())
            {
                _currentSourceFile = file;
                foreach (var stmt in file.Statements)
                {
                    // just skip allowed statements
                    if (stmt is AstUsingStmt)
                    {
                        continue;
                    }

                    if (stmt is not AstDeclaration decl)
                    {
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, stmt, [], ErrorCode.Get(CTEN.StmtExpectedToBeDecl));
                        continue;
                    }

                    string newName;
                    if (decl is AstClassDecl classDecl)
                    {
                        _currentClass = classDecl;

                        // creating a new class name with namespace
                        newName = $"{file.Namespace}.{classDecl.Name.Name}";
                        AllClassesMetadata.Add(classDecl);
                        _serializeClassesMetadata.Add(classDecl);
                    }
                    else if (decl is AstStructDecl structDecl)
                    {
                        // creating a new struct name with namespace
                        newName = $"{file.Namespace}.{structDecl.Name.Name}";
                        AllStructsMetadata.Add(structDecl);
                        _serializeStructsMetadata.Add(structDecl);
                    }
                    else if (decl is AstEnumDecl enumDecl)
                    {
                        // creating a new enum name with namespace
                        newName = $"{file.Namespace}.{enumDecl.Name.Name}";
                        AllEnumsMetadata.Add(enumDecl);
                        _serializeEnumsMetadata.Add(enumDecl);
                    }
                    else if (decl is AstDelegateDecl delegateDecl)
                    {
                        // creating a new delegate name with namespace
                        newName = $"{file.Namespace}.{delegateDecl.Name.Name}";
                        AllDelegatesMetadata.Add(delegateDecl);
                        _serializeDelegatesMetadata.Add(delegateDecl);
                    }
                    else
                    {
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name, [], ErrorCode.Get(CTEN.DeclNotAllowedInNamespace));
                        continue;
                    }

                    // TODO: check for partial :)
                    decl.Name = decl.Name.GetCopy(newName);
                    var smbl = file.NamespaceScope.GetSymbol(decl.Name.Name);
                    // TODO: better error like where is the first decl?
                    if (smbl != null)
                    {
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name, [file.Namespace], ErrorCode.Get(CTEN.NamespaceAlreadyContains));
                    }
                    else
                    {
                        file.NamespaceScope.DefineDeclSymbol(decl.Name.Name, decl);

                        PostPrepareAliases(newName, file.NamespaceScope, decl);
                    }
                }
            }
        }

        private void PostPrepareMetadataInheritance()
        {
            // resolve inheritance shite of classes
            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                foreach (var inh in cls.InheritedFrom)
                {
                    PostPrepareExprInference(inh);

                    if (inh.OutType is not ClassType)
                    {
                        // error - cannot inherit from non class types
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, inh, [HapetType.AsString(cls.Type.OutType), HapetType.AsString(inh.OutType)], ErrorCode.Get(CTEN.CannotDeriveFromStruct));
                        continue;
                    }

                    // check for sealed type
                    if ((inh.OutType as ClassType).Declaration.SpecialKeys.Contains(TokenType.KwSealed))
                    {
                        // error - cannot inherit from sealed
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, inh, [], ErrorCode.Get(CTEN.DerivedFromSealed));
                    }
                }

                // set System.Object inheritance if there is nothing
                if ((cls.InheritedFrom.Count <= 0 || 
                    (cls.InheritedFrom[0].OutType is ClassType && 
                    (cls.InheritedFrom[0].OutType as ClassType).Declaration.IsInterface)) &&
                    cls.Name.Name != "System.Object") // skip itself
                {
                    // set it only if there are not inheritances or only interfaces
                    var nst = new AstNestedExpr(new AstIdExpr("System.Object", cls), null, cls);
                    cls.InheritedFrom.Insert(0, nst);
                    SetScopeAndParent(nst, cls);
                    PostPrepareExprScoping(nst);
                    PostPrepareExprInference(nst);
                }
            }
            // resolve inheritance shite of structs
            foreach (var str in AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;
                foreach (var inh in str.InheritedFrom)
                {
                    PostPrepareExprInference(inh);

                    if (inh.OutType is not ClassType || !(inh.OutType as ClassType).Declaration.IsInterface)
                    {
                        // error - cannot inherit from non interfaces
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, inh, [HapetType.AsString(inh.OutType)], ErrorCode.Get(CTEN.NonInterfaceInhInStruct));
                        continue;
                    }

                    // check for sealed type
                    if ((inh.OutType as ClassType).Declaration.SpecialKeys.Contains(TokenType.KwSealed))
                    {
                        // error - cannot inherit from sealed
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, inh, [], ErrorCode.Get(CTEN.DerivedFromSealed));
                    }
                }

                // set System.Object inheritance if there is nothing
                if ((str.InheritedFrom.Count <= 0 ||
                    (str.InheritedFrom[0].OutType is ClassType && 
                    (str.InheritedFrom[0].OutType as ClassType).Declaration.IsInterface)))
                {
                    // set it only if there are not inheritances or only interfaces
                    var nst = new AstNestedExpr(new AstIdExpr("System.Object", str), null, str);
                    str.InheritedFrom.Insert(0, nst);
                    SetScopeAndParent(nst, str);
                    PostPrepareExprScoping(nst);
                    PostPrepareExprInference(nst);
                }
            }
            // resolve inheritance shite of enums
            foreach (var enm in AllEnumsMetadata)
            {
                _currentSourceFile = enm.SourceFile;
                if (enm.InheritedType == null)
                    continue;
                PostPrepareExprInference(enm.InheritedType);
                // only int type inheritance allowed for enums
                if (enm.InheritedType.OutType is not IntType)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, enm.InheritedType, [], ErrorCode.Get(CTEN.EnumTypeNotInt));
                }
            }
        }

        /// <summary>
        /// We need to infer all decl at first and only then - their intializers
        /// </summary>
        private void PostPrepareMetadataTypeFieldDecls()
        {
            void InternalVarPP(AstVarDecl decl)
            {
                PostPrepareExprInference(decl.Type);

                if (decl.Type.OutType is ClassType)
                {
                    // the var is actually a pointer to the class
                    var astPtr = new AstPointerExpr(decl.Type, false, decl.Type.Location);
                    astPtr.Scope = decl.Type.Scope;
                    decl.Type = astPtr;
                    PostPrepareExprInference(decl.Type);
                }
            }

            // resolve all fields of classes
            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                // infer fields and props at first
                foreach (var decl in cls.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
                {
                    // field or property
                    InternalVarPP(decl);
                }
            }
            // resolve all fields of structs
            foreach (var str in AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;
                // infer fields at first
                foreach (var decl in str.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
                {
                    // field 
                    InternalVarPP(decl);
                }
            }
            foreach (var enm in AllEnumsMetadata)
            {
                _currentSourceFile = enm.SourceFile;
                // infer fields at first
                foreach (var decl in enm.Declarations)
                {
                    // field 
                    InternalVarPP(decl);
                }
            }
        }

        private void PostPrepareMetadataTypeInheritedFieldDecls()
        {
            void CopyInheritedFields(AstDeclaration decl, List<AstVarDecl> preparedDecls)
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
            List<AstVarDecl> GetPreparedFields(AstDeclaration decl)
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
                            inheritedFieldDecls.AddRange(GetPreparedFields(inhDecl));
                        }
                        else
                        {
                            // get all the fields of interface
                            var inhFields = GetPreparedFields(inhDecl);
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
                                        var currF = currentFieldDecls.GetSameDeclByTypeAndName(inhF);
                                        if (currF != null)
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
                        var parentFields = GetPreparedFields(inhDecl);
                        // just add parent fields if it is a class
                        inheritedFieldDecls.AddRange(parentFields);
                    }
                }

                inheritedFieldDecls.AddRange(currentFieldDecls);
                return inheritedFieldDecls;
            }

            // resolve all inherited fields of classes
            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;

                cls.AllRawFields = GetPreparedFields(cls);
            }
            foreach (var str in AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;

                str.AllRawFields = GetPreparedFields(str);
            }

            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;

                CopyInheritedFields(cls, cls.AllRawFields);
            }
            foreach (var str in AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;

                CopyInheritedFields(str, str.AllRawFields);
            }
        }

        private void PostPrepareMetadataTypeInheritedPropsDecls()
        {
            void CopyInheritedProps(AstDeclaration decl, List<AstPropertyDecl> preparedDecls)
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
            List<AstPropertyDecl> GetPreparedProps(AstDeclaration decl)
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
                            inheritedPropDecls.AddRange(GetPreparedProps(inhDecl));
                        }
                        else
                        {
                            // get all the props of interface
                            var inhFields = GetPreparedProps(inhDecl);
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
                            var parentProps = GetPreparedProps(inhDecl);
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

            // resolve all inherited props of classes
            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;

                cls.AllRawProps = GetPreparedProps(cls);
            }
            foreach (var str in AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;

                str.AllRawProps = GetPreparedProps(str);
            }

            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;

                CopyInheritedProps(cls, cls.AllRawProps);
            }
            foreach (var str in AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;

                CopyInheritedProps(str, str.AllRawProps);
            }
        }

        private void PostPrepareMetadataTypeFieldInits()
        {
            // resolve all fields of classes
            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                // infer fields and props at first
                foreach (var decl in cls.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
                {
                    // field or property
                    PostPrepareVarInference(decl, true);
                }
            }
            // resolve all fields of structs
            foreach (var str in AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;
                // infer fields at first
                foreach (var decl in str.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
                {
                    // field 
                    PostPrepareVarInference(decl);
                }
            }
            foreach (var enm in AllEnumsMetadata)
            {
                _currentSourceFile = enm.SourceFile;
                // generating all the values of fields
                int currentValue = 0;
                List<int> allValues = new List<int>(enm.Declarations.Count);

                // infer fields at first
                foreach (var decl in enm.Declarations)
                {
                    // field 
                    PostPrepareVarInference(decl);
                    // this shite is to generate values for enum fields
                    if (decl.Initializer == null)
                    {
                        decl.Initializer = PostPrepareExpressionWithType(decl.Type.OutType, new AstNumberExpr(NumberData.FromInt(currentValue)));
                        // warn if the value already exists in enum
                        if (allValues.Contains(currentValue))
                        {
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl, [], ErrorCode.Get(CTWN.EnumHasSameValue), null, ReportType.Warning);
                        }
                        allValues.Add(currentValue);
                        currentValue++;
                    }
                    else
                    {
                        if (decl.Initializer.OutValue == null)
                        {
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Initializer, [], ErrorCode.Get(CTEN.EnumIniNotComptime));
                            continue;
                        }
                        else if (decl.Initializer.OutValue is not NumberData)
                        {
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Initializer, [], ErrorCode.Get(CTEN.EnumIniNotNumber));
                            continue;
                        }
                        var userDefinedValue = (int)((NumberData)decl.Initializer.OutValue).IntValue;
                        // warn if the value already exists in enum
                        if (allValues.Contains(userDefinedValue))
                        {
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl, [], ErrorCode.Get(CTWN.EnumHasSameValue), null, ReportType.Warning);
                        }
                        allValues.Add(userDefinedValue);
                        currentValue = userDefinedValue + 1; // getting value for the next field
                    }
                }
            }
        }

        private void PostPrepareMetadataDelegates()
        {
            // inferrencing delegates
            foreach (var del in AllDelegatesMetadata)
            {
                _currentSourceFile = del.SourceFile;
                PostPrepareDelegateInference(del);
            }
        }

        private void PostPrepareMetadataFunctions()
        {
            // inferrencing funcs
            // WARN! _serializeClassesMetadata is used because we don't want external funcs to be inferred like that
            foreach (var cls in _serializeClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                foreach (var decl in cls.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl))
                {
                    PostPrepareFunctionInference(decl, true);
                    AllFunctionsMetadata.Add(decl);
                    _serializeFunctionsMetadata.Add(decl);
                }
            }
            foreach (var str in _serializeStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;
                foreach (var decl in str.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl))
                {
                    PostPrepareFunctionInference(decl, true);
                    AllFunctionsMetadata.Add(decl);
                    _serializeFunctionsMetadata.Add(decl);
                }
            }
        }

        private void PostPrepareMetadataInheritedFunctions()
        {
            // to get all virtual methods including inherited
            List<AstFuncDecl> GetPreparedVirtualMethods(AstDeclaration decl)
            {
                List<AstFuncDecl> inheritedFuncDecls = new List<AstFuncDecl>();
                List<AstFuncDecl> currentClassMethods;
                List<AstNestedExpr> inheritedFrom;
                bool isInterface = false;
                if (decl is AstClassDecl clsDecl)
                {
                    currentClassMethods = clsDecl.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl).ToList();
                    inheritedFrom = clsDecl.InheritedFrom;
                    isInterface = clsDecl.IsInterface;
                }
                else if (decl is AstStructDecl strDecl)
                {
                    currentClassMethods = strDecl.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl).ToList();
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
                            inheritedFuncDecls.AddRange(GetPreparedVirtualMethods(inhDecl));
                        }
                        else
                        {
                            // get all the methods of interface
                            var inhMethods = GetPreparedVirtualMethods(inhDecl);
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
                                        if (currF != null)
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
                            inheritedFuncDecls.AddRange(GetPreparedVirtualMethods(inhDecl));

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
                    if (parentFnc != null)
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

            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;

                cls.AllVirtualMethods = GetPreparedVirtualMethods(cls);
            }
            foreach (var str in AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;

                str.AllVirtualMethods = GetPreparedVirtualMethods(str);
            }
        }

        private void PostPrepareMetadataAttributes()
        {
            // inferrencing attribtues of functions
            foreach (var fnc in AllFunctionsMetadata)
            {
                _currentSourceFile = fnc.SourceFile;
                // inferencing attrs
                foreach (var a in fnc.Attributes)
                {
                    PostPrepareExprInference(a);
                }
                // inferencing params attrs
                foreach (var p in fnc.Parameters)
                {
                    // inferencing attrs
                    foreach (var a in p.Attributes)
                    {
                        PostPrepareExprInference(a);
                    }
                }
            }
            // inferrencing attribtues of classes
            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                // infer fields and props attibutes
                foreach (var decl in cls.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
                {
                    // inferencing attrs
                    foreach (var a in decl.Attributes)
                    {
                        PostPrepareExprInference(a);
                    }
                }
                // inferencing attrs
                foreach (var a in cls.Attributes)
                {
                    PostPrepareExprInference(a);
                }
            }
            // inferrencing attribtues of structs
            foreach (var str in AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;
                // inferencing attrs
                foreach (var a in str.Attributes)
                {
                    PostPrepareExprInference(a);
                }
            }
            // inferrencing attribtues of enums
            foreach (var enm in AllEnumsMetadata)
            {
                _currentSourceFile = enm.SourceFile;
                // inferencing attrs
                foreach (var a in enm.Attributes)
                {
                    PostPrepareExprInference(a);
                }
            }
            // inferrencing attribtues of delegates
            foreach (var del in AllDelegatesMetadata)
            {
                _currentSourceFile = del.SourceFile;
                // inferencing attrs
                foreach (var a in del.Attributes)
                {
                    PostPrepareExprInference(a);
                }
                // inferencing params attrs
                foreach (var p in del.Parameters)
                {
                    // inferencing attrs
                    foreach (var a in p.Attributes)
                    {
                        PostPrepareExprInference(a);
                    }
                }
            }
        }

        private void PostPrepareMetadataCreate()
        {
            var projectVersion = _compiler.CurrentProjectSettings.ProjectVersion;

            MetadataJson metadata = new MetadataJson();
            metadata.Version = projectVersion;
            // serialize all unreflected
            metadata.ClassDecls = _serializeClassesMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();
            metadata.StructDecls = _serializeStructsMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();
            metadata.EnumDecls = _serializeEnumsMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();
            metadata.DelegateDecls = _serializeDelegatesMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();
            metadata.FuncDecls = _serializeFunctionsMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();

            // WARN: take care about the shite that is goin on here
            var sz = JsonConvert.SerializeObject(metadata, Formatting.Indented);
            var outFolderPath = _compiler.CurrentProjectSettings.OutputDirectory;
            var projectName = _compiler.CurrentProjectSettings.ProjectName;
            File.WriteAllText($"{outFolderPath}/{projectName}.json", sz);
        }

        private void RemoveAllProperties()
        {
            foreach (var cls in AllClassesMetadata)
            {
                cls.Declarations.RemoveAll(x => x is AstPropertyDecl);
            }
            foreach (var cls in AllStructsMetadata)
            {
                cls.Declarations.RemoveAll(x => x is AstPropertyDecl);
            }
        }
    }

    public class MetadataJson
    {
        public string Version { get; set; }
        public List<ClassDeclJson> ClassDecls { get; set; }
        public List<StructDeclJson> StructDecls { get; set; }
        public List<EnumDeclJson> EnumDecls { get; set; }
        public List<DelegateDeclJson> DelegateDecls { get; set; }
        public List<FuncDeclJson> FuncDecls { get; set; }
    }
}
