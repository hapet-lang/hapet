using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using LLVMSharp.Interop;
using System;
using System.Xml.Linq;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        /// <summary>
        /// Inits some dicts and other shite with metadata types :)
        /// </summary>
        private void GenerateMetadataShite()
        {
            GenerateMetadataShiteTypes();
            GenerateMetadataShiteFuncs();
            GenerateMetadataShiteFields();
            GenerateMetadataShiteAfterAll();
        }

        // TODO: mb refactor somehow?
        private void GenerateMetadataShiteTypes()
        {
            // all over the classes
            foreach (var cls in _postPreparer.AllClassesMetadata)
            {
                // skip generic (non-real) classes
                if (cls.HasGenericTypes)
                    continue;

                _currentSourceFile = cls.SourceFile;
                // doing that we are registering the type in dict
                var _ = HapetTypeToLLVMType(cls.Type.OutType);
            }
            // all over the structs
            foreach (var str in _postPreparer.AllStructsMetadata)
            {
                _currentSourceFile = str.SourceFile;
                // doing that we are registering the type in dict
                var _ = HapetTypeToLLVMType(str.Type.OutType);
            }
            // all over the enums
            foreach (var enm in _postPreparer.AllEnumsMetadata)
            {
                _currentSourceFile = enm.SourceFile;
                // doing that we are registering the type in dict
                var _ = HapetTypeToLLVMType(enm.Type.OutType);
            }
        }

        // TODO: mb refactor somehow?
        private void GenerateMetadataShiteFields()
        {
            foreach (var cls in _postPreparer.AllClassesMetadata)
            {
                // skip generic (non-real) classes
                if (cls.HasGenericTypes)
                    continue;

                _currentSourceFile = cls.SourceFile;

                var classStruct = HapetTypeToLLVMType(cls.Type.OutType);

                var entryTypes = new List<LLVMTypeRef>();
                var entryHapetTypes = new List<HapetType>();

                // add typeinfo field 
                entryTypes.Add(_context.Int8Type.GetPointerTo());
                entryHapetTypes.Add(PointerType.GetPointerType(IntPtrType.Instance));

                // getting all field except props
                foreach (var decl in cls.Declarations.Where(x => x is AstVarDecl && x is not AstPropertyDecl).Select(x => x as AstVarDecl))
                {
                    // check for const/static fields
                    if (decl.SpecialKeys.Contains(TokenType.KwStatic))
                    {
                        // creating a static field of the class
                        var globStatic = _module.AddGlobal(HapetTypeToLLVMType(decl.Type.OutType), $"{cls.Type.OutType}::{decl.Name.Name}");
                        if (decl.Initializer != null)
                        {
                            globStatic.Initializer = GenerateExpressionCode(decl.Initializer);
                        }
                        else
                        {
                            // set default value to it
                            globStatic.Initializer = GenerateExpressionCode(AstDefaultExpr.GetDefaultValueForType(decl.Type.OutType, null));
                        }
                        _valueMap[decl.GetSymbol] = globStatic;
                    }
                    else if (decl.SpecialKeys.Contains(TokenType.KwConst))
                    {
                        // creating a const field of the class
                        // TODO: consts should not create a variable in LLVM IR 
                        // just use their values where needed
                        var globConst = _module.AddGlobal(HapetTypeToLLVMType(decl.Type.OutType), $"{cls.Type.OutType}::{decl.Name.Name}");
                        globConst.Initializer = HapetValueToLLVMValue(decl.Type.OutType, decl.Initializer.OutValue);
                        _valueMap[decl.GetSymbol] = globConst;
                    }
                    else
                    {
                        // if it is non const/static - create a field in struct
                        entryTypes.Add(HapetTypeToLLVMType(decl.Type.OutType));
                        entryHapetTypes.Add(decl.Type.OutType);
                    }
                }

                _structTypeElementsMap.Add(cls.Type.OutType, entryHapetTypes);
                classStruct.StructSetBody(entryTypes.ToArray(), false);
            }
            foreach (var enm in _postPreparer.AllEnumsMetadata)
            {
                foreach (var decl in enm.Declarations)
                {
                    // creating a static field of the enum
                    var globStatic = _module.AddGlobal(HapetTypeToLLVMType(decl.Type.OutType), $"{enm.Type.OutType}::{decl.Name.Name}");
                    // decl.Initializer is checked in Metadata PP. could not be null
                    globStatic.Initializer = GenerateExpressionCode(decl.Initializer);
                    _valueMap[decl.GetSymbol] = globStatic;
                }
            }
            // TODO: structs and other shite
        }

        private void GenerateMetadataShiteFuncs()
        {
            foreach (var func in _postPreparer.AllFunctionsMetadata)
            {
                // skip generic (non-real) classes
                if (func.ContainingParent is AstClassDecl cls && cls.HasGenericTypes)
                    continue;

                // skip generic (non-real) funcs
                if (func.HasGenericTypes)
                    continue;

                _currentSourceFile = func.SourceFile;
                GenerateFuncCode(func, null, true);
            }
        }

        private void GenerateMetadataShiteAfterAll()
        {
            foreach (var cls in _postPreparer.AllClassesMetadata)
            {
                // skip generic (non-real) classes
                if (cls.HasGenericTypes)
                    continue;

                // reg type info if non static
                if (!cls.SpecialKeys.Contains(TokenType.KwStatic))
                {
                    GenerateTypeInfoConst(cls.Type.OutType);
                    GenerateVirtualTableConst(cls.Type.OutType);
                }
            }
            foreach (var str in _postPreparer.AllStructsMetadata)
            {
                // reg type info if non static
                if (!str.SpecialKeys.Contains(TokenType.KwStatic))
                {
                    GenerateTypeInfoConst(str.Type.OutType);
                    GenerateVirtualTableConst(str.Type.OutType);
                }
            }
        }
    }
}
