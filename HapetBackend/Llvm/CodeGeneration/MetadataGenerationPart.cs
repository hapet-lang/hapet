using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetLastPrepare;
using LLVMSharp.Interop;
using System;
using System.Xml.Linq;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private readonly List<(ISymbol, AstExpression)> _initializersMapList = new List<(ISymbol, AstExpression)>();

        /// <summary>
        /// Inits some dicts and other shite with metadata types :)
        /// </summary>
        private void GenerateMetadataShite()
        {
            GenerateMetadataShiteTypes();
            GenerateMetadataShiteFuncs();
            GenerateMetadataShiteFields();
            GenerateMetadataShiteAfterAll();
            GenerateMetadataShiteFieldInitializers();
        }
        
        private void GenerateMetadataShiteTypes()
        {
            // all over the classes
            foreach (var cls in _postPreparer.AllClassesMetadata)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(cls))
                    continue;

                _currentSourceFile = cls.SourceFile;
                // doing that we are registering the type in dict
                var _ = HapetTypeToLLVMType(cls.Type.OutType);
            }
            // all over the structs
            foreach (var str in _postPreparer.AllStructsMetadata)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(str))
                    continue;

                _currentSourceFile = str.SourceFile;
                // doing that we are registering the type in dict
                var _ = HapetTypeToLLVMType(str.Type.OutType);
            }
            // all over the enums
            foreach (var enm in _postPreparer.AllEnumsMetadata)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(enm))
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
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(cls))
                {
                    // getting all STATIC/CONST fields except props
                    foreach (var decl in cls.Declarations.Where(x => x is AstVarDecl && x is not AstPropertyDecl).Select(x => x as AstVarDecl))
                    {
                        // need to make a ptr to a class
                        var varType = decl.Type.OutType;

                        // check for const/static fields
                        if (decl.SpecialKeys.Contains(TokenType.KwStatic) || decl.SpecialKeys.Contains(TokenType.KwConst))
                        {
                            // creating a static field of the class
                            var globStatic = _module.AddGlobal(HapetTypeToLLVMType(varType), $"{cls.Name.Name}::{decl.Name.Name}");
                            _initializersMapList.Add((decl.GetSymbol, decl.Initializer));
                            _valueMap[decl.GetSymbol] = globStatic;
                        }
                    }
                    continue;
                }

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
                    var varType = decl.Type.OutType;

                    // check for const/static fields
                    if (decl.SpecialKeys.Contains(TokenType.KwStatic) || decl.SpecialKeys.Contains(TokenType.KwConst))
                    {
                        // TODO: consts should not create a variable in LLVM IR 
                        // just use their values where needed

                        // creating a static field of the class
                        var globStatic = _module.AddGlobal(HapetTypeToLLVMType(varType), $"{declName.Name}::{decl.Name.Name}");
                        _initializersMapList.Add((decl.GetSymbol, decl.Initializer));
                        _valueMap[decl.GetSymbol] = globStatic;
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
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(str))
                {
                    // getting all STATIC/CONST fields except props
                    foreach (var decl in str.Declarations.Where(x => x is AstVarDecl && x is not AstPropertyDecl).Select(x => x as AstVarDecl))
                    {
                        // need to make a ptr to a class
                        var varType = decl.Type.OutType;

                        // check for const/static fields
                        if (decl.SpecialKeys.Contains(TokenType.KwStatic) || decl.SpecialKeys.Contains(TokenType.KwConst))
                        {
                            // creating a static field of the class
                            var globStatic = _module.AddGlobal(HapetTypeToLLVMType(varType), $"{str.Name.Name}::{decl.Name.Name}");
                            _initializersMapList.Add((decl.GetSymbol, decl.Initializer));
                            _valueMap[decl.GetSymbol] = globStatic;
                        }
                    }
                    continue;
                }

                // if the decl is impl of gen - take only orig generic
                var declName = str.Name;

                _currentSourceFile = str.SourceFile;

                // getting all field except props
                foreach (var decl in str.Declarations.Where(x => x is AstVarDecl && x is not AstPropertyDecl).Select(x => x as AstVarDecl))
                {
                    // check for const/static fields
                    if (decl.SpecialKeys.Contains(TokenType.KwStatic) || decl.SpecialKeys.Contains(TokenType.KwConst))
                    {
                        // TODO: consts should not create a variable in LLVM IR 
                        // just use their values where needed

                        // creating a static field of the class
                        var globStatic = _module.AddGlobal(HapetTypeToLLVMType(decl.Type.OutType), $"{declName.Name}::{decl.Name.Name}");
                        _initializersMapList.Add((decl.GetSymbol, decl.Initializer));
                        _valueMap[decl.GetSymbol] = globStatic;
                    }
                    else
                    {
                        /// handled inside <see cref="HapetTypeToLLVMType"/>
                    }
                }
            }
            foreach (var enm in _postPreparer.AllEnumsMetadata)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(enm))
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
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(func))
                    continue;

                // also we need to skip here stors of generic impls
                if (func.ContainingParent != null && func.ContainingParent.IsImplOfGeneric && 
                    func.ClassFunctionType == ClassFunctionType.StaticCtor)
                    continue;

                _currentSourceFile = func.SourceFile;
                GenerateFuncCode(func, null, true);
            }
        }

        private void GenerateMetadataShiteAfterAll()
        {
            foreach (var cls in _postPreparer.AllClassesMetadata)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(cls))
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
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(str))
                    continue;

                // reg type info if non static
                if (!str.SpecialKeys.Contains(TokenType.KwStatic))
                {
                    GenerateTypeInfoConst(str.Type.OutType);
                    GenerateVirtualTableConst(str.Type.OutType);
                }
            }
        }

        private void GenerateMetadataShiteFieldInitializers()
        {
            // we do generate initializers of static/const shite here 
            // because typeInfo is only generated before us
            // and typeInfo was to generated at GenerateMetadataShiteFields() call
            // but we could require typeInfo because of some casts

            foreach (var ini in _initializersMapList)
            {
                var field = _valueMap[ini.Item1];
                var decl = (ini.Item1 as DeclSymbol).Decl;

                // special check for const
                if (decl.SpecialKeys.Contains(TokenType.KwConst))
                {
                    field.Initializer = HapetValueToLLVMValue(decl.Type.OutType, ini.Item2.OutValue);
                    continue;
                }
                // for static
                else
                {
                    // WARN: do not set value from Initializer - it would be set inside stor
                    // set default value to it
                    field.Initializer = GenerateExpressionCode(AstDefaultExpr.GetDefaultValueForType(decl.Type.OutType, null, _compiler.MessageHandler));
                }
            }
        }
    }
}
