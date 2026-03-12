using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using HapetFrontend.Types;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private void GenerateInlinedExternFunction(AstFuncDecl funcDecl, LLVMTypeRef funcType)
        {
            string dllImportAttrFullName = "System.Runtime.InteropServices.DllImportAttribute"; // WARN: hard cock
            var dllImportAttr = funcDecl.GetAttribute(dllImportAttrFullName);
            // many checks are here
            // TODO: check in PP
            if (dllImportAttr == null)
            {
                string libImportAttrFullName = "System.Runtime.InteropServices.LibImportAttribute"; // WARN: hard cock
                var libImportAttr = funcDecl.GetAttribute(libImportAttrFullName);
                if (libImportAttr == null)
                {
                    _messageHandler.ReportMessage(_currentSourceFile, funcDecl, [], ErrorCode.Get(CTEN.ExternFuncNoAttr));
                    return;
                }
                else
                {
                    string libName = libImportAttr.Arguments[0].OutValue as string;
                    string entryPoint = libImportAttr.Arguments[1].OutValue as string;
                    bool isSupp = false;
                    if (libImportAttr.Arguments.Count > 2 && libImportAttr.Arguments[2].OutValue is bool b)
                        isSupp = b;

                    // check if there is a lib to be linked with!
                    if (!string.IsNullOrWhiteSpace(libName))
                        _libsToBeLinked.Add(libName);

                    // declaring global func
                    LLVMValueRef lfunc = _module.GetOrCreateFunction(entryPoint, funcType);
                    lfunc.Linkage = LLVMLinkage.LLVMExternalLinkage;
                    // check if suppress dllimport attr
                    if (!isSupp) lfunc.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLImportStorageClass;

                    // setting parameter names
                    for (int i = 0; i < funcDecl.Parameters.Count; ++i)
                    {
                        var p = funcDecl.Parameters[i];
                        if (p.Name != null)
                            lfunc.GetParams()[i].Name = p.Name.Name;
                    }
                    // caching the function	
                    _valueMap[funcDecl.Symbol] = lfunc;
                    _lastFunctionValueRef = lfunc;
                }
            }
            else
            {
                // TODO: error that inline could not be used with DllImport, only LibImport
                // ERROR IN PP!!!
                throw new NotImplementedException();
            }
        }

        private void GenerateExternFunctionBody(AstFuncDecl funcDecl)
        {
            // creating extern call of C func
            string dllImportAttrFullName = "System.Runtime.InteropServices.DllImportAttribute"; // WARN: hard cock
            var dllImportAttr = funcDecl.GetAttribute(dllImportAttrFullName);

            // many checks are here
            if (dllImportAttr == null)
            {
                string libImportAttrFullName = "System.Runtime.InteropServices.LibImportAttribute"; // WARN: hard cock
                var libImportAttr = funcDecl.GetAttribute(libImportAttrFullName);
                if (libImportAttr == null)
                {
                    _messageHandler.ReportMessage(_currentSourceFile, funcDecl, [], ErrorCode.Get(CTEN.ExternFuncNoAttr));
                    return;
                }
                else
                {
                    GenerateExternLibImportFunctionBody(funcDecl, libImportAttr);
                }
            }
            else
            {
                GenerateExternDllImportFunctionBody(funcDecl, dllImportAttr);
            }
        }

        private void GenerateExternDllImportFunctionBody(AstFuncDecl funcDecl, AstAttributeStmt importAttr)
        {
            string dllName = importAttr.Arguments[0].OutValue as string;
            string entryPoint = importAttr.Arguments[1].OutValue as string;

            LLVMValueRef apAllocaBitcasted = default;

            // build dll resolver call
            var funtionPtr = CallResolveSymbolFunction(
                HapetValueToLLVMValue(HapetType.CurrentTypeContext.StringTypeInstance, dllName),
                HapetValueToLLVMValue(HapetType.CurrentTypeContext.StringTypeInstance, entryPoint));

            // making external func type
            var funcType = GetFunctionTypeForExternalCall(funcDecl);

            // generating list of args for call
            var argsToCall = GetArgumentsForExternalCall(funcDecl, ref apAllocaBitcasted);

            // if there is smth to return
            if (funcDecl.Returns.OutType is VoidType)
            {
                _builder.BuildCall2(funcType, funtionPtr, argsToCall.ToArray());
                TryBuildVaEnd(funcDecl, apAllocaBitcasted);
                _builder.BuildRetVoid();
            }
            else
            {
                var v = _builder.BuildCall2(funcType, funtionPtr, argsToCall.ToArray(), $"{entryPoint}Result");
                TryBuildVaEnd(funcDecl, apAllocaBitcasted);
                _builder.BuildRet(v);
            }
        }

        private void GenerateExternLibImportFunctionBody(AstFuncDecl funcDecl, AstAttributeStmt importAttr)
        {
            string libName = importAttr.Arguments[0].OutValue as string;
            string entryPoint = importAttr.Arguments[1].OutValue as string;
            bool isSupp = false;
            if (importAttr.Arguments.Count > 2 && importAttr.Arguments[2].OutValue is bool b)
                isSupp = b;

            LLVMTypeRef funcType;
            LLVMValueRef funcValue;
            LLVMValueRef apAllocaBitcasted = default;

            // check if there is a dll to be linked with!
            if (!string.IsNullOrWhiteSpace(libName))
                _libsToBeLinked.Add(libName);

            // making external func type
            funcType = GetFunctionTypeForExternalCall(funcDecl);

            // declaring external global func
            funcValue = _module.AddFunction(entryPoint, funcType);
            funcValue.Linkage = LLVMLinkage.LLVMExternalLinkage;
            // check if suppress dllimport attr
            if (!isSupp) funcValue.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLImportStorageClass;

            // setting parameter names
            for (int i = 0; i < funcDecl.Parameters.Count; ++i)
            {
                var p = funcDecl.Parameters[i];
                if (p.Name != null)
                    funcValue.GetParams()[i].Name = p.Name.Name;
            }

            // generating list of args for call
            var argsToCall = GetArgumentsForExternalCall(funcDecl, ref apAllocaBitcasted);

            // if there is smth to return
            if (funcDecl.Returns.OutType is VoidType)
            {
                _builder.BuildCall2(funcType, funcValue, argsToCall.ToArray());
                TryBuildVaEnd(funcDecl, apAllocaBitcasted);
                _builder.BuildRetVoid();
            }
            else
            {
                var v = _builder.BuildCall2(funcType, funcValue, argsToCall.ToArray(), $"{entryPoint}Result");
                TryBuildVaEnd(funcDecl, apAllocaBitcasted);
                _builder.BuildRet(v);
            }
        }

        private LLVMTypeRef GetFunctionTypeForExternalCall(AstFuncDecl funcDecl)
        {
            HapetType vaListType = HapetType.CurrentTypeContext.VaListTypeInstance;
            HapetType ptrToVaListType = PointerType.GetPointerType(vaListType);

            // the same type
            /// almost the same as in <see cref="HapetTypeToLLVMType"/>
            var f = funcDecl.Type.OutType as FunctionType;
            var paramTypes = f.Declaration.Parameters.Select(rt =>
                HapetTypeToLLVMType(rt.ParameterModificator == ParameterModificator.Arglist ? ptrToVaListType : rt.Type.OutType)).ToList();
            var returnType = HapetTypeToLLVMType(f.Declaration.Returns.OutType);
            return LLVMTypeRef.CreateFunction(returnType, paramTypes.ToArray(), false);
        }

        private List<LLVMValueRef> GetArgumentsForExternalCall(AstFuncDecl funcDecl, ref LLVMValueRef apAllocaBitcasted)
        {
            HapetType vaListType = HapetType.CurrentTypeContext.VaListTypeInstance;
            HapetType ptrToVaListType = PointerType.GetPointerType(vaListType);

            // generating params
            List<LLVMValueRef> args = new List<LLVMValueRef>();
            for (int i = 0; i < funcDecl.Parameters.Count; ++i)
            {
                var p = funcDecl.Parameters[i];
                if (p.ParameterModificator == ParameterModificator.Arglist)
                {
                    // need to create va_list and va_start
                    var apAlloca = _builder.BuildAlloca(HapetTypeToLLVMType(vaListType), $"va_list.ap.addr");
                    apAllocaBitcasted = _builder.BuildBitCast(apAlloca, _context.Int8Type.GetPointerTo(), "va_list.bitcasted");

                    // va start
                    var startFunc = GetVaStartFunc();
                    _builder.BuildCall2(startFunc.Item1, startFunc.Item2, new LLVMValueRef[] { apAllocaBitcasted });

                    var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(ptrToVaListType), apAllocaBitcasted, "va_list.ap.loaded");
                    args.Add(loaded);
                }
                else
                {
                    var vptr = _valueMap[p.Symbol];
                    var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(p.Type.OutType), vptr, p.Name.Name);
                    args.Add(loaded);
                }
            }
            return args;
        }

        private void TryBuildVaEnd(AstFuncDecl funcDecl, LLVMValueRef apAllocaBitcasted)
        {
            // check for va
            if (funcDecl.Parameters.Count == 0 || funcDecl.Parameters.Last().ParameterModificator != ParameterModificator.Arglist)
                return;

            // va end
            var endFunc = GetVaEndFunc();
            _builder.BuildCall2(endFunc.Item1, endFunc.Item2, new LLVMValueRef[] { apAllocaBitcasted });
        }
    }
}
