using HapetFrontend.Ast.Statements;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using LLVMSharp.Interop;
using System;
using System.Xml.Linq;

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

        private (LLVMTypeRef funcType, LLVMValueRef funcValue, LLVMTypeRef returnType) GetCheckedFunction(string op, int size, bool signed)
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
            // skip not int types
            skip = skip || builtInOp.LhsType is not IntType || builtInOp.RhsType is not IntType;
            // skip non arithmetic ops
            skip = skip || (builtInOp.Name != "+" && builtInOp.Name != "-" && builtInOp.Name != "*");
            if (skip)
            {
                result = default;
                return false;
            }
            var intType = builtInOp.LhsType as IntType;
            var fncData = GetCheckedFunction(builtInOp.Name, intType.GetSize(), intType.Signed);
            var binOpResult = _builder.BuildCall2(fncData.funcType, fncData.funcValue, new LLVMValueRef[] { left, right }, "checkedResult");

            var opResult = _builder.BuildExtractValue(binOpResult, 0, "res");
            var isOverflow = _builder.BuildExtractValue(binOpResult, 1, "isOverflow");

            BuildOverflowCheckBlocks(isOverflow);

            result = opResult;
            return true;
        }

        private void BuildOverflowCheckBlocks(LLVMValueRef checkValue)
        {
            // creating OF check blocks
            var bbOverflow = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"overflow");
            var bbEnd = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"overflow.end");

            _builder.BuildCondBr(checkValue, bbOverflow, bbEnd);
            // if overflow
            _builder.PositionAtEnd(bbOverflow);
            GenerateOverflowException();
            _builder.BuildUnreachable();

            _builder.PositionAtEnd(bbEnd);
        }

        private (LLVMValueRef, LLVMValueRef) GetMinMaxOfType(HapetType type)
        {
            switch (type)
            {
                case CharType:
                    return (HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetIntType(2, false), 0), 
                        HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetIntType(2, false), ushort.MaxValue));
                case FloatType ft:
                    return ft.GetSize() switch
                    {
                        4 => (HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetFloatType(4), float.MinValue),
                            HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetFloatType(4), float.MaxValue)),
                        8 => (HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetFloatType(8), double.MinValue),
                            HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetFloatType(8), double.MaxValue)),
                        _ => throw new NotImplementedException()
                    };
                case IntType it:
                    return it.GetSize() switch
                    {
                        1 => (HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetIntType(1, it.Signed), it.Signed ? sbyte.MinValue : byte.MinValue),
                            HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetIntType(1, it.Signed), it.Signed ? sbyte.MaxValue : byte.MaxValue)),
                        2 => (HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetIntType(2, it.Signed), it.Signed ? short.MinValue : ushort.MinValue),
                            HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetIntType(2, it.Signed), it.Signed ? short.MaxValue : ushort.MaxValue)),
                        4 => (HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetIntType(4, it.Signed), it.Signed ? int.MinValue : uint.MinValue),
                            HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetIntType(4, it.Signed), it.Signed ? int.MaxValue : uint.MaxValue)),
                        8 => (HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetIntType(8, it.Signed), it.Signed ? long.MinValue : ulong.MinValue),
                            HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetIntType(8, it.Signed), it.Signed ? long.MaxValue : ulong.MaxValue)),
                        _ => throw new NotImplementedException()
                    };
                default:
                    throw new NotImplementedException();
            }
        }

        private (LLVMValueRef, LLVMValueRef, LLVMValueRef) GetNanInfOfType(FloatType type)
        {
            return type.GetSize() switch
            {
                4 => (HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetFloatType(4), float.NaN),
                    HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetFloatType(4), float.NegativeInfinity),
                    HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetFloatType(4), float.PositiveInfinity)),
                8 => (HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetFloatType(8), double.NaN),
                    HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetFloatType(8), double.NegativeInfinity),
                    HapetValueToLLVMValue(HapetType.CurrentTypeContext.GetFloatType(8), double.PositiveInfinity)),
                _ => throw new NotImplementedException()
            };
        }
    }
}
