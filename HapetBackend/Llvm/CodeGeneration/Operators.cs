using HapetFrontend.Ast.Declarations;
using HapetFrontend.Errors;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using LLVMSharp.Interop;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private Dictionary<BuiltInBinaryOperator, Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef>> builtInBinOperators;
        private Dictionary<BuiltInUnaryOperator, Func<LLVMBuilderRef, LLVMValueRef, string, LLVMValueRef>> builtInUnOperators;

        private void InitOperators()
        {
            InitBinaryOperators();
            InitUnaryOperators();
        }

        private Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef> GetBinOp(BuiltInBinaryOperator op)
        {
            return builtInBinOperators[op];
        }

        private Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef> SearchBinOp(string name, HapetType left, HapetType right)
        {
            var ops = _compiler.GlobalScope.GetBinaryOperators(name, left, right);
            return GetBinOp(ops[0] as BuiltInBinaryOperator); // WARN: there has to be at least one op - PostPrepare has to handle it
        }

        private Func<LLVMBuilderRef, LLVMValueRef, string, LLVMValueRef> GetUnOp(BuiltInUnaryOperator op)
        {
            return builtInUnOperators[op];
        }

        private Func<LLVMBuilderRef, LLVMValueRef, string, LLVMValueRef> SearchUnOp(string name, HapetType type)
        {
            var ops = _compiler.GlobalScope.GetUnaryOperators(name, type);
            return GetUnOp(ops[0] as BuiltInUnaryOperator); // WARN: there has to be at least one op - PostPrepare has to handle it
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
            builtInBinOperators = new Dictionary<BuiltInBinaryOperator, Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef>>();
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
                            else if (op.LhsType is PointerType || op.RhsType is PointerType) theFunc = GetCringeFuncForPtrs(op);
                            else theFunc = LlvmExtensions.BuildAdd;
                            break;
                        }
                    case "-":
                        {
                            // checking if the result type of the OP is float - then use FSub
                            if (op.ResultType is FloatType) theFunc = LlvmExtensions.BuildFSub;
                            // special logics for ptrs
                            else if (op.LhsType is PointerType || op.RhsType is PointerType) theFunc = GetCringeFuncForPtrs(op);
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
                            if (op.ResultType is IntType || op.ResultType is CharType || op.ResultType is EnumType) 
                                theFunc = LlvmExtensions.BuildAnd;
                            else theFunc = null;
                            break;
                        }
                    case "|":
                        {
                            // checking if the result type of the OP is float - set null
                            if (op.ResultType is IntType || op.ResultType is CharType || op.ResultType is EnumType) 
                                theFunc = LlvmExtensions.BuildOr;
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
                    case "^":
                        {
                            // xor 
                            // checking if the result type of the OP is float - set null
                            if (op.ResultType is IntType || op.ResultType is CharType) theFunc = LlvmExtensions.BuildXor;
                            else theFunc = null;
                            break;
                        }
                    case "==":
                        {
                            // checking if the result type of the OP is float
                            if (op.RhsType is FloatType || op.LhsType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealOEQ);
                            // special logics for ptrs
                            else if (op.RhsType is PointerType || op.LhsType is PointerType || op.RhsType is NullType || op.LhsType is NullType) 
                                theFunc = GetCringeFuncForPtrs(op);
                            // special logics for ints
                            else theFunc = GetICompare(LLVMIntPredicate.LLVMIntEQ);
                            // TODO: special keys for strings and arrays
                            break;
                        }
                    case "!=":
                        {
                            // checking if the result type of the OP is float
                            if (op.RhsType is FloatType || op.LhsType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealONE);
                            // special logics for ptrs
                            else if (op.RhsType is PointerType || op.LhsType is PointerType || op.RhsType is NullType || op.LhsType is NullType) 
                                theFunc = GetCringeFuncForPtrs(op);
                            // special logics for ints
                            else theFunc = GetICompare(LLVMIntPredicate.LLVMIntNE);
                            // TODO: special keys for strings and arrays
                            break;
                        }
                    case "<":
                        {
                            // checking if the result type of the OP is float
                            if (op.RhsType is FloatType || op.LhsType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealOLT);
                            else if (op.RhsType is IntType intType && intType.Signed) theFunc = GetICompare(LLVMIntPredicate.LLVMIntSLT);
                            else theFunc = GetICompare(LLVMIntPredicate.LLVMIntULT); // here is also char type, so it is ok
                            break;
                        }
                    case "<=":
                        {
                            // checking if the result type of the OP is float
                            if (op.RhsType is FloatType || op.LhsType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealOLE);
                            else if (op.RhsType is IntType intType && intType.Signed) theFunc = GetICompare(LLVMIntPredicate.LLVMIntSLE);
                            else theFunc = GetICompare(LLVMIntPredicate.LLVMIntULE); // here is also char type, so it is ok
                            break;
                        }
                    case ">":
                        {
                            // checking if the result type of the OP is float
                            if (op.RhsType is FloatType || op.LhsType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealOGT);
                            else if (op.RhsType is IntType intType && intType.Signed) theFunc = GetICompare(LLVMIntPredicate.LLVMIntSGT);
                            else theFunc = GetICompare(LLVMIntPredicate.LLVMIntUGT); // here is also char type, so it is ok
                            break;
                        }
                    case ">=":
                        {
                            // checking if the result type of the OP is float
                            if (op.RhsType is FloatType || op.LhsType is FloatType) theFunc = GetFCompare(LLVMRealPredicate.LLVMRealOGE);
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
                    builtInBinOperators.Add(op, theFunc);
            }

            Func<LLVMBuilderRef, LLVMValueRef, LLVMValueRef, string, LLVMValueRef> GetCringeFuncForPtrs(BuiltInBinaryOperator op)
            {
                return (LLVMBuilderRef builder, LLVMValueRef left, LLVMValueRef right, string oper) =>
                {
                    HapetType leftT = op.LhsType is ClassType ? PointerType.GetPointerType(op.LhsType) : op.LhsType;
                    HapetType rightT = op.RhsType is ClassType ? PointerType.GetPointerType(op.RhsType) : op.RhsType;
                    HapetType outT = op.ResultType is ClassType ? PointerType.GetPointerType(op.ResultType) : op.ResultType;

                    var leftV = CreateCast(builder, left, leftT, HapetType.CurrentTypeContext.IntPtrTypeInstance);
                    var rightV = CreateCast(builder, right, rightT, HapetType.CurrentTypeContext.IntPtrTypeInstance);

                    // getting add func for the new types
                    var binOp = SearchBinOp(op.Name, HapetType.CurrentTypeContext.IntPtrTypeInstance, HapetType.CurrentTypeContext.IntPtrTypeInstance);
                    var res = binOp(builder, leftV, rightV, oper);
                    if (outT is not BoolType)
                        return CreateCast(builder, res, HapetType.CurrentTypeContext.IntPtrTypeInstance, outT);
                    return res;
                };
            }
        }

        private void InitUnaryOperators()
        {
            builtInUnOperators = new Dictionary<BuiltInUnaryOperator, Func<LLVMBuilderRef, LLVMValueRef, string, LLVMValueRef>>();
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
                    case "~":
                        {
                            /// just skip. Handled in <see cref="GenerateUnaryExprCode"/>
                            theFunc = null;
                            break;
                        }
                    case "--":
                    case "++":
                        {
                            // just skip them here
                            theFunc = null;
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
                    builtInUnOperators.Add(op, theFunc);
            }
        }
    }
}
