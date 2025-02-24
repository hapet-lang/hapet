using HapetFrontend.Errors;
using HapetFrontend.Types;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private Dictionary<(string, HapetType, HapetType), Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef>> builtInBinOperators;
        private Dictionary<(string, HapetType), Func<LLVMBuilderRef, LLVMValueRef, string, LLVMValueRef>> builtInUnOperators;

        private void InitOperators()
        {
            InitBinaryOperators();
            InitUnaryOperators();
        }

        private Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef> GetBinOp(string op, HapetType l, HapetType r)
        {
            // this is a kostyl to search for any ptr bin ops
            var searchL = l is PointerType ? PointerType.VoidLiteralType : l;
            var searchR = r is PointerType ? PointerType.VoidLiteralType : r;
            return builtInBinOperators[(op, searchL, searchR)];
        }

        private Func<LLVMBuilderRef, LLVMValueRef, string, LLVMValueRef> GetUnOp(string op, HapetType t)
        {
            // this is a kostyl to search for any ptr bin ops
            var searchT = t is PointerType ? PointerType.VoidLiteralType : t;
            return builtInUnOperators[(op, searchT)];
        }

        private static unsafe Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef> GetICompare(LLVMIntPredicate pred)
        {
            return (a, b, c, d) =>
            {
                using var marshaledName = new MarshaledString(d.AsSpan());
                return LLVM.BuildICmp(a, pred, b, c, marshaledName);
            };
        }

        private static unsafe Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef> GetFCompare(LLVMRealPredicate pred)
        {
            return (a, b, c, d) =>
            {
                using var marshaledName = new MarshaledString(d.AsSpan());
                return LLVM.BuildFCmp(a, pred, b, c, marshaledName);
            };
        }

        private void InitBinaryOperators()
        {
            builtInBinOperators = new Dictionary<(string, HapetType, HapetType), Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef>>();
            var globalScope = _compiler.GlobalScope;
            var allBuiltInOperators = globalScope.GetBuiltInBinaryOperators();
            foreach (var op in allBuiltInOperators)
            {
                Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef> theFunc;
                switch (op.Name)
                {
                    case "+":
                        {
                            // checking if the result type of the OP is float - then use FAdd
                            if (op.ResultType is FloatType) theFunc = LlvmExtensions.BuildFAdd;
                            // special logics for ptrs
                            else if (op.ResultType is PointerType)
                            {
                                theFunc = (LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string oper) =>
                                {
                                    var leftV = CreateCast(builder, left, op.LhsType, IntPtrType.Instance);
                                    var rightV = CreateCast(builder, right, op.RhsType, IntPtrType.Instance);

                                    // getting add func for the new types
                                    var binOp = GetBinOp(op.Name, IntPtrType.Instance, IntPtrType.Instance);
                                    var res = binOp(builder, leftV, rightV, oper);
                                    return CreateCast(builder, res, IntPtrType.Instance, op.ResultType);
                                };
                            }
                            else theFunc = LlvmExtensions.BuildAdd;
                            break;
                        }
                    case "-":
                        {
                            // checking if the result type of the OP is float - then use FSub
                            if (op.ResultType is FloatType) theFunc = LlvmExtensions.BuildFSub;
                            // special logics for ptrs
                            else if (op.ResultType is PointerType)
                            {
                                theFunc = (LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string oper) =>
                                {
                                    var leftV = CreateCast(builder, left, op.LhsType, IntPtrType.Instance);
                                    var rightV = CreateCast(builder, right, op.RhsType, IntPtrType.Instance);

                                    // getting sub func for the new types
                                    var binOp = GetBinOp(op.Name, IntPtrType.Instance, IntPtrType.Instance);
                                    var res = binOp(builder, leftV, rightV, oper);
                                    return CreateCast(builder, res, IntPtrType.Instance, op.ResultType);
                                };
                            }
                            else theFunc = LlvmExtensions.BuildSub;
                            break;
                        }
                    case "*":
                        {
                            // checking if the result type of the OP is float - then use FMul
                            if (op.ResultType is FloatType) theFunc = LlvmExtensions.BuildFMul;
                            else theFunc = LlvmExtensions.BuildMul;
                            break;
                        }
                    case "/":
                        {
                            // checking if the result type of the OP is float - then use FDiv
                            if (op.ResultType is FloatType) theFunc = LlvmExtensions.BuildFDiv;
                            else if (op.ResultType is IntType intType && intType.Signed) theFunc = LlvmExtensions.BuildSDiv;
                            else theFunc = LlvmExtensions.BuildUDiv; // here is also char type, so it is ok
                            break;
                        }
                    case "%":
                        {
                            // checking if the result type of the OP is float - then use FRem
                            if (op.ResultType is FloatType) theFunc = LlvmExtensions.BuildFRem;
                            else if (op.ResultType is IntType intType && intType.Signed) theFunc = LlvmExtensions.BuildSRem;
                            else theFunc = LlvmExtensions.BuildURem; // here is also char type, so it is ok
                            break;
                        }
                    case "&":
                        {
                            // checking if the result type of the OP is float - set null
                            if (op.ResultType is IntType || op.ResultType is CharType) theFunc = LlvmExtensions.BuildAnd;
                            else theFunc = null;
                            break;
                        }
                    case "|":
                        {
                            // checking if the result type of the OP is float - set null
                            if (op.ResultType is IntType || op.ResultType is CharType) theFunc = LlvmExtensions.BuildOr;
                            else theFunc = null;
                            break;
                        }
                    case ">>":
                        {
                            // checking if the result type of the OP is float - set null
                            if (op.ResultType is IntType || op.ResultType is CharType) theFunc = LlvmExtensions.BuildRShift;
                            else theFunc = null;
                            break;
                        }
                    case "<<":
                        {
                            // checking if the result type of the OP is float - set null
                            if (op.ResultType is IntType || op.ResultType is CharType) theFunc = LlvmExtensions.BuildLShift;
                            else theFunc = null;
                            break;
                        }
                    case "==":
                        {
                            // checking if the result type of the OP is float
                            if (op.RhsType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealOEQ);
                            // special logics for ptrs
                            else if (op.RhsType is PointerType)
                            {
                                theFunc = (LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string oper) =>
                                {
                                    var leftV = CreateCast(builder, left, op.LhsType, IntPtrType.Instance);
                                    var rightV = CreateCast(builder, right, op.RhsType, IntPtrType.Instance);

                                    // getting add func for the new types
                                    var binOp = GetBinOp(op.Name, IntPtrType.Instance, IntPtrType.Instance);
                                    var res = binOp(builder, leftV, rightV, oper);
                                    return CreateCast(builder, res, IntPtrType.Instance, op.ResultType);
                                };
                            }
                            else theFunc = GetICompare(LLVMIntPredicate.LLVMIntEQ);
                            break;
                        }
                    case "!=":
                        {
                            // checking if the result type of the OP is float
                            if (op.RhsType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealONE);
                            // special logics for ptrs
                            else if (op.RhsType is PointerType)
                            {
                                theFunc = (LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string oper) =>
                                {
                                    var leftV = CreateCast(builder, left, op.LhsType, IntPtrType.Instance);
                                    var rightV = CreateCast(builder, right, op.RhsType, IntPtrType.Instance);

                                    // getting add func for the new types
                                    var binOp = GetBinOp(op.Name, IntPtrType.Instance, IntPtrType.Instance);
                                    var res = binOp(builder, leftV, rightV, oper);
                                    return CreateCast(builder, res, IntPtrType.Instance, op.ResultType);
                                };
                            }
                            else theFunc = GetICompare(LLVMIntPredicate.LLVMIntNE);
                            break;
                        }
                    case "<":
                        {
                            // checking if the result type of the OP is float
                            if (op.RhsType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealOLT);
                            else if (op.RhsType is IntType intType && intType.Signed) theFunc = GetICompare(LLVMIntPredicate.LLVMIntSLT);
                            else theFunc = GetICompare(LLVMIntPredicate.LLVMIntULT); // here is also char type, so it is ok
                            break;
                        }
                    case "<=":
                        {
                            // checking if the result type of the OP is float
                            if (op.RhsType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealOLE);
                            else if (op.RhsType is IntType intType && intType.Signed) theFunc = GetICompare(LLVMIntPredicate.LLVMIntSLE);
                            else theFunc = GetICompare(LLVMIntPredicate.LLVMIntULE); // here is also char type, so it is ok
                            break;
                        }
                    case ">":
                        {
                            // checking if the result type of the OP is float
                            if (op.RhsType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealOGT);
                            else if (op.RhsType is IntType intType && intType.Signed) theFunc = GetICompare(LLVMIntPredicate.LLVMIntSGT);
                            else theFunc = GetICompare(LLVMIntPredicate.LLVMIntUGT); // here is also char type, so it is ok
                            break;
                        }
                    case ">=":
                        {
                            // checking if the result type of the OP is float
                            if (op.RhsType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealOGE);
                            else if (op.RhsType is IntType intType && intType.Signed) theFunc = GetICompare(LLVMIntPredicate.LLVMIntSGE);
                            else theFunc = GetICompare(LLVMIntPredicate.LLVMIntUGE); // here is also char type, so it is ok
                            break;
                        }
                    case "&&":
                        {
                            // bool and 
                            if (op.ResultType is BoolType && op.RhsType is BoolType && op.LhsType is BoolType) theFunc = LlvmExtensions.BuildAnd;
                            else theFunc = null;
                            break;
                        }
                    case "||":
                        {
                            // bool or 
                            if (op.ResultType is BoolType && op.RhsType is BoolType && op.LhsType is BoolType) theFunc = LlvmExtensions.BuildOr;
                            else theFunc = null;
                            break;
                        }
                    default:
                        {
                            // skip as/is/in
                            if (op.Name != "as" && op.Name != "is" && op.Name != "in")
                                _messageHandler.ReportMessage([op.Name], ErrorCode.Get(CTEN.UnexpectedOperator));
                            theFunc = null;
                            break;
                        }
                }
                if (theFunc != null)
                    builtInBinOperators.Add((op.Name, op.LhsType, op.RhsType), theFunc);
            }
        }

        private void InitUnaryOperators()
        {
            builtInUnOperators = new Dictionary<(string, HapetType), Func<LLVMBuilderRef, LLVMValueRef, string, LLVMValueRef>>();
            var globalScope = _compiler.GlobalScope;
            var allBuiltInOperators = globalScope.GetBuiltInUnaryOperators();
            foreach (var op in allBuiltInOperators)
            {
                Func<LLVMBuilderRef, LLVMValueRef, string, LLVMValueRef> theFunc;
                switch (op.Name)
                {
                    case "!":
                        {
                            // '!' op can only be applied to 'bool' type
                            if (op.ResultType is BoolType && op.SubExprType is BoolType) theFunc = LlvmExtensions.BuildNot;
                            else theFunc = null;
                            break;
                        }
                    case "-":
                        {
                            // negating numbers (do we need this for char type?)
                            if (op.ResultType is FloatType) theFunc = LlvmExtensions.BuildFNeg;
                            else if (op.ResultType is IntType) theFunc = LlvmExtensions.BuildNeg;
                            else theFunc = null;
                            break;
                        }
                    default:
                        {
                            // error here (internal compiler error, should not happen)
                            _messageHandler.ReportMessage([op.Name], ErrorCode.Get(CTEN.UnexpectedOperator));
                            theFunc = null;
                            break;
                        }
                }
                if (theFunc != null)
                    builtInUnOperators.Add((op.Name, op.SubExprType), theFunc);
            }
        }
    }
}
