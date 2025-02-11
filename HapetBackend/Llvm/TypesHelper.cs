using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using LLVMSharp.Interop;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private Dictionary<HapetType, LLVMTypeRef> _typeMap = new Dictionary<HapetType, LLVMTypeRef>();
        /// <summary>
        /// The value itself (loaded after alloca)
        /// </summary>
        private Dictionary<ISymbol, LLVMValueRef> _valueMap = new Dictionary<ISymbol, LLVMValueRef>();
        /// <summary>
        /// Class or struct mapping to their elements
        /// </summary>
        private Dictionary<HapetType, List<HapetType>> _structTypeElementsMap = new Dictionary<HapetType, List<HapetType>>();

        /// <summary>
        /// Struct offsets mapping when StructLayoutAttribute used
        /// </summary>
        private Dictionary<HapetType, uint[]> _structOffsets = new Dictionary<HapetType, uint[]>();

        /// <summary>
        /// Anon delegate name to type mappings. Used when creating a delegate and assigning a func to it
        /// </summary>
        private Dictionary<string, LLVMTypeRef> _delegateAnonTypes = new Dictionary<string, LLVMTypeRef>();

        private LLVMTypeRef HapetTypeToLLVMType(HapetType ht)
        {
            if (_typeMap.TryGetValue(ht, out var tt))
                return tt;

            // all array types are the same
            if (ht is ArrayType)
            {
                var kw = _typeMap.Any(x => x.Key is ArrayType);
                if (kw)
                    return _typeMap.First(x => x.Key is ArrayType).Value;
            }

            var t = HapetTypeToLLVMTypeHelper(ht);
            _typeMap[ht] = t;
            return t;
        }

        private unsafe LLVMTypeRef HapetTypeToLLVMTypeHelper(HapetType ht)
        {
            switch (ht)
            {
                case ClassType t:
                    {
                        return _context.CreateNamedStruct($"class.{t.Declaration.Name.Name}"); ;
                    }

                case BoolType b:
                    return _context.Int1Type;

                case IntType i:
                    return _context.GetIntType((uint)i.GetSize() * 8);

                case FloatType f:
                    if (f.GetSize() == 2)
                        return _context.HalfType;
                    else if (f.GetSize() == 4)
                        return _context.FloatType;
                    else if (f.GetSize() == 8)
                        return _context.DoubleType;
                    else throw new NotImplementedException();

                case CharType c:
                    return _context.GetIntType((uint)c.GetSize() * 8);

                case PointerType p:
                    {
                        if (p.TargetType == null || p.TargetType == VoidType.Instance)
                            return ((LLVMTypeRef)_context.Int8Type).GetPointerTo();
                        return HapetTypeToLLVMType(p.TargetType).GetPointerTo();
                    }

                case ReferenceType r:
                    {
                        return HapetTypeToLLVMType(r.TargetType).GetPointerTo();
                    }

                case ThisType self:
                    return _context.Int8Type;

                case VoidType _:
                    return _context.VoidType;

                case FunctionType f:
                    {
                        var paramTypes = f.Declaration.Parameters.Select(rt => HapetTypeToLLVMType(rt.Type.OutType)).ToList();
                        var returnType = HapetTypeToLLVMType(f.Declaration.Returns.OutType);
                        var funcType = LLVMTypeRef.CreateFunction(returnType, paramTypes.ToArray(), false); // TODO: var args
                        return funcType;
                    }

                case DelegateType d:
                    {
                        var funcType = GetFunctionTypeOfDelegate(d);

                        // fields of delegate struct
                        var objectPtr = HapetTypeToLLVMType(PointerType.GetPointerType(IntType.GetIntType(1, false))); // ptr to func object
                        var funcPtr = funcType.GetPointerTo();

                        var str = _context.CreateNamedStruct($"delegate.{d.Declaration.Name.Name}");
                        str.StructSetBody(new LLVMTypeRef[] {
                            ((LLVMTypeRef)funcPtr),
                            ((LLVMTypeRef)objectPtr)
                        }, false);
                        return str;
                    }

                case EnumType e:
                    {
                        return _context.GetIntType(((uint)e.Declaration.InheritedType.OutType.GetSize()) * 8);
                    }

                case StructType s:
                    {
                        // the main logics with offsets
                        // No Attribute: Call StructSetBody with 'false', LLVM will set offsets by its own.
                        // Attribute [StructLayout(Pack = 1)]: Call StructSetBody with 'true', minimal pack.
                        // Attribute [StructLayout(Pack = 2, 4, and etc.)]: Call StructSetBody with 'true', set offsets by my own.

                        // enable packing if there is a proper attribute
                        // getting attribute if it exists
                        var layoutAttr = s.Declaration.Attributes.FirstOrDefault(x => x.AttributeName.TryFlatten(null, null) == "System.Runtime.InteropServices.StructLayoutAttribute");
                        int packNumber = 0;
                        if (layoutAttr != null)
                        {
                            var thePackParam = layoutAttr.Parameters.First();
                            var tmpPack = (int)((NumberData)thePackParam.OutValue).IntValue;
                            // check for 0 and other shite
                            if (tmpPack <= 0)
                            {
                                _messageHandler.ReportMessage(_currentSourceFile.Text, thePackParam, [], ErrorCode.Get(CTEN.PackLessThanOne));
                            }
                            else if (!Funcad.IsPowerOfTwo(tmpPack))
                            {
                                // if it is not a power of two
                                _messageHandler.ReportMessage(_currentSourceFile.Text, thePackParam, [], ErrorCode.Get(CTEN.PackNotPowerOfTwo));
                            }
                            else
                            {
                                // if everything is ok :)
                                packNumber = tmpPack;
                                s.IsUserDefinedAlignment = true;
                            }
                        }

                        // creating the struct
                        var name = $"struct.{s.Declaration.Name.Name}";

                        var llvmType = _context.CreateNamedStruct(name);
                        _typeMap[s] = llvmType;

                        // WARN: this shite with alignment is done like 'LayoutKind.Sequential' in C#
                        // so all the members are going to be aligned properly by their types
                        var memTypes = new List<LLVMTypeRef>(s.Declaration.Declarations.Count);
                        var offsets = new uint[s.Declaration.Declarations.Count];
                        int currentSize = 0;
                        int i = 0;
                        foreach (var mem in s.Declaration.Declarations)
                        {
                            // we need to get a minimal type alignment size depending on pack
                            int typeAlignment = Math.Min(mem.Type.OutType.GetAlignment(), packNumber);

                            // if current offset is shity for the member type
                            // we need to append a padding
                            // WARN: create offsets only if pack is set or bigger than 1
                            if (packNumber > 1 && currentSize % typeAlignment != 0)
                            {
                                // add padding
                                int padding = typeAlignment - currentSize % typeAlignment;
                                memTypes.Add(LLVM.ArrayType(LLVM.Int8Type(), (uint)padding));
                                currentSize += padding;
                            }

                            offsets[i] = (uint)memTypes.Count;
                            memTypes.Add(HapetTypeToLLVMType(mem.Type.OutType));
                            currentSize += (int)((_targetData.SizeOfTypeInBits(memTypes.Last()) + 7) / 8);
                            i += 1;
                        }
                        // add padding at the end
                        // WARN: create offsets only if pack is set or bigger than 1
                        if (packNumber > 1 && currentSize % packNumber != 0)
                        {
                            // add padding
                            int padding = packNumber - currentSize % packNumber;
                            memTypes.Add(LLVM.ArrayType(LLVM.Int8Type(), (uint)padding));
                            currentSize += padding;
                        }
                        // saving the offsets so we can access struct elements easily in the future
                        _structOffsets[s] = offsets;

                        llvmType.StructSetBody(memTypes.ToArray(), packNumber >= 1);

                        // getting size of the struct that is calced inside LLVM
                        s.ChangeSize((int)LLVM.ABISizeOfType(_targetData, llvmType));
                        // getting the alignment of the struct
                        if (packNumber >= 1)
                            s.ChangeAlignment(packNumber);
                        else
                            s.ChangeAlignment((int)LLVM.ABIAlignmentOfType(_targetData, llvmType));

                        return llvmType;
                    }

                case TupleType t:
                    {
                        var memTypes = t.Members.Select(m => HapetTypeToLLVMType(m.type)).ToArray();
                        return _context.GetStructType(memTypes, false);
                    }

                default:
                    throw new NotImplementedException(HapetType.AsString(ht));
            }
        }

        private unsafe LLVMValueRef HapetValueToLLVMValue(HapetType type, object v)
        {
            // ??? TOOD: why there is such check?
            if (type == IntType.LiteralType || type == FloatType.LiteralType)
                throw new Exception();

            switch (type)
            {
                case BoolType _: return LLVM.ConstInt(HapetTypeToLLVMType(type), (bool)v ? 1ul : 0ul, 0);
                case CharType _: return LLVM.ConstInt(HapetTypeToLLVMType(type), (char)v, 0);
                case IntType i: return LLVM.ConstInt(HapetTypeToLLVMType(type), ((NumberData)v).ToULong(), i.Signed ? 1 : 0);
                case FloatType: return LLVM.ConstReal(HapetTypeToLLVMType(type), ((NumberData)v).ToDouble());
                case StringType:
                    {
                        // this code creates a new string struct so its content could be easily copied to another string var
                        string theString = (string)v;
                        var stringSizeValueRef = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(IntType.GetIntType(4, true)), (ulong)theString.Length);

                        // creating global static array
                        var elements = theString.ToCharArray().Select(c => HapetValueToLLVMValue(CharType.DefaultType, c)).ToArray();
                        var stringGlobArray = _module.AddGlobal(LLVMTypeRef.CreateArray(HapetTypeToLLVMType(CharType.DefaultType), (uint)theString.Length), "constString");
                        stringGlobArray.Initializer = LLVMValueRef.CreateConstArray(HapetTypeToLLVMType(CharType.DefaultType), elements);

                        var stringType = StringType.GetInstance(_currentSourceFile.NamespaceScope);

                        // creating string variable
                        var theStringItself = CreateLocalVariable(stringType, "theString");

                        // it would work only with const string assignment. if you want smth like this to work
                        // if they are trying to store a char* in string
                        var tp = HapetTypeToLLVMType(stringType);
                        // the 1 is because StringType struct has buf field as it's 1 param
                        var buf = _builder.BuildStructGEP2(tp, theStringItself, 1, "strBuf");
                        _builder.BuildStore(stringGlobArray, buf);
                        /// setting the string size.
                        var len = _builder.BuildStructGEP2(tp, theStringItself, 0, "strLen");
                        _builder.BuildStore(stringSizeValueRef, len);

                        // loading the string struct
                        var theStringItselfLoaded = _builder.BuildLoad2(HapetTypeToLLVMType(stringType), theStringItself, "theStringLoaded");
                        return theStringItselfLoaded;
                    }
            }
            return new LLVMValueRef();
        }

        private LLVMValueRef CreateLocalVariable(HapetType exprType, string name = "temp")
        {
            return CreateLocalVariable(HapetTypeToLLVMType(exprType), exprType.GetAlignment(), name);
        }

        private LLVMValueRef CreateLocalVariable(LLVMTypeRef type, int alignment, string name = "temp")
        {
            var result = _builder.BuildAlloca(type, name);
            var llvmAlignment = _targetData.PreferredAlignmentOfType(type);
            if (alignment >= 0)
            {
                result.SetAlignment((uint)alignment);
            }

            return result;
        }

        private LLVMValueRef CreateCast(LLVMBuilderRef builder, LLVMValueRef val, HapetType inType, HapetType outType)
        {
            if (inType is PointerType)
            {
                if (outType is IntPtrType)
                {
                    return builder.BuildPtrToInt(val, HapetTypeToLLVMType(outType));
                }
            }
            if (inType is IntPtrType)
            {
                if (outType is PointerType)
                {
                    return builder.BuildIntToPtr(val, HapetTypeToLLVMType(outType));
                }
            }


            if (inType is IntType intType && intType.Signed)
            {
                if (outType is FloatType floatType)
                {
                    return builder.BuildSIToFP(val, HapetTypeToLLVMType(floatType));
                }
                else if (outType is IntType || outType is CharType)
                {
                    if (inType.GetSize() > outType.GetSize())
                    {
                        return builder.BuildTruncOrBitCast(val, HapetTypeToLLVMType(outType));
                    }
                    else if (inType.GetSize() < outType.GetSize())
                    {
                        // for signed and unsigned they are the same
                        return builder.BuildSExtOrBitCast(val, HapetTypeToLLVMType(outType));
                    }
                    // if the same size just return it
                    return val;
                }
                // TODO: ...
            }
            else if ((inType is IntType intType2 && !intType2.Signed) || inType is CharType)
            {
                if (outType is FloatType floatType)
                {
                    return builder.BuildUIToFP(val, HapetTypeToLLVMType(floatType));
                }
                else if (outType is IntType || outType is CharType)
                {
                    if (inType.GetSize() > outType.GetSize())
                    {
                        return builder.BuildTruncOrBitCast(val, HapetTypeToLLVMType(outType));
                    }
                    else if (inType.GetSize() < outType.GetSize())
                    {
                        return builder.BuildZExtOrBitCast(val, HapetTypeToLLVMType(outType));
                    }
                    // if the same size just return it
                    return val;
                }
                // TODO: ...
            }
            else if (inType is FloatType)
            {
                if (outType is IntType intType1 && intType1.Signed)
                {
                    return builder.BuildFPToSI(val, HapetTypeToLLVMType(intType1));
                }
                else if ((outType is IntType intType3 && !intType3.Signed) || outType is CharType)
                {
                    return builder.BuildFPToUI(val, HapetTypeToLLVMType(outType));
                }
                else if (outType is FloatType floatType)
                {
                    if (inType.GetSize() > floatType.GetSize())
                    {
                        return builder.BuildFPTrunc(val, HapetTypeToLLVMType(floatType));
                    }
                    else
                    {
                        return builder.BuildFPExt(val, HapetTypeToLLVMType(floatType));
                    }
                }
                // TODO: ...
            }
            // TODO: ...
            return val;
        }

        /// <summary>
        /// Assigns value to a variable
        /// </summary>
        /// <param name="varPtr">The variable</param>
        /// <param name="varType">The type of variable</param>
        /// <param name="value">The value that needs to be assigned</param>
        private void AssignToVar(LLVMValueRef varPtr, HapetType varType, AstExpression value)
        {
            // generate the initializer value
            var x = GenerateExpressionCode(value);
            // return if the value is cringe and was not generated properly
            if (x == default)
                return;
            // just storing initializer value in the var
            _builder.BuildStore(x, varPtr);
        }

        private LLVMTypeRef GetFunctionTypeOfDelegate(DelegateType del)
        {
            var paramTypes = del.Declaration.Parameters.Select(rt => HapetTypeToLLVMType(rt.Type.OutType)).ToList();
            var returnType = HapetTypeToLLVMType(del.Declaration.Returns.OutType);
            return LLVMTypeRef.CreateFunction(returnType, paramTypes.ToArray(), false);
        }

        #region Mallocs
        private LLVMValueRef GetMalloc(int typeSize, int amount)
        {
            var tp = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(IntType.GetIntType(4, true)), (ulong)typeSize);
            var am = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(IntType.GetIntType(4, true)), (ulong)amount);
            return GetMalloc(tp, am);
        }

        private LLVMValueRef GetMalloc(LLVMValueRef typeSize, int amount)
        {
            var am = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(IntType.GetIntType(4, true)), (ulong)amount);
            return GetMalloc(typeSize, am);
        }

        private LLVMValueRef GetMalloc(int typeSize, LLVMValueRef amount)
        {
            var tp = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(IntType.GetIntType(4, true)), (ulong)typeSize);
            return GetMalloc(tp, amount);
        }

        private LLVMValueRef GetMalloc(LLVMValueRef typeSize, LLVMValueRef amount)
        {
            // WARN: hard cock
            var marshalDecl = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", "Marshal");
            var mallocSymbol = (marshalDecl.Decl as AstClassDecl).SubScope.GetSymbol("System.Runtime.InteropServices.Marshal::Malloc(int)") as DeclSymbol;
            var mallocFunc = _valueMap[mallocSymbol];
            LLVMTypeRef funcType = _typeMap[mallocSymbol.Decl.Type.OutType];
            // calc size to malloc = amount * typeSize
            var sizeToMalloc = _builder.BuildMul(amount, typeSize, "sizeToMalloc");
            return _builder.BuildCall2(funcType, mallocFunc, new LLVMValueRef[] { sizeToMalloc }, "allocated");
        }
        #endregion
    }
}
