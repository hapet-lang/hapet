using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;
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

        private PreparationStep _currentPreparationStep { get; set; } = PreparationStep.None;

        private int PostPrepareMetadata()
        {
            AllPostPrepareMetadataTypes();
            AllPostPrepareMetadataGenerics();
            AllPostPrepareMetadataInheritance();
            AllPostPrepareMetadataDelegates();
            AllPostPrepareMetadataFunctions();
            AllPostPrepareMetadataInheritedFunctions();
            AllPostPrepareMetadataTypeFieldDecls();
            ФддPostPrepareMetadataTypeInheritedFieldDecls();
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

        private void AllPostPrepareMetadataTypes()
        {
            _currentPreparationStep = PreparationStep.Types;

            foreach (var (path, file) in _compiler.GetFiles())
            {
                _currentSourceFile = file;
                foreach (var stmt in file.Statements)
                {
                    PostPrepareMetadataTypes(stmt, true);
                }
            }
        }

        private void AllPostPrepareMetadataGenerics()
        {
            _currentPreparationStep = PreparationStep.Generics;

            // resolve inheritance shite of classes
            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataGenerics(cls);
            }
        }

        private void AllPostPrepareMetadataInheritance()
        {
            _currentPreparationStep = PreparationStep.Inheritance;

            // resolve inheritance shite of classes
            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataInheritance(cls);
            }
            // resolve inheritance shite of structs
            foreach (var str in AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;
                PostPrepareMetadataInheritance(str);
            }
            // resolve inheritance shite of enums
            foreach (var enm in AllEnumsMetadata)
            {
                _currentSourceFile = enm.SourceFile;
                PostPrepareMetadataInheritance(enm);
            }
        }

        private void AllPostPrepareMetadataDelegates()
        {
            _currentPreparationStep = PreparationStep.Delegates;

            // inferrencing delegates
            foreach (var del in AllDelegatesMetadata)
            {
                _currentSourceFile = del.SourceFile;
                PostPrepareMetadataDelegates(del);
            }
        }

        private void AllPostPrepareMetadataFunctions()
        {
            _currentPreparationStep = PreparationStep.Functions;

            // inferrencing funcs
            // WARN! _serializeClassesMetadata is used because we don't want external funcs to be inferred like that
            foreach (var cls in _serializeClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataFunctions(cls, true);
            }
            foreach (var str in _serializeStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;
                PostPrepareMetadataFunctions(str, true);
            }
        }

        private void AllPostPrepareMetadataInheritedFunctions()
        {
            _currentPreparationStep = PreparationStep.InheritedFunctions;

            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataInheritedFunctions(cls);
            }
            foreach (var str in AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;
                PostPrepareMetadataInheritedFunctions(str);
            }
        }

        /// <summary>
        /// We need to infer all decl at first and only then - their intializers
        /// </summary>
        private void AllPostPrepareMetadataTypeFieldDecls()
        {
            _currentPreparationStep = PreparationStep.FieldAndPropDecls;

            // resolve all fields of classes
            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataTypeFieldDecls(cls);
            }
            // resolve all fields of structs
            foreach (var str in AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;
                PostPrepareMetadataTypeFieldDecls(str);
            }
            foreach (var enm in AllEnumsMetadata)
            {
                _currentSourceFile = enm.SourceFile;
                PostPrepareMetadataTypeFieldDecls(enm);
            }
        }

        private void ФддPostPrepareMetadataTypeInheritedFieldDecls()
        {
            _currentPreparationStep = PreparationStep.InheritedFieldDecls;

            // resolve all inherited fields of classes
            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataTypeInheritedFieldDecls(cls);
            }
            foreach (var str in AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;
                PostPrepareMetadataTypeInheritedFieldDecls(str);
            }

            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataTypeInheritedFieldDeclsCopy(cls);
            }
            foreach (var str in AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;
                PostPrepareMetadataTypeInheritedFieldDeclsCopy(str);
            }
        }

        private void PostPrepareMetadataTypeInheritedPropsDecls()
        {
            _currentPreparationStep = PreparationStep.InheritedPropDecls;

            // resolve all inherited props of classes
            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataTypeInheritedPropsDecls(cls);
            }
            foreach (var str in AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;
                PostPrepareMetadataTypeInheritedPropsDecls(str);
            }

            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                PostPrepareMetadataTypeInheritedPropsDeclsCopy(cls);
            }
            foreach (var str in AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;
                PostPrepareMetadataTypeInheritedPropsDeclsCopy(str);
            }
        }

        private void PostPrepareMetadataTypeFieldInits()
        {
            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            // resolve all fields of classes
            foreach (var cls in AllClassesMetadata)
            {
                _currentSourceFile = cls.SourceFile;
                _currentClass = cls;
                // infer fields and props at first
                foreach (var decl in cls.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
                {
                    // this kostyl is done to skip double error on uninferred type
                    var savedIsPropF = decl.IsPropertyField;
                    decl.IsPropertyField = true;

                    // field or property
                    inInfo.AllowSpecialKeys = true;
                    PostPrepareVarInference(decl, inInfo, ref outInfo);
                    inInfo.AllowSpecialKeys = false;

                    decl.IsPropertyField = savedIsPropF;
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
                    PostPrepareVarInference(decl, inInfo, ref outInfo);
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
                    PostPrepareVarInference(decl, inInfo, ref outInfo);
                    // this shite is to generate values for enum fields
                    if (decl.Initializer == null)
                    {
                        decl.Initializer = PostPrepareExpressionWithType(GetPreparedAst(decl.Type.OutType, decl.Type), new AstNumberExpr(NumberData.FromInt(currentValue)));
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

        private void PostPrepareMetadataAttributes()
        {
            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            // inferrencing attribtues of functions
            foreach (var fnc in AllFunctionsMetadata)
            {
                _currentSourceFile = fnc.SourceFile;
                // inferencing attrs
                foreach (var a in fnc.Attributes)
                {
                    PostPrepareExprInference(a, inInfo, ref outInfo);
                }
                // inferencing params attrs
                foreach (var p in fnc.Parameters)
                {
                    // inferencing attrs
                    foreach (var a in p.Attributes)
                    {
                        PostPrepareExprInference(a, inInfo, ref outInfo);
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
                        PostPrepareExprInference(a, inInfo, ref outInfo);
                    }
                }
                // inferencing attrs
                foreach (var a in cls.Attributes)
                {
                    PostPrepareExprInference(a, inInfo, ref outInfo);
                }
            }
            // inferrencing attribtues of structs
            foreach (var str in AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;
                // inferencing attrs
                foreach (var a in str.Attributes)
                {
                    PostPrepareExprInference(a, inInfo, ref outInfo);
                }
            }
            // inferrencing attribtues of enums
            foreach (var enm in AllEnumsMetadata)
            {
                _currentSourceFile = enm.SourceFile;
                // inferencing attrs
                foreach (var a in enm.Attributes)
                {
                    PostPrepareExprInference(a, inInfo, ref outInfo);
                }
            }
            // inferrencing attribtues of delegates
            foreach (var del in AllDelegatesMetadata)
            {
                _currentSourceFile = del.SourceFile;
                // inferencing attrs
                foreach (var a in del.Attributes)
                {
                    PostPrepareExprInference(a, inInfo, ref outInfo);
                }
                // inferencing params attrs
                foreach (var p in del.Parameters)
                {
                    // inferencing attrs
                    foreach (var a in p.Attributes)
                    {
                        PostPrepareExprInference(a, inInfo, ref outInfo);
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
