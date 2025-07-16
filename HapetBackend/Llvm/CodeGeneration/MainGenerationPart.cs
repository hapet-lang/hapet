using System;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetLastPrepare;
using HapetPostPrepare;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private ProgramFile _currentSourceFile;
        private AstFuncDecl _currentFunction;

        private void GenerateCode()
        {
            var funcDecls = _postPreparer.AllFunctionsMetadata.ToList();
            var funcs = new Dictionary<AstFuncDecl, LLVMTypeRef>();

            foreach (var func in funcDecls)
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(func))
                    continue;

                if (IsFunctionShouldBeSkipped(func))
                    continue;

                _currentSourceFile = func.SourceFile;
                // defining global func
                var funcType = HapetTypeToLLVMType(func.Type.OutType);
                funcs.Add(func, funcType);
            }

            foreach (var (funcDecl, funcType) in funcs)
            {
                _currentSourceFile = funcDecl.SourceFile;
                GenerateFuncCode(funcDecl, funcType);
            }
        }

        private LLVMValueRef _lastFunctionValueRef;
        private unsafe void GenerateFuncCode(AstFuncDecl funcDecl, LLVMTypeRef? funcType = null, bool forMetadata = false)
        {
            _currentFunction = funcDecl;

            // which imported functions has to be generated with body
            var allowFunctionBodyToBeGenerated = funcDecl.IsImplOfGeneric || (funcDecl.ContainingParent?.IsImplOfGeneric ?? false) || 
                (funcDecl.IsNestedDecl && funcDecl.ParentDecl.IsImplOfGeneric) || 
                ((funcDecl.ContainingParent?.IsNestedDecl ?? false) && funcDecl.ContainingParent.ParentDecl.IsImplOfGeneric);

            funcType ??= HapetTypeToLLVMType(funcDecl.Type.OutType);            

            // if it is for metadata - only 'declares' would be generated that would be replaced with 'defines' in the future
            if (forMetadata)
            {
                string funcName = GenericsHelper.GetCodegenFunctionName(funcDecl, _messageHandler);

                // declaring global func
                LLVMValueRef lfunc = _module.AddFunction(funcName, funcType.Value);

                if (funcDecl.IsImported && !allowFunctionBodyToBeGenerated)
                {
                    // this is an imported function from another assembly
                    lfunc.Linkage = LLVMLinkage.LLVMExternalLinkage;
                    lfunc.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLImportStorageClass;

                    // WARN: do I need to define calling convention here like below?
                }
                else if (!funcDecl.SpecialKeys.Contains(TokenType.KwUnreflected))
                {
                    // make the function dllexport when it is not 'unreflected'
                    lfunc.Linkage = LLVMLinkage.LLVMExternalLinkage;
                    lfunc.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLExportStorageClass;

                    // for win-x86 callconv is that
                    if (_compiler.CurrentProjectSettings.TargetPlatformData.TargetPlatform == HapetFrontend.TargetPlatform.Win86)
                    {
                        lfunc.FunctionCallConv = (uint)LLVMCallConv.LLVMCCallConv; // cdecl
                    }
                }
                else
                {
                    // unreflected function
                    lfunc.Linkage = LLVMLinkage.LLVMInternalLinkage;
                }

                // check inline attr
                if (funcDecl.SpecialKeys.Contains(TokenType.KwInline))
                {
                    // 3 - is AlwaysInline
                    // https://github.com/dotnet/LLVMSharp/blob/fb8f621699da07ed3244f75142c3cb37a7f49d2f/sources/LLVMSharp/Attribute.cs#L32
                    var attr = LLVM.CreateEnumAttribute(_context, (uint)3, default);
                    LLVM.AddAttributeAtIndex(lfunc, LLVMAttributeIndex.LLVMAttributeFunctionIndex, attr);
                }

                // caching the function	
                _valueMap[funcDecl.Symbol] = lfunc;
                _lastFunctionValueRef = lfunc;

                // setting parameter names
                for (int i = 0; i < funcDecl.Parameters.Count; ++i)
                {
                    var p = funcDecl.Parameters[i];
                    if (p.Name != null)
                        lfunc.Params[i].Name = p.Name.Name;
                }
            }
            else
            {
                // skip imported funcs that are not generics
                if (funcDecl.IsImported && !allowFunctionBodyToBeGenerated)
                    return;

                // skip interface funcs
                if (funcDecl.ContainingParent is AstClassDecl clsD && clsD.IsInterface)
                    return;

                // getting the func
                LLVMValueRef lfunc = _valueMap[funcDecl.Symbol];
                _lastFunctionValueRef = lfunc;

                // check if there is no implementation and it is not an extern shite
                if (funcDecl.Body == null && !funcDecl.SpecialKeys.Contains(TokenType.KwExtern))
                    return;

                // params body
                var paramsBody = lfunc.AppendBasicBlockInContext(_context, "params");
                _builder.PositionAtEnd(paramsBody);
                // generating params allocs
                for (int i = 0; i < funcDecl.Parameters.Count; ++i)
                {
                    var p = funcDecl.Parameters[i];
                    // skip this shite
                    if (p.ParameterModificator == ParameterModificator.Arglist)
                        continue;

                    // if it is a ref or out or a class - need to make it as a ptr
                    var paramType = p.Type.OutType;
                    if (p.ParameterModificator == ParameterModificator.Ref ||
                        p.ParameterModificator == ParameterModificator.Out)
                        paramType = PointerType.GetPointerType(paramType);

                    var addrAlloca = _builder.BuildAlloca(HapetTypeToLLVMType(paramType), $"{p.Name.Name}.addr");
                    _builder.BuildStore(lfunc.GetParam((uint)i), addrAlloca);
                    _valueMap[p.Symbol] = addrAlloca;
                }

                // function body
                var bbBody = lfunc.AppendBasicBlockInContext(_context, "entry");
                _builder.BuildBr(bbBody);
                _builder.PositionAtEnd(bbBody);

                // different behaviour when extern func
                if (funcDecl.SpecialKeys.Contains(TokenType.KwExtern))
                {
                    // when extern func
                    GenerateExternFunctionBody(funcDecl);
                }
                else
                {
                    // genereting inside stuff of the function
                    GenerateBlockExprCode(funcDecl.Body);
                }

                if (!lfunc.VerifyFunction(LLVMVerifierFailureAction.LLVMReturnStatusAction))
                {
                    _messageHandler.ReportMessage([$"Failed to validate function '{lfunc.Name}'"], ErrorCode.Get(CTEN.LLVMValidateError), ReportType.Error);
                }
            }
        }

        private void GenerateVarDeclCode(AstVarDecl varDecl)
        {
            // alloca new var in basicBlock
            var varPtr = CreateLocalVariable(varDecl.Type.OutType, varDecl.Name.Name);

            // check for initializer and try to evaluate expr
            if (varDecl.Initializer != null)
            {
                AssignToVar(varPtr, varDecl.Initializer);
            }

            // _refMap[varDecl.GetSymbol] = varPtr;
            // _valueMap[varDecl.GetSymbol] = _builder.BuildLoad2(HapetTypeToLLVMType(varDecl.Type.OutType), varPtr, varDecl.Name.Name);
            _valueMap[varDecl.Symbol] = varPtr;
        }

        private void GenerateExternFunctionBody(AstFuncDecl funcDecl)
        {
            // creating extern call of C func
            string dllImportAttrFullName = "System.Runtime.InteropServices.DllImportAttribute"; // WARN: hard cock
            var dllImportAttr = funcDecl.Attributes.FirstOrDefault(x =>
            {
                // could be if an attr was not infered properly
                if (x.AttributeName.OutType == null)
                    return false;
                return HapetType.AsString(x.AttributeName.OutType) == dllImportAttrFullName;
            });

            // many checks are here
            if (dllImportAttr == null)
            {
                _messageHandler.ReportMessage(_currentSourceFile.Text, funcDecl, [], ErrorCode.Get(CTEN.ExternFuncNoAttr));
                return;
            }
            string dllName = dllImportAttr.Arguments[0].OutValue as string;
            string entryPoint = dllImportAttr.Arguments[1].OutValue as string;

            HapetType vaListType = HapetType.CurrentTypeContext.VaListType;
            HapetType ptrToVaListType = PointerType.GetPointerType(vaListType);
            LLVMTypeRef funcType;
            LLVMValueRef funcValue;
            LLVMValueRef apAlloca = default;
            LLVMValueRef apAllocaBitcasted = default;

            // check if there is a dll to be linked with!
            if (!string.IsNullOrWhiteSpace(dllName))
                _libsToBeLinked.Add(dllName);

            // the same type
            /// almost the same as in <see cref="HapetTypeToLLVMType"/>
            var f = funcDecl.Type.OutType as FunctionType;
            var paramTypes = f.Declaration.Parameters.Select(rt => 
                HapetTypeToLLVMType(rt.ParameterModificator == ParameterModificator.Arglist ? ptrToVaListType : rt.Type.OutType)).ToList();
            var returnType = HapetTypeToLLVMType(f.Declaration.Returns.OutType);
            funcType = LLVMTypeRef.CreateFunction(returnType, paramTypes.ToArray(), false);

            // declaring external global func
            funcValue = _module.AddFunction(entryPoint, funcType);
            funcValue.Linkage = LLVMLinkage.LLVMExternalLinkage;
            funcValue.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLImportStorageClass;

            // setting parameter names
            for (int i = 0; i < funcDecl.Parameters.Count; ++i)
            {
                var p = funcDecl.Parameters[i];
                if (p.Name != null)
                    funcValue.Params[i].Name = p.Name.Name;
            }

            // generating params
            List<LLVMValueRef> parameters = new List<LLVMValueRef>();
            for (int i = 0; i < funcDecl.Parameters.Count; ++i)
            {
                var p = funcDecl.Parameters[i];
                if (p.ParameterModificator == ParameterModificator.Arglist)
                {
                    // need to create va_list and va_start
                    apAlloca = _builder.BuildAlloca(HapetTypeToLLVMType(vaListType), $"va_list.ap.addr");
                    apAllocaBitcasted = _builder.BuildBitCast(apAlloca, _context.Int8Type.GetPointerTo(), "va_list.bitcasted");

                    // va start
                    var startFunc = GetVaStartFunc();
                    _builder.BuildCall2(startFunc.Item1, startFunc.Item2, new LLVMValueRef[] { apAllocaBitcasted });

                    var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(ptrToVaListType), apAllocaBitcasted, "va_list.ap.loaded");
                    parameters.Add(loaded);
                }
                else
                {
                    var vptr = _valueMap[p.Symbol];
                    var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(p.Type.OutType), vptr, p.Name.Name);
                    parameters.Add(loaded);
                }
            }

            // if there is smth to return
            if (funcDecl.Returns.OutType is VoidType)
            {
                _builder.BuildCall2(funcType, funcValue, parameters.ToArray());
                TryBuildVaEnd();
                _builder.BuildRetVoid();
            }
            else
            {
                var v = _builder.BuildCall2(funcType, funcValue, parameters.ToArray(), $"{entryPoint}Result");
                TryBuildVaEnd();
                _builder.BuildRet(v);
            }            

            void TryBuildVaEnd()
            {
                // check for va
                if (funcDecl.Parameters.Count == 0 || funcDecl.Parameters.Last().ParameterModificator != ParameterModificator.Arglist)
                    return;

                // va end
                var endFunc = GetVaStartFunc();
                _builder.BuildCall2(endFunc.Item1, endFunc.Item2, new LLVMValueRef[] { apAllocaBitcasted });
            }
        }

        private bool IsFunctionShouldBeSkipped(AstFuncDecl func) 
        {
            // special case for low level classes
            if (func.ContainingParent != null && (
                func.ContainingParent.Name.Name.StartsWith("System.Runtime.InteropServices.Marshal") ||
                func.ContainingParent.Name.Name.StartsWith("System.Runtime.Conversion") ||
                func.ContainingParent.Name.Name.StartsWith("System.Text.Native") ||
                func.ContainingParent.Name.Name.StartsWith("System.Object") ||
                func.ContainingParent.Name.Name.StartsWith("System.ValueType")))
                return false;

            // allow special funcs if the containing type is used
            if ((func.ClassFunctionType == ClassFunctionType.Ctor ||
                func.ClassFunctionType == ClassFunctionType.Dtor ||
                func.ClassFunctionType == ClassFunctionType.Initializer) &&
                GetNormalDeclIsUsed(func.ContainingParent))
                return false;

            // also we need to skip here stors of generic impls
            if (func.ContainingParent != null && func.ContainingParent.IsImplOfGeneric &&
                func.ClassFunctionType == ClassFunctionType.StaticCtor)
                return true;

            // skip generation of imported-not-used functions
            if (func.IsImported && !GetNormalDeclIsUsed(func) &&
                !func.SpecialKeys.Contains(TokenType.KwVirtual) &&
                !func.SpecialKeys.Contains(TokenType.KwAbstract) &&
                !func.SpecialKeys.Contains(TokenType.KwOverride))
                return true;
            return false;
        }

        private bool IsTypeShouldBeSkipped(AstDeclaration decl)
        {
            // special case for required types
            if (decl.Type.OutType == HapetType.CurrentTypeContext.GetArrayType(HapetType.CurrentTypeContext.ObjectTypeInstance))
                return false;

            // skip generation of imported-not-used decls
            if (decl.IsImported && !GetNormalDeclIsUsed(decl))
                return true;
            return false;
        }

        private bool GetNormalDeclIsUsed(AstDeclaration decl)
        {
            // is decl used or
            // decl is propa field/func and propa is used
            bool isUsed = decl.IsDeclarationUsed ||
                          _compiler.CurrentProjectSettings.TargetFormat == HapetFrontend.TargetFormat.Library || // all used for library
                          (decl is AstVarDecl vd1 && vd1.IsPropertyField && vd1.NormalParent is AstDeclaration pd1 && pd1.IsDeclarationUsed) ||
                          (decl is AstFuncDecl fd1 && fd1.IsPropertyFunction && fd1.NormalParent is AstDeclaration pd2 && pd2.IsDeclarationUsed);
            return isUsed;
        }
    }
}
