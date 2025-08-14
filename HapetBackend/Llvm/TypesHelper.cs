using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private readonly Dictionary<HapetType, LLVMTypeRef> _typeMap = new Dictionary<HapetType, LLVMTypeRef>();
        /// <summary>
        /// This dict is used to store real structs of types like bool, int, char and etc.
        /// </summary>
        private readonly Dictionary<HapetType, LLVMTypeRef> _typeMapWithRealStructs = new Dictionary<HapetType, LLVMTypeRef>();
        /// <summary>
        /// The value itself (loaded after alloca)
        /// </summary>
        private readonly Dictionary<ISymbol, LLVMValueRef> _valueMap = new Dictionary<ISymbol, LLVMValueRef>();
        /// <summary>
        /// Used to store get/set funcs for static fields
        /// </summary>
        private readonly Dictionary<ISymbol, (LLVMValueRef, LLVMValueRef)> _staticFieldsValueMap = new Dictionary<ISymbol, (LLVMValueRef, LLVMValueRef)>();

        /// <summary>
        /// Struct offsets mapping when StructLayoutAttribute used
        /// </summary>
        private readonly Dictionary<HapetType, uint[]> _structOffsets = new Dictionary<HapetType, uint[]>();
        /// <summary>
        /// Boxed struct mappings with the first param offset and size
        /// </summary>
        private readonly Dictionary<HapetType, (LLVMTypeRef, uint, int)> _boxedStructTypes = new Dictionary<HapetType, (LLVMTypeRef, uint, int)>();

        /// <summary>
        /// Anon delegate name to type mappings. Used when creating a delegate and assigning a func to it
        /// </summary>
        private readonly Dictionary<string, LLVMTypeRef> _delegateAnonTypes = new Dictionary<string, LLVMTypeRef>();

        private unsafe LLVMTypeRef HapetTypeToLLVMType(HapetType ht, bool getRealStruct = false)
        {
            if (_typeMap.TryGetValue(ht, out var tt))
                return HandleReturnValue(tt);

            var t = HapetTypeToLLVMTypeHelper(ht);
            _typeMap[ht] = t;
            return HandleReturnValue(t);

            // used for return real struct if getRealStruct is true
            LLVMTypeRef HandleReturnValue(LLVMTypeRef val)
            {
                // return the same if no need
                if (!getRealStruct)
                    return val;
                // only structs are allowed
                if (ht is not StructType s)
                    return val;
                // one of these only allowed
                if (s is not IntType && s is not CharType && s is not FloatType && s is not VoidType && s is not BoolType)
                    return val;
                // return if already exists
                if (_typeMapWithRealStructs.TryGetValue(ht, out var tt))
                    return tt;
                
                // creating the struct
                var name = $"struct.{s.TypeName}";
                var llvmType = _context.CreateNamedStruct(name);
                var fieldDeclarations = s.Declaration.GetAllRawFields();
                var (offsets, sssize, memTypes, _) = CalcStructData(fieldDeclarations, 0, false);

                // saving the offsets so we can access struct elements easily in the future
                _structOffsets[s] = offsets;

                llvmType.StructSetBody(memTypes.ToArray(), false);

                _typeMapWithRealStructs[ht] = llvmType;
                return llvmType;
            }
        }

        #region checks for boxed types creation
        private bool _isBoolBoxedCreated = false;
        private readonly List<IntType> _isIntBoxedCreated = new List<IntType>();
        private readonly List<FloatType> _isFloatBoxedCreated = new List<FloatType>();
        private bool _isCharBoxedCreated = false;
        private bool _isVoidBoxedCreated = false;
        #endregion

        private unsafe LLVMTypeRef HapetTypeToLLVMTypeHelper(HapetType ht)
        {
            switch (ht)
            {
                case BoolType b:
                    {
                        if (!_isBoolBoxedCreated)
                        {
                            _isBoolBoxedCreated = true;
                            AddBoxedType(b, 0);
                        }
                        return _context.Int1Type;
                    }

                case IntType i:
                    {
                        if (!_isIntBoxedCreated.Contains(i))
                        {
                            _isIntBoxedCreated.Add(i);
                            AddBoxedType(i, 0);
                        }

                        return _context.GetIntType((uint)i.GetSize() * 8);
                    }

                case FloatType f:
                    {
                        if (!_isFloatBoxedCreated.Contains(f))
                        {
                            _isFloatBoxedCreated.Add(f);
                            AddBoxedType(f, 0);
                        }

                        if (f.GetSize() == 4)
                            return _context.FloatType;
                        else if (f.GetSize() == 8)
                            return _context.DoubleType;
                        else throw new NotImplementedException();
                    }

                case CharType c:
                    {
                        if (!_isCharBoxedCreated)
                        {
                            _isCharBoxedCreated = true;
                            AddBoxedType(c, 0);
                        }
                        return _context.GetIntType((uint)c.GetSize() * 8);
                    }

                case PointerType p:
                    {
                        if (p.TargetType == null || p.TargetType == HapetType.CurrentTypeContext.VoidTypeInstance)
                            return ((LLVMTypeRef)_context.Int8Type).GetPointerTo();
                        return HapetTypeToLLVMType(p.TargetType).GetPointerTo();
                    }

                case ReferenceType r:
                    {
                        return HapetTypeToLLVMType(r.TargetType).GetPointerTo();
                    }

                case NullType n:
                    {
                        if (n.TargetType == null || n.TargetType == HapetType.CurrentTypeContext.VoidTypeInstance)
                            return ((LLVMTypeRef)_context.Int8Type).GetPointerTo();
                        return HapetTypeToLLVMType(n.TargetType).GetPointerTo();
                    }

                case VoidType v:
                    {
                        if (!_isVoidBoxedCreated)
                        {
                            _isVoidBoxedCreated = true;
                            AddBoxedType(v, 0);
                        }
                        return _context.VoidType;
                    }

                case FunctionType f:
                    {
                        // skip arglist params!!!
                        var pars = f.Declaration.Parameters.Where(x => x.ParameterModificator != ParameterModificator.Arglist);
                        List<LLVMTypeRef> paramTypes = new List<LLVMTypeRef>();
                        foreach (var par in pars)
                        {
                            // need to take ptr to class type
                            var pt = par.Type.OutType;

                            // making it as pointer if it has ref/out modifier
                            if (par.ParameterModificator == ParameterModificator.Ref || par.ParameterModificator == ParameterModificator.Out)
                                pt = PointerType.GetPointerType(pt);

                            paramTypes.Add(HapetTypeToLLVMType(pt));
                        }
                        // need to make it as a ptr
                        var retT = f.Declaration.Returns.OutType;
                        var returnType = HapetTypeToLLVMType(retT);

                        bool hasArgList = f.Declaration.Parameters.FirstOrDefault(x => x.ParameterModificator == ParameterModificator.Arglist) != null;
                        var funcType = LLVMTypeRef.CreateFunction(returnType, paramTypes.ToArray(), hasArgList);
                        return funcType;
                    }

                case ClassType t:
                    {
                        // creating a class
                        var coolName = GenericsHelper.GetCodegenGenericName(t.Declaration.Name, _messageHandler);
                        var name = $"class.{coolName}";
                        var llvmType = _context.CreateNamedStruct(name);
                        _typeMap[t] = llvmType; // need to do this to prevent stackoverflow
                        var fieldDeclarations = t.Declaration.GetAllRawFields();
                        var (offsets, sssize, memTypes, algn) = CalcStructData(fieldDeclarations, 0, true);

                        llvmType.StructSetBody(memTypes.ToArray(), false);

                        // getting size and align
                        t.SetSizeAndAlignment(sssize, algn);
                        return llvmType;
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
                        var layoutAttr = s.Declaration.Attributes.FirstOrDefault(x => 
                            (x.AttributeName.OutType as ClassType).Declaration.Name.Name == "System.Runtime.InteropServices.StructLayoutAttribute");
                        int packNumber = 0;
                        if (layoutAttr != null)
                        {
                            var thePackParam = layoutAttr.Arguments.First();
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
                        var coolName = GenericsHelper.GetCodegenGenericName(s.Declaration.Name, _messageHandler);
                        var name = $"struct.{coolName}";
                        var llvmType = _context.CreateNamedStruct(name);
                        _typeMap[s] = llvmType; // need to do this to prevent stackoverflow
                        var fieldDeclarations = s.Declaration.GetAllRawFields();
                        var (offsets, sssize, memTypes, algn) = CalcStructData(fieldDeclarations, packNumber, false);

                        // saving the offsets so we can access struct elements easily in the future
                        _structOffsets[s] = offsets;

                        llvmType.StructSetBody(memTypes.ToArray(), packNumber >= 1);

                        // getting size and align
                        s.SetSizeAndAlignment(sssize, packNumber >= 1 ? packNumber : algn);

                        // create a boxed type
                        AddBoxedType(s, packNumber);

                        return llvmType;
                    }

                default:
                    throw new NotImplementedException(HapetType.AsString(ht));
            }
        }

        private void AddBoxedType(StructType type, int packNumber)
        {
            // create a boxed type
            var nameBoxed = $"boxed.{type.Declaration.Name.Name}";
            var llvmTypeBoxed = _context.CreateNamedStruct(nameBoxed);
            var fieldDeclarationsBoxed = type.Declaration.GetAllRawFields();
            var (offsetsBoxed, boxedSize, memTypesBoxed, algn) = CalcStructData(fieldDeclarationsBoxed, packNumber, true);

            llvmTypeBoxed.StructSetBody(memTypesBoxed.ToArray(), packNumber >= 1);

            // offset to the first normal field
            _boxedStructTypes.Add(type, (llvmTypeBoxed, 0, boxedSize));
        }

        private (LLVMTypeRef, uint, int) GetBoxedType(HapetType type)
        {
            // we need to search there a literal type or array
            var typeToSearch = type;
            return _boxedStructTypes[typeToSearch];
        }

        private unsafe LLVMValueRef HapetValueToLLVMValue(HapetType type, object v)
        {
            // ??? TOOD: why there is such check?
            if (type == IntType.LiteralType || type == FloatType.LiteralType)
                throw new ArgumentException("Literal types are not allowed for conversion");

            switch (type)
            {
                case BoolType _: return LLVM.ConstInt(HapetTypeToLLVMType(type), (bool)v ? 1ul : 0ul, 0);
                case CharType _:
                    char val = v is NumberData data ? (char)(int)(data.IntValue) : (char)v;
                    return LLVM.ConstInt(HapetTypeToLLVMType(type), val, 0);
                case IntType i: return LLVM.ConstInt(HapetTypeToLLVMType(type), ((NumberData)v).ToULong(), i.Signed ? 1 : 0);
                case FloatType: return LLVM.ConstReal(HapetTypeToLLVMType(type), ((NumberData)v).ToDouble());
                case StringType:
                    {
                        // this code creates a new string struct so its content could be easily copied to another string var
                        string theString = (string)v;
                        var stringSizeValueRef = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(HapetType.CurrentTypeContext.GetIntType(4, true)), (ulong)theString.Length);

                        // creating global static array
                        var charT = HapetType.CurrentTypeContext.CharTypeInstance;
                        var elements = theString.ToCharArray().Select(c => HapetValueToLLVMValue(charT, c)).ToArray();
                        var stringGlobArray = _module.AddGlobal(LLVMTypeRef.CreateArray(HapetTypeToLLVMType(charT), (uint)theString.Length), "constString");
                        stringGlobArray.Initializer = LLVMValueRef.CreateConstArray(HapetTypeToLLVMType(charT), elements);

                        var stringType = HapetType.CurrentTypeContext.StringTypeInstance;

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

        private unsafe (uint[], int, List<LLVMTypeRef>, int) CalcStructData(List<AstVarDecl> decls, int packNumber, bool withTypeInfo = true)
        {
            // WARN: this shite with alignment is done like 'LayoutKind.Sequential' in C#
            // so all the members are going to be aligned properly by their types
            var memTypes = new List<LLVMTypeRef>(decls.Count);
            var offsets = new uint[decls.Count];
            int currentSize = 0;
            int biggestAlignment = 0;
            int i = 0;
            int padding;

            if (withTypeInfo)
            {
                currentSize += 8; // WARN: cringe alloc for Typeinfo ptr
            }

            foreach (var mem in decls)
            {
                // getting the type
                var type = mem.Type.OutType;

                // if the field type is not yet calculated
                if (type.GetAlignment() <= 0 && (type is ClassType || type is StructType))
                {
                    _ = HapetTypeToLLVMType(type);
                }

                // we need to get a minimal type alignment size depending on pack
                int typeAlignment = packNumber > 0 ? Math.Min(type.GetAlignment(), packNumber) : type.GetAlignment();
                Debug.Assert(typeAlignment > 0);

                // update biggest alignment
                if (typeAlignment > biggestAlignment)
                    biggestAlignment = typeAlignment;

                padding = currentSize % typeAlignment != 0 ? typeAlignment - currentSize % typeAlignment : 0;
                // if current offset is shity for the member type
                // we need to append a padding
                // WARN: create offsets only if pack is set or bigger than 1
                if (packNumber > 1 && currentSize % typeAlignment != 0)
                {
                    // add padding
                    memTypes.Add(LLVM.ArrayType(LLVM.Int8Type(), (uint)padding));
                }
                currentSize += padding;

                offsets[i] = (uint)memTypes.Count;
                memTypes.Add(HapetTypeToLLVMType(type));
                currentSize += type.GetSize();
                i += 1;
            }

            // set packing as biggest alignment if it is really bigger
            if (packNumber > biggestAlignment)
                biggestAlignment = packNumber;

            // add padding at the end
            // WARN: create offsets only if pack is set or bigger than 1
            if (packNumber > 1 && currentSize % packNumber != 0)
            {
                padding = packNumber - currentSize % packNumber;
                // add padding
                memTypes.Add(LLVM.ArrayType(LLVM.Int8Type(), (uint)padding));
            }
            else if (biggestAlignment != 0 && currentSize % biggestAlignment != 0)
            {
                padding = biggestAlignment - currentSize % biggestAlignment;
            }
            else if (!withTypeInfo && decls.Count == 0)
            {
                // special case to handle empty struct

                // to make size == 1
                padding = 1;
                biggestAlignment = HapetType.CurrentTypeContext.PointerSize;
            }
            else
            {
                padding = 0;
            }
            currentSize += padding;

            return (offsets, currentSize, memTypes, biggestAlignment);
        }

        private LLVMValueRef CreateLocalVariable(HapetType exprType, string name = "temp")
        {
            // need to make a ptr to a class
            var t = exprType;
            return CreateLocalVariable(HapetTypeToLLVMType(t), t.GetAlignment(), name);
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
            // special case
            if (inType == outType)
                return val;

            // check user oveloads at first
            /// almost the same as in <see cref="HapetPostPrepare.PostPrepare.PostPrepareExpressionWithType"/>
            var castOps = _currentFunction.Scope.GetBinaryOperators("cast", outType, inType);
            var allOps = castOps.Where(x => x is UserDefinedBinaryOperator userDef &&
                                                 userDef.Function.Declaration is AstOverloadDecl overDecl &&
                                                 (overDecl.OverloadType == OverloadType.ImplicitCast ||
                                                 overDecl.OverloadType == OverloadType.ExplicitCast)).ToList();
            // if there is a cast - do it 
            if (allOps.Count == 1 && allOps[0] is UserDefinedBinaryOperator castOp)
            {
                var fncType = _typeMap[castOp.Function];
                var fncValue = _valueMap[castOp.Function.Declaration.Symbol];

                return _builder.BuildCall2(fncType, fncValue, new LLVMValueRef[] { val }, "castOp");
            }
            else if (allOps.Count > 1)
            {
                // TODO: get normal location somehow or make the error as out param of the func
                // TODO: MOVE TO PP
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, null, [HapetType.AsString(inType), HapetType.AsString(outType)], ErrorCode.Get(CTEN.AmbiguousCastOverloads));
            }

            if (inType is PointerType ptrT)
            {
                if (outType is IntPtrType)
                {
                    return builder.BuildPtrToInt(val, HapetTypeToLLVMType(outType));
                }
                else if (ptrT.TargetType is ClassType classType && outType is StructType structType2)
                {
                    // check inheritance
                    bool isDownCast = structType2.IsInheritedFrom(classType);
                    if (isDownCast)
                    {
                        return CreateStructCastFromObject(val, structType2, true);
                    }
                }
            }
            if (inType is IntPtrType)
            {
                if (outType is PointerType)
                {
                    return builder.BuildIntToPtr(val, HapetTypeToLLVMType(outType));
                }
                else if (outType is IntType)
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
                else if (outType is StructType structType2)
                {
                    return CreateStructCastFromObject(val, structType2);
                }
            }
            if (inType is NullType)
            {
                // like 'int[] a = null'
                if (outType is ArrayType arrayType)
                {
                    var nullTarget = LLVMValueRef.CreateConstPointerNull(HapetTypeToLLVMType(PointerType.GetPointerType(arrayType.TargetType)));
                    var v = _builder.BuildAlloca(HapetTypeToLLVMType(arrayType), $"nulled_array");
                    var buffer = _builder.BuildGEP2(HapetTypeToLLVMType(arrayType), v, new LLVMValueRef[] { LLVMValueRef.CreateConstInt(_context.Int32Type, 1) }, "arrBuffer");
                    _builder.BuildStore(nullTarget, buffer);
                    return _builder.BuildLoad2(HapetTypeToLLVMType(arrayType), v); // return loaded
                }
                // like 'string a = null'
                else if (outType is StringType stringType)
                {
                    var nullTarget = LLVMValueRef.CreateConstPointerNull(HapetTypeToLLVMType(PointerType.GetPointerType(HapetType.CurrentTypeContext.CharTypeInstance)));
                    var v = _builder.BuildAlloca(HapetTypeToLLVMType(stringType), $"nulled_string");
                    var buffer = _builder.BuildGEP2(HapetTypeToLLVMType(stringType), v, new LLVMValueRef[] { LLVMValueRef.CreateConstInt(_context.Int32Type, 1) }, "strBuffer");
                    _builder.BuildStore(nullTarget, buffer);
                    return _builder.BuildLoad2(HapetTypeToLLVMType(stringType), v); // return loaded
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
                // ...
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
                // ...
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
                // ...
            }

            // no need to else-if here - it can handle bacis types transformation
            if (inType is StructType structType) 
            {
                if (outType is PointerType pt && pt.TargetType is ClassType clsT && 
                    (clsT.Declaration.Name.Name == "System.Object" || clsT.Declaration.Name.Name == "System.ValueType" || clsT.Declaration.IsInterface))
                {
                    // cast from struct instance to object
                    var boxedTypeData = GetBoxedType(inType);
                    int structSize = boxedTypeData.Item3;
                    // allocating memory for struct
                    var v = GetMalloc(structSize, 1);
                    // making offset
                    // WARN: always 8 offset is here
                    var normalOffset = LLVMValueRef.CreateConstInt(_context.Int32Type, (ulong)1);
                    // get ptr by offset
                    v = _builder.BuildGEP2(_context.Int64Type, v, new LLVMValueRef[] { normalOffset }, "offsetedV");

                    // cringe kostyl
                    var typeToSearch = structType;
                    // set up type data ptr!!!
                    SetTypeInfo(v, typeToSearch);

                    // storing struct into the alloced mem
                    _builder.BuildStore(val, v);

                    return v; // return malloced
                }
            }
            // ...
            return val;
        }

        private LLVMValueRef CreateStructCastFromObject(LLVMValueRef val, StructType structType, bool generateErrorOnInvalidCast = true)
        {
            // cast from object instance to struct

            var ptrToCastTypeInfo = _typeInfoDictionary[structType];

            // WARN: hard cock
            var typeConverter = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.Conversion", new AstIdExpr("TypeConverter"));
            DeclSymbol downcasterSymbol;
            downcasterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("CanBeDowncasted")) as DeclSymbol;
            var downcasterFunc = _valueMap[downcasterSymbol];
            LLVMTypeRef funcType = _typeMap[downcasterSymbol.Decl.Type.OutType];
            var canBeDowncasted = _builder.BuildCall2(funcType, downcasterFunc, new LLVMValueRef[] { val, ptrToCastTypeInfo }, "canBeDowncasted");

            // creating other blocks
            var bbTrue = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"cast.true");
            var bbFalse = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"cast.false");
            var bbEnd = _lastFunctionValueRef.AppendBasicBlockInContext(_context, $"cast.end");

            var v = _builder.BuildAlloca(HapetTypeToLLVMType(structType), $"tmp_{structType.Declaration.Name.Name}");
            _builder.BuildCondBr(canBeDowncasted, bbTrue, bbFalse);

            // if could be downcasted
            _builder.PositionAtEnd(bbTrue);
            // getting struct from the alloced mem
            var castedOffseted = _builder.BuildBitCast(val, HapetTypeToLLVMType(PointerType.GetPointerType(structType)), "offseted");
            var loadedData = _builder.BuildLoad2(HapetTypeToLLVMType(structType), castedOffseted, "loaded");
            _builder.BuildStore(loadedData, v);
            _builder.BuildBr(bbEnd);

            // if could not be downcasted
            _builder.PositionAtEnd(bbFalse);
            // check if we need to generate an runtime exception on invalid cast
            if (generateErrorOnInvalidCast)
            {
                // TODO: generate runtime error!!!
            }
            else
            {
                // set default expr to the var
                var castTypeDefault = GenerateEmptyStructExprCode(new AstEmptyStructExpr(structType));
                _builder.BuildStore(castTypeDefault, v);
            }
            _builder.BuildBr(bbEnd);

            _builder.PositionAtEnd(bbEnd);

            return _builder.BuildLoad2(HapetTypeToLLVMType(structType), v); // return loaded
        }

        /// <summary>
        /// Assigns value to a variable
        /// </summary>
        /// <param name="varPtr">The variable</param>
        /// <param name="varType">The type of variable</param>
        /// <param name="value">The value that needs to be assigned</param>
        private void AssignToVar(LLVMValueRef varPtr, AstExpression value)
        {
            // generate the initializer value
            var x = GenerateExpressionCode(value);
            AssignToVar(varPtr, x);
        }

        /// <summary>
        /// Assigns value to a variable
        /// </summary>
        /// <param name="varPtr">The variable</param>
        /// <param name="varType">The type of variable</param>
        /// <param name="value">The value that needs to be assigned</param>
        private void AssignToVar(LLVMValueRef varPtr, LLVMValueRef value)
        {
            // return if the value is cringe and was not generated properly
            if (value == default)
                return;
            // just storing initializer value in the var
            _builder.BuildStore(value, varPtr);
        }

        private LLVMTypeRef GetFunctionTypeOfDelegate(DelegateType del, bool isStaticCall = true)
        {
            var paramTypes = del.TargetDeclaration.Parameters.Select(rt => HapetTypeToLLVMType(rt.Type.OutType)).ToList();
            if (!isStaticCall)
                paramTypes.Insert(0, HapetTypeToLLVMType(HapetType.CurrentTypeContext.GetIntType(1, false)).GetPointerTo());
            var returnType = HapetTypeToLLVMType(del.TargetDeclaration.Returns.OutType);
            return LLVMTypeRef.CreateFunction(returnType, paramTypes.ToArray(), false);
        }

        private void SetTypeInfo(LLVMValueRef v, HapetType type)
        {
            // create an array of ptrs:
            // [ptrToTypeInfo, ptrToVtable]
            //
            //       example class
            //        -1    0    1
            //      { ptr, ..., ... }   
            //         ↓
            //      [ ptr, ptr ]
            //         |     \
            //         |      ↓
            //         ↓   "VirtualTableStruct"
            //  "TypeInfoStruct"

            // allocating memory for the data in array
            var allocated = GetMalloc(HapetType.CurrentTypeContext.PointerSize, 2, "allocForTypeInfo");
            var ptrToType = _builder.BuildGEP2(LLVMTypeRef.CreatePointer(GetTypeInfoType(), 0), allocated, new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0) }, $"elementPtr{0}");
            _builder.BuildStore(_typeInfoDictionary[type], ptrToType);
            var ptrToVtable = _builder.BuildGEP2(LLVMTypeRef.CreatePointer(GetVirtualTableType(), 0), allocated, new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1) }, $"elementPtr{1}");
            _builder.BuildStore(_virtualTableDictionary[type], ptrToVtable);

            // save the array into first field
            var typeConverter = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.Conversion", new AstIdExpr("TypeConverter"));
            DeclSymbol setterSymbol;
            setterSymbol = (typeConverter.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("SetTypeInfo")) as DeclSymbol;
            var setterFunc = _valueMap[setterSymbol];
            LLVMTypeRef funcType = _typeMap[setterSymbol.Decl.Type.OutType];
            _builder.BuildCall2(funcType, setterFunc, new LLVMValueRef[] { v, allocated });
        }

        #region Delegate shite
        private LLVMTypeRef GetDelegateAnonType(DelegateType delType)
        {
            return GetDelegateAnonTypeInternal(true, delType.TargetDeclaration.ToCringeString(),
                delType.TargetDeclaration.Parameters, delType.TargetDeclaration.Returns);
        }

        private LLVMTypeRef GetDelegateAnonType(LambdaType lambda)
        {
            return GetDelegateAnonTypeInternal(true, lambda.Declaration.ToCringeString(),
                lambda.Declaration.Parameters, lambda.Declaration.Returns);
        }

        private LLVMTypeRef GetDelegateAnonType(FunctionType fncType)
        {
            return GetDelegateAnonTypeInternal(fncType.IsStaticFunction(), fncType.Declaration.ToCringeString(), 
                fncType.Declaration.Parameters, fncType.Declaration.Returns);
        }

        private LLVMTypeRef GetDelegateAnonTypeInternal(bool isStatic, string cringeString, List<AstParamDecl> parameters, AstExpression returns)
        {
            return GetDelegateAnonTypeInternal(isStatic, cringeString, 
                parameters.Select(rt => HapetTypeToLLVMType(rt.Type.OutType)).ToList(), HapetTypeToLLVMType(returns.OutType));
        }

        private LLVMTypeRef GetDelegateAnonTypeInternal(bool isStatic, string cringeString, List<LLVMTypeRef> parameters, LLVMTypeRef returnType)
        {
            LLVMTypeRef delegateIrType;
            if (_delegateAnonTypes.TryGetValue(cringeString, out LLVMTypeRef irType))
            {
                delegateIrType = irType;
            }
            else
            {
                IEnumerable<LLVMTypeRef> paramTypes;
                // creating anon delegate type
                if (isStatic)
                {
                    // the func is static...
                    paramTypes = parameters;
                }
                else
                {
                    // the func is non-static...
                    // skip the first param with class object ptr
                    paramTypes = parameters.Skip(1);
                }
                var funcType = LLVMTypeRef.CreateFunction(returnType, paramTypes.ToArray(), false);

                // fields of delegate struct
                var objectPtr = HapetTypeToLLVMType(PointerType.GetPointerType(HapetType.CurrentTypeContext.GetIntType(1, false))); // ptr to func object
                var funcPtr = funcType.GetPointerTo();

                delegateIrType = _context.CreateNamedStruct($"delegate.anon.{cringeString}");
                delegateIrType.StructSetBody(new LLVMTypeRef[] {
                            ((LLVMTypeRef)funcPtr),
                            ((LLVMTypeRef)objectPtr)
                        }, false);
                _delegateAnonTypes[cringeString] = delegateIrType;
            }
            return delegateIrType;
        }

        private unsafe LLVMValueRef CreateDelegateFromFunction(FunctionType func, DeclSymbol declSymbol, LLVMValueRef ptrToObject)
        {
            // this whole shite is done to create anon delegate of the specified function
            LLVMTypeRef delegateIrType = GetDelegateAnonType(func);
            LLVMValueRef ptrToFunc = _valueMap[declSymbol]; // mb ptr to?
            return CreateDelegateInstanceInternal(delegateIrType, ptrToFunc, ptrToObject);
        }

        private unsafe LLVMValueRef CreateDelegateFromLambda(LambdaType lambda, LLVMValueRef ptrToObject)
        {
            // this whole shite is done to create anon delegate of the specified lambda
            LLVMTypeRef delegateIrType = GetDelegateAnonType(lambda);
            LLVMValueRef lfunc = _valueMap[lambda.Declaration.Symbol];
            return CreateDelegateInstanceInternal(delegateIrType, lfunc, ptrToObject);
        }

        private unsafe LLVMValueRef CreateDelegateInstanceInternal(LLVMTypeRef delegateIrType, LLVMValueRef ptrToFunc, LLVMValueRef ptrToObject)
        {
            var allocatedDelegate = _builder.BuildAlloca(delegateIrType, "anonAllocated");
            // the 1 is because delegate struct has object field as it's 1 param
            var objPtr = _builder.BuildStructGEP2(delegateIrType, allocatedDelegate, 1, "objectPtr");
            _builder.BuildStore(ptrToObject, objPtr);
            // setting the func ptr
            var funcPtrr = _builder.BuildStructGEP2(delegateIrType, allocatedDelegate, 0, "funcPtr");
            _builder.BuildStore(ptrToFunc, funcPtrr);

            return allocatedDelegate;
        }
        #endregion

        #region Mallocs
        private LLVMValueRef GetMalloc(int typeSize, int amount, string allocName = "allocated")
        {
            var tp = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(HapetType.CurrentTypeContext.GetIntType(4, true)), (ulong)typeSize);
            var am = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(HapetType.CurrentTypeContext.GetIntType(4, true)), (ulong)amount);
            return GetMalloc(tp, am, allocName);
        }

        private LLVMValueRef GetMalloc(LLVMValueRef typeSize, int amount, string allocName = "allocated")
        {
            var am = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(HapetType.CurrentTypeContext.GetIntType(4, true)), (ulong)amount);
            return GetMalloc(typeSize, am, allocName);
        }

        private LLVMValueRef GetMalloc(int typeSize, LLVMValueRef amount, string allocName = "allocated")
        {
            var tp = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(HapetType.CurrentTypeContext.GetIntType(4, true)), (ulong)typeSize);
            return GetMalloc(tp, amount, allocName);
        }

        private LLVMValueRef GetMalloc(LLVMValueRef typeSize, LLVMValueRef amount, string allocName = "allocated")
        {
            // WARN: hard cock
            var marshalDecl = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", new AstIdExpr("Marshal"));
            var mallocSymbol = (marshalDecl.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("Malloc")) as DeclSymbol;
            var mallocFunc = _valueMap[mallocSymbol];
            LLVMTypeRef funcType = _typeMap[mallocSymbol.Decl.Type.OutType];
            // calc size to malloc = amount * typeSize
            var sizeToMalloc = _builder.BuildMul(amount, typeSize, "sizeToMalloc");
            return _builder.BuildCall2(funcType, mallocFunc, new LLVMValueRef[] { sizeToMalloc }, allocName);
        }
        #endregion

        #region Memcmp
        private LLVMValueRef GetMemcmp(LLVMValueRef ptr1, LLVMValueRef ptr2, LLVMValueRef num, string name = "compared")
        {
            // WARN: hard cock
            var marshalDecl = _currentFunction.Scope.GetSymbolInNamespace("System.Runtime.InteropServices", new AstIdExpr("Marshal"));
            var memcmpSymbol = (marshalDecl.Decl as AstClassDecl).SubScope.GetSymbol(new AstIdExpr("System.Runtime.InteropServices.Marshal::Memcmp(void*:void*:uintptr)")) as DeclSymbol;
            var memcmpFunc = _valueMap[memcmpSymbol];
            LLVMTypeRef funcType = _typeMap[memcmpSymbol.Decl.Type.OutType];
            return _builder.BuildCall2(funcType, memcmpFunc, new LLVMValueRef[] { ptr1, ptr2, num }, name);
        }
        #endregion

        #region Variatic args
        private (LLVMTypeRef, LLVMValueRef)? _vaStartFunc;
        private (LLVMTypeRef, LLVMValueRef) GetVaStartFunc()
        {
            if (_vaStartFunc.HasValue)
                return _vaStartFunc.Value;

            var voidT = HapetType.CurrentTypeContext.VoidTypeInstance;
            List<LLVMTypeRef> paramTypes = [HapetTypeToLLVMType(PointerType.GetPointerType(voidT))];
            var returnType = HapetTypeToLLVMType(voidT);
            var funcType = LLVMTypeRef.CreateFunction(returnType, paramTypes.ToArray(), false);
            var funcValue = _module.AddFunction("llvm.va_start", funcType);
            funcValue.Linkage = LLVMLinkage.LLVMExternalLinkage;
            funcValue.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLImportStorageClass;

            _vaStartFunc = (funcType, funcValue);
            return _vaStartFunc.Value;
        }

        private (LLVMTypeRef, LLVMValueRef)? _vaEndFunc;
        private (LLVMTypeRef, LLVMValueRef) GetVaEndFunc()
        {
            if (_vaEndFunc.HasValue)
                return _vaEndFunc.Value;

            var voidT = HapetType.CurrentTypeContext.VoidTypeInstance;
            List<LLVMTypeRef> paramTypes = [HapetTypeToLLVMType(PointerType.GetPointerType(voidT))];
            var returnType = HapetTypeToLLVMType(voidT);
            var funcType = LLVMTypeRef.CreateFunction(returnType, paramTypes.ToArray(), false);
            var funcValue = _module.AddFunction("llvm.va_end", funcType);
            funcValue.Linkage = LLVMLinkage.LLVMExternalLinkage;
            funcValue.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLImportStorageClass;

            _vaEndFunc = (funcType, funcValue);
            return _vaEndFunc.Value;
        }
        #endregion
    }
}
