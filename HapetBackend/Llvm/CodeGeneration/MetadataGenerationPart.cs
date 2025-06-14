using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
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
        
        private void GenerateMetadataShiteTypes()
        {
            // all over the classes
            foreach (var cls in _postPreparer.AllClassesMetadata)
            {
                if (ShouldTheDeclBeSkippedFromCodeGen(cls))
                    continue;

                _currentSourceFile = cls.SourceFile;
                // doing that we are registering the type in dict
                var _ = HapetTypeToLLVMType(cls.Type.OutType);
            }
            // all over the structs
            foreach (var str in _postPreparer.AllStructsMetadata)
            {
                if (ShouldTheDeclBeSkippedFromCodeGen(str))
                    continue;

                _currentSourceFile = str.SourceFile;
                // doing that we are registering the type in dict
                var _ = HapetTypeToLLVMType(str.Type.OutType);
            }
            // all over the enums
            foreach (var enm in _postPreparer.AllEnumsMetadata)
            {
                if (ShouldTheDeclBeSkippedFromCodeGen(enm))
                    continue;

                _currentSourceFile = enm.SourceFile;
                // doing that we are registering the type in dict
                var _ = HapetTypeToLLVMType(enm.Type.OutType);
            }
        }

        private void GenerateMetadataShiteFields()
        {
            foreach (var cls in _postPreparer.AllClassesMetadata)
            {
                if (ShouldTheDeclBeSkippedFromCodeGen(cls))
                    continue;

                _currentSourceFile = cls.SourceFile;

                // if the decl is impl of gen - take only orig generic
                var declName = cls.Name;

                var classStruct = HapetTypeToLLVMType(cls.Type.OutType);

                var entryTypes = new List<LLVMTypeRef>();

                // add typeinfo field 
                entryTypes.Add(_context.Int8Type.GetPointerTo());

                // getting all field except props
                foreach (var decl in cls.Declarations.Where(x => x is AstVarDecl && x is not AstPropertyDecl).Select(x => x as AstVarDecl))
                {
                    // need to make a ptr to a class
                    var varType = decl.Type.OutType is ClassType ? PointerType.GetPointerType(decl.Type.OutType) : decl.Type.OutType;

                    // check for const/static fields
                    if (decl.SpecialKeys.Contains(TokenType.KwStatic))
                    {
                        // creating a static field of the class
                        var globStatic = _module.AddGlobal(HapetTypeToLLVMType(varType), $"{declName.Name}::{decl.Name.Name}");
                        if (decl.Initializer != null)
                        {
                            globStatic.Initializer = GenerateExpressionCode(decl.Initializer);
                        }
                        else
                        {
                            // set default value to it
                            globStatic.Initializer = GenerateExpressionCode(AstDefaultExpr.GetDefaultValueForType(varType, null, _compiler.MessageHandler));
                        }
                        _valueMap[decl.GetSymbol] = globStatic;
                    }
                    else if (decl.SpecialKeys.Contains(TokenType.KwConst))
                    {
                        // creating a const field of the class
                        // TODO: consts should not create a variable in LLVM IR 
                        // just use their values where needed
                        var globConst = _module.AddGlobal(HapetTypeToLLVMType(varType), $"{declName.Name}::{decl.Name.Name}");
                        globConst.Initializer = HapetValueToLLVMValue(varType, decl.Initializer.OutValue);
                        _valueMap[decl.GetSymbol] = globConst;
                    }
                    else
                    {
                        // if it is non const/static - create a field in struct
                        entryTypes.Add(HapetTypeToLLVMType(varType));
                    }
                }
                classStruct.StructSetBody(entryTypes.ToArray(), false);
            }
            foreach (var str in _postPreparer.AllStructsMetadata)
            {
                if (ShouldTheDeclBeSkippedFromCodeGen(str))
                    continue;

                // if the decl is impl of gen - take only orig generic
                var declName = str.Name;

                _currentSourceFile = str.SourceFile;

                // getting all field except props
                foreach (var decl in str.Declarations.Where(x => x is AstVarDecl && x is not AstPropertyDecl).Select(x => x as AstVarDecl))
                {
                    // check for const/static fields
                    if (decl.SpecialKeys.Contains(TokenType.KwStatic))
                    {
                        // creating a static field of the class
                        var globStatic = _module.AddGlobal(HapetTypeToLLVMType(decl.Type.OutType), $"{declName.Name}::{decl.Name.Name}");
                        if (decl.Initializer != null)
                        {
                            globStatic.Initializer = GenerateExpressionCode(decl.Initializer);
                        }
                        else
                        {
                            // set default value to it
                            globStatic.Initializer = GenerateExpressionCode(AstDefaultExpr.GetDefaultValueForType(decl.Type.OutType, null, _compiler.MessageHandler));
                        }
                        _valueMap[decl.GetSymbol] = globStatic;
                    }
                    else if (decl.SpecialKeys.Contains(TokenType.KwConst))
                    {
                        // creating a const field of the class
                        // TODO: consts should not create a variable in LLVM IR 
                        // just use their values where needed
                        var globConst = _module.AddGlobal(HapetTypeToLLVMType(decl.Type.OutType), $"{declName.Name}::{decl.Name.Name}");
                        globConst.Initializer = HapetValueToLLVMValue(decl.Type.OutType, decl.Initializer.OutValue);
                        _valueMap[decl.GetSymbol] = globConst;
                    }
                    else
                    {
                        /// handled inside <see cref="HapetTypeToLLVMType"/>
                    }
                }
            }
            foreach (var enm in _postPreparer.AllEnumsMetadata)
            {
                if (ShouldTheDeclBeSkippedFromCodeGen(enm))
                    continue;

                foreach (var decl in enm.Declarations)
                {
                    // creating a static field of the enum
                    var globStatic = _module.AddGlobal(HapetTypeToLLVMType(decl.Type.OutType), $"{enm.Type.OutType}::{decl.Name.Name}");
                    // decl.Initializer is checked in Metadata PP. could not be null
                    globStatic.Initializer = GenerateExpressionCode(decl.Initializer);
                    _valueMap[decl.GetSymbol] = globStatic;
                }
            }
        }

        private void GenerateMetadataShiteFuncs()
        {
            foreach (var func in _postPreparer.AllFunctionsMetadata)
            {
                if (ShouldTheDeclBeSkippedFromCodeGen(func))
                    continue;

                _currentSourceFile = func.SourceFile;
                GenerateFuncCode(func, null, true);
            }
        }

        private void GenerateMetadataShiteAfterAll()
        {
            foreach (var cls in _postPreparer.AllClassesMetadata)
            {
                if (ShouldTheDeclBeSkippedFromCodeGen(cls))
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
                if (ShouldTheDeclBeSkippedFromCodeGen(str))
                    continue;

                // reg type info if non static
                if (!str.SpecialKeys.Contains(TokenType.KwStatic))
                {
                    GenerateTypeInfoConst(str.Type.OutType);
                    GenerateVirtualTableConst(str.Type.OutType);
                }
            }
        }

        private bool ShouldTheDeclBeSkippedFromCodeGen(AstDeclaration decl)
        {
            // skip generic (non-real) parents
            if (decl.ContainingParent?.HasGenericTypes ?? false)
                return true;
            // skip generic (non-real) funcs
            if (decl.HasGenericTypes)
                return true;
            // also skip if parent has generic types
            if (decl.IsNestedDecl && decl.ParentDecl.HasGenericTypes)
                return true;
            // skip genericDecl parents
            if (decl.ContainingParent is AstGenericDecl)
                return true;
            // happens at least when 'decl' is a func in a normal struct and the struct
            // is nested into a generic class
            if (decl.ContainingParent != null && decl.ContainingParent.IsNestedDecl && 
                decl.ContainingParent.ParentDecl.HasGenericTypes)
                return true;
            return false;
        }
    }
}
