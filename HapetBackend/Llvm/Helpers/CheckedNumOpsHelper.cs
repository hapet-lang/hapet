using HapetFrontend.Scoping;
using HapetFrontend.Types;
using LLVMSharp.Interop;
using System;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private List<(LLVMTypeRef funcType, LLVMValueRef funcValue, LLVMTypeRef returnType)> _checkedNumericIntrinsics = 
            new List<(LLVMTypeRef, LLVMValueRef, LLVMTypeRef)>();

        private void AddCheckedNumericOps()
        {
            // s add
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.sadd.with.overflow.i16", 2, true));
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.sadd.with.overflow.i32", 4, true));
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.sadd.with.overflow.i64", 8, true));
            // s sub
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.ssub.with.overflow.i16", 2, true));
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.ssub.with.overflow.i32", 4, true));
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.ssub.with.overflow.i64", 8, true));
            // s mul
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.smul.with.overflow.i16", 2, true));
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.smul.with.overflow.i32", 4, true));
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.smul.with.overflow.i64", 8, true));

            // u add
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.uadd.with.overflow.i16", 2, false));
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.uadd.with.overflow.i32", 4, false));
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.uadd.with.overflow.i64", 8, false));
            // u sub
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.usub.with.overflow.i16", 2, false));
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.usub.with.overflow.i32", 4, false));
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.usub.with.overflow.i64", 8, false));
            // u mul
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.umul.with.overflow.i16", 2, false));
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.umul.with.overflow.i32", 4, false));
            _checkedNumericIntrinsics.Add(CreateCheckedNumericFunc("llvm.umul.with.overflow.i64", 8, false));
        }

        private (LLVMTypeRef, LLVMValueRef, LLVMTypeRef) CreateCheckedNumericFunc(string name, int size, bool signed)
        {
            LLVMTypeRef funcType;
            LLVMValueRef lfunc;
            LLVMTypeRef returnType;

            returnType = _context.GetStructType([
                    HapetTypeToLLVMType(HapetType.CurrentTypeContext.GetIntType(size, signed)),
                    HapetTypeToLLVMType(HapetType.CurrentTypeContext.BoolTypeInstance)], false);
            funcType = LLVMTypeRef.CreateFunction(returnType,
                [HapetTypeToLLVMType(HapetType.CurrentTypeContext.GetIntType(size, signed)),
                HapetTypeToLLVMType(HapetType.CurrentTypeContext.GetIntType(size, signed))], false);
            lfunc = _module.AddFunction(name, funcType);
            lfunc.Linkage = LLVMLinkage.LLVMExternalLinkage;
            lfunc.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLImportStorageClass;
            return (funcType, lfunc, returnType);
        }

        private (LLVMTypeRef, LLVMValueRef, LLVMTypeRef) GetCheckedFunction(string op, int size, bool signed)
        {
            int pls = op switch
            {
                "+" => 0,
                "-" => 3,
                "*" => 6,
                _ => 0
            };
            switch (size)
            {
                case 2: return _checkedNumericIntrinsics[signed ? 0 + pls : 9 + pls];
                case 4: return _checkedNumericIntrinsics[signed ? 1 + pls : 10 + pls];
                case 8: return _checkedNumericIntrinsics[signed ? 2 + pls : 11 + pls];
            }
            throw new NotImplementedException($"Checked function for {op}, sized {size} and signed {signed} does not exist");
        }

        private bool HandleBinOpInCheckedContext(BuiltInBinaryOperator builtInOp, LLVMValueRef left, LLVMValueRef right, out LLVMValueRef result)
        {
            // no need to do shite when not in checked context and type are different
            bool skip = !_isInCheckedContext || builtInOp.LhsType != builtInOp.RhsType;
            if (skip)
            {
                result = default;
                return false;
            }



            result = default;
            return false;
        }
    }
}
