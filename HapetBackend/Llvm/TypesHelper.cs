using HapetFrontend.Ast;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using LLVMSharp.Interop;
using System;
using System.Diagnostics;

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

		// cringe
		// this is because all arrays are the same in LLVM IR
		private LLVMTypeRef _llvmArrayType = null;

        // rtti stuff
        private HapetType sTypeInfoAttribute;
		private ClassType sTypeInfo;
		private HapetType sTypeInfoInt;
		private HapetType sTypeInfoVoid;
		private HapetType sTypeInfoFloat;
		private HapetType sTypeInfoBool;
		private HapetType sTypeInfoChar;
		private HapetType sTypeInfoString;
		private HapetType sTypeInfoPointer;
		private HapetType sTypeInfoReference;
		private HapetType sTypeInfoArray;
		private HapetType sTypeInfoTuple;
		private HapetType sTypeInfoFunction;
		private HapetType sTypeInfoStruct;
		private HapetType sTypeInfoEnum;
		private HapetType sTypeInfoClass;
		private HapetType sTypeInfoType;
		private HapetType sTypeInfoCode;

		private LLVMTypeRef rttiTypeInfoAttribute;
		private LLVMTypeRef rttiTypeInfoPtr;
		private LLVMTypeRef rttiTypeInfoRef;
		private LLVMTypeRef rttiTypeInfoInt;
		private LLVMTypeRef rttiTypeInfoVoid;
		private LLVMTypeRef rttiTypeInfoFloat;
		private LLVMTypeRef rttiTypeInfoBool;
		private LLVMTypeRef rttiTypeInfoChar;
		private LLVMTypeRef rttiTypeInfoString;
		private LLVMTypeRef rttiTypeInfoPointer;
		private LLVMTypeRef rttiTypeInfoReference;
		private LLVMTypeRef rttiTypeInfoArray;
		private LLVMTypeRef rttiTypeInfoTuple;
		private LLVMTypeRef rttiTypeInfoFunction;
		private LLVMTypeRef rttiTypeInfoStruct;
		private LLVMTypeRef rttiTypeInfoEnum;
		private LLVMTypeRef rttiTypeInfoClass;
		private LLVMTypeRef rttiTypeInfoType;
		private LLVMTypeRef rttiTypeInfoCode;

		private void InitTypeInfoLLVMTypes()
		{
			sTypeInfoAttribute = _compiler.GlobalScope.GetStruct("TypeInfoAttribute").Type.OutType;
			sTypeInfo = _compiler.GlobalScope.GetClass("TypeInfo").Type.OutType as ClassType;
			sTypeInfoInt = _compiler.GlobalScope.GetStruct("TypeInfoInt").Type.OutType;
			sTypeInfoVoid = _compiler.GlobalScope.GetStruct("TypeInfoVoid").Type.OutType;
			sTypeInfoFloat = _compiler.GlobalScope.GetStruct("TypeInfoFloat").Type.OutType;
			sTypeInfoBool = _compiler.GlobalScope.GetStruct("TypeInfoBool").Type.OutType;
			sTypeInfoChar = _compiler.GlobalScope.GetStruct("TypeInfoChar").Type.OutType;
			sTypeInfoString = _compiler.GlobalScope.GetStruct("TypeInfoString").Type.OutType;
			sTypeInfoPointer = _compiler.GlobalScope.GetStruct("TypeInfoPointer").Type.OutType;
			sTypeInfoReference = _compiler.GlobalScope.GetStruct("TypeInfoReference").Type.OutType;
			sTypeInfoArray = _compiler.GlobalScope.GetStruct("TypeInfoArray").Type.OutType;
			sTypeInfoTuple = _compiler.GlobalScope.GetStruct("TypeInfoTuple").Type.OutType;
			sTypeInfoFunction = _compiler.GlobalScope.GetStruct("TypeInfoFunction").Type.OutType;
			sTypeInfoStruct = _compiler.GlobalScope.GetStruct("TypeInfoStruct").Type.OutType;
			sTypeInfoEnum = _compiler.GlobalScope.GetStruct("TypeInfoEnum").Type.OutType;
			sTypeInfoClass = _compiler.GlobalScope.GetStruct("TypeInfoClass").Type.OutType;
			sTypeInfoType = _compiler.GlobalScope.GetStruct("TypeInfoType").Type.OutType;
			sTypeInfoCode = _compiler.GlobalScope.GetStruct("TypeInfoCode").Type.OutType;

			rttiTypeInfoPtr = HapetTypeToLLVMType(PointerType.GetPointerType(sTypeInfo));
			rttiTypeInfoRef = HapetTypeToLLVMType(ReferenceType.GetRefType(sTypeInfo));
			rttiTypeInfoInt = HapetTypeToLLVMType(sTypeInfoInt);
			rttiTypeInfoVoid = HapetTypeToLLVMType(sTypeInfoVoid);
			rttiTypeInfoFloat = HapetTypeToLLVMType(sTypeInfoFloat);
			rttiTypeInfoBool = HapetTypeToLLVMType(sTypeInfoBool);
			rttiTypeInfoChar = HapetTypeToLLVMType(sTypeInfoChar);
			rttiTypeInfoString = HapetTypeToLLVMType(sTypeInfoString);
			rttiTypeInfoPointer = HapetTypeToLLVMType(sTypeInfoPointer);
			rttiTypeInfoReference = HapetTypeToLLVMType(sTypeInfoReference);
			rttiTypeInfoArray = HapetTypeToLLVMType(sTypeInfoArray);
			rttiTypeInfoTuple = HapetTypeToLLVMType(sTypeInfoTuple);
			rttiTypeInfoFunction = HapetTypeToLLVMType(sTypeInfoFunction);
			rttiTypeInfoStruct = HapetTypeToLLVMType(sTypeInfoStruct);
			rttiTypeInfoEnum = HapetTypeToLLVMType(sTypeInfoEnum);
			rttiTypeInfoClass = HapetTypeToLLVMType(sTypeInfoClass);
			rttiTypeInfoAttribute = HapetTypeToLLVMType(sTypeInfoAttribute);
			rttiTypeInfoType = HapetTypeToLLVMType(sTypeInfoType);
			rttiTypeInfoCode = HapetTypeToLLVMType(sTypeInfoCode);
		}

		private HapetType NormalizeType(HapetType type)
		{
			return type switch
			{
				// TODO: do i need it???
				//ReferenceType r => ReferenceType.GetRefType(NormalizeType(r.TargetType)),
				//PointerType r => PointerType.GetPointerType(NormalizeType(r.TargetType)),
				//ArrayType r => ArrayType.GetArrayType(NormalizeType(r.TargetType), r.Length),
				//TupleType r => TupleType.GetTuple(r.Members.Select(m => (NormalizeType(m.type), m.name)).ToArray()),
				//FunctionType r => new FunctionType(r.Declaration.Parameters.Select(m => (m.name, NormalizeType(m.type), m.defaultValue)).ToArray(), NormalizeType(r.ReturnType)),
				_ => type,
			};
		}

		private LLVMTypeRef HapetTypeToLLVMType(HapetType ht)
		{
			ht = NormalizeType(ht);

			if (_typeMap.TryGetValue(ht, out var tt)) 
				return tt;

			var t = HapetTypeToLLVMTypeHelper(ht);
			_typeMap[ht] = t;
			return t;
		}

		private unsafe LLVMTypeRef HapetTypeToLLVMTypeHelper(HapetType ht)
		{
			switch (ht)
			{
				case StringType s:
					{
						var charType = HapetTypeToLLVMType(CharType.DefaultType);
						var str = _context.CreateNamedStruct("string.type");
						str.StructSetBody(new LLVMTypeRef[] {
							((LLVMTypeRef)_context.Int32Type),
							((LLVMTypeRef)charType).GetPointerTo()
						}, false);
						return str;
					}

				case ArrayType a:
					{
						// because all array types are the same in LLVM IR
						if (_llvmArrayType == null)
						{
							var arrayStruct = _context.CreateNamedStruct($"array.type");
							var arrayType = HapetTypeToLLVMType(a.TargetType);
							arrayStruct.StructSetBody(new LLVMTypeRef[] { _context.Int32Type, arrayType.GetPointerTo() }, false);
							_llvmArrayType = arrayStruct;
						}
						return _llvmArrayType;
					}

				case ClassType t:
					{
						Console.WriteLine($"[ERROR] class type {t}");
						return _context.VoidType;
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
						// TODO: check it
						//if (p.TargetType == CheezType.Any)
						//{
						//	var str = LLVM.StructCreateNamed(context, $"^{p.TargetType.ToString()}");
						//	LLVM.StructSetBody(str, new LLVMTypeRef[] {
						//		LLVM.Int8Type().GetPointerTo(),
						//		rttiTypeInfoPtr
						//	}, false);
						//	return str;
						//}
						//else if (p.TargetType is TraitType)
						//{
						//	var str = LLVM.StructCreateNamed(context, $"^{p.TargetType.ToString()}");
						//	LLVM.StructSetBody(str, new LLVMTypeRef[] {
						//		LLVM.Int8Type().GetPointerTo(),
						//		LLVM.Int8Type().GetPointerTo()
						//	}, false);
						//	return str;
						//}
						//else
						//{
						//	if (p.TargetType == VoidType.Instance)
						//		return LLVM.Int8Type().GetPointerTo();
						//	return HapetTypeToLLVMType(p.TargetType).GetPointerTo();
						//}

						if (p.TargetType == VoidType.Instance)
							return ((LLVMTypeRef)_context.Int8Type).GetPointerTo();
						return HapetTypeToLLVMType(p.TargetType).GetPointerTo();
					}

				case ReferenceType r:
					{
						// TODO: check it
						//if (r.TargetType == CheezType.Any)
						//{
						//	var str = LLVM.StructCreateNamed(context, $"&{r.TargetType.ToString()}");
						//	LLVM.StructSetBody(str, new LLVMTypeRef[] {
						//		LLVM.Int8Type().GetPointerTo(),
						//		rttiTypeInfoPtr
						//	}, false);
						//	return str;
						//}
						//else if (r.TargetType is TraitType)
						//{
						//	var str = LLVM.StructCreateNamed(context, $"&{r.TargetType.ToString()}");
						//	LLVM.StructSetBody(str, new LLVMTypeRef[] {
						//		LLVM.Int8Type().GetPointerTo(),
						//		LLVM.Int8Type().GetPointerTo()
						//	}, false);
						//	return str;
						//}
						//else
						//{
						//	return HapetTypeToLLVMType(r.TargetType).GetPointerTo();
						//}

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

					// TODO: check it
				//case EnumType e:
				//	{
				//		var llvmType = _context.CreateNamedStruct($"enum.{e}");

				//		// TODO: here was some shite with tags
				//		var restSize = e.GetSize();
				//		llvmType.StructSetBody(new LLVMTypeRef[]
				//		{
				//			LLVM.ArrayType(LLVM.Int8Type(), (uint)0),
				//			LLVM.ArrayType(LLVM.Int8Type(), (uint)restSize)
				//		}, false);

				//		return llvmType;
				//	}

				//case StructType s:
				//	{
				//		var name = $"struct.{s.Declaration.Name.Name}";

				//		var llvmType = _context.CreateNamedStruct(name);
				//		typeMap[s] = llvmType;

				//		var memTypes = new List<LLVMTypeRef>(s.Declaration.Declarations.Count);
				//		var offsets = new uint[s.Declaration.Declarations.Count];
				//		int currentSize = 0;
				//		int i = 0;
				//		foreach (var mem in s.Declaration.Declarations)
				//		{
				//			if (currentSize % mem.Type.OutType.GetAlignment() != 0)
				//			{
				//				// add padding
				//				int padding = mem.Type.OutType.GetAlignment() - currentSize % mem.Type.OutType.GetAlignment();
				//				memTypes.Add(LLVM.ArrayType(LLVM.Int8Type(), (uint)padding));
				//				currentSize += padding;
				//			}

				//			offsets[i] = (uint)memTypes.Count;
				//			memTypes.Add(HapetTypeToLLVMType(mem.Type.OutType));
				//			currentSize += (int)((_targetData.SizeOfTypeInBits(memTypes.Last()) + 7) / 8);
				//			i += 1;
				//		}
				//		if (currentSize % s.GetAlignment() != 0)
				//		{
				//			// add padding
				//			int padding = s.GetAlignment() - currentSize % s.GetAlignment();
				//			memTypes.Add(LLVM.ArrayType(LLVM.Int8Type(), (uint)padding));
				//			currentSize += padding;
				//		}

				//		structMemberOffsets[s] = offsets;

				//		llvmType.StructSetBody(memTypes.ToArray(), false);

				//		// TODO: offsets checks
				//		//foreach (var m in s.Declaration.Declarations)
				//		//{
				//		//	int myOffset = m.Type.Offset;
				//		//	int llvmOffset = (int)LLVM.OffsetOfElement(_targetData, llvmType, offsets[m.Index]);

				//		//	if (myOffset != llvmOffset)
				//		//	{
				//		//		System.Console.WriteLine($"[ERROR] {s.Declaration.Name}: offset mismatch at {m.Index}: cheez {myOffset}, llvm {llvmOffset}");
				//		//	}
				//		//}

				//		if (_targetData.SizeOfTypeInBits(llvmType) / 8ul != (ulong)s.GetSize())
				//		{
				//			System.Console.WriteLine($"[ERROR] {s.Declaration.Name}: struct size mismatch: cheez {s.GetSize()}, llvm {_targetData.SizeOfTypeInBits(llvmType) / 8}");
				//		}

				//		if (_targetData.PreferredAlignmentOfType(llvmType) != (uint)s.GetAlignment()) // TODO: here was a directive check
				//		{
				//			System.Console.WriteLine($"[WARNING] {s.Declaration.Name}: struct alignment mismatch: cheez {s.GetAlignment()}, llvm {_targetData.PreferredAlignmentOfType(llvmType)}");
				//		}

				//		return llvmType;
				//	}

				case TupleType t:
					{
						var memTypes = t.Members.Select(m => HapetTypeToLLVMType(m.type)).ToArray();
						return _context.GetStructType(memTypes, false);
					}

				default:
					throw new NotImplementedException(ht.ToString());
			}
		}

		private LLVMValueRef _lastStringSizeValueRef = default;
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
						string theString = (string)v;
						_lastStringSizeValueRef = LLVMValueRef.CreateConstInt(HapetTypeToLLVMType(IntType.GetIntType(4, true)), (ulong)theString.Length);

						var elements = theString.ToCharArray().Select(c => HapetValueToLLVMValue(CharType.DefaultType, c)).ToArray();
						var stringGlobArray = _module.AddGlobal(LLVMTypeRef.CreateArray(HapetTypeToLLVMType(CharType.DefaultType), (uint)theString.Length), "constString");
						stringGlobArray.Initializer = LLVMValueRef.CreateConstArray(HapetTypeToLLVMType(CharType.DefaultType), elements);
						return stringGlobArray;
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
				Debug.Assert(alignment >= llvmAlignment && alignment % llvmAlignment == 0);
				result.SetAlignment((uint)alignment);
			}

			return result;
		}

		private LLVMValueRef CreateCast(LLVMValueRef val, HapetType inType, HapetType outType)
		{
			if (inType is IntType intType && intType.Signed)
			{
				if (outType is FloatType floatType)
				{
					return _builder.BuildSIToFP(val, HapetTypeToLLVMType(floatType));
				}
				else if (outType is IntType || outType is CharType)
				{
					if (inType.GetSize() > outType.GetSize())
					{
						return _builder.BuildTruncOrBitCast(val, HapetTypeToLLVMType(outType));
					}
					else if (inType.GetSize() < outType.GetSize())
					{
						// for signed and unsigned they are the same
						return _builder.BuildSExtOrBitCast(val, HapetTypeToLLVMType(outType));
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
					return _builder.BuildUIToFP(val, HapetTypeToLLVMType(floatType));
				}
				else if (outType is IntType || outType is CharType)
				{
					if (inType.GetSize() > outType.GetSize())
					{
						return _builder.BuildTruncOrBitCast(val, HapetTypeToLLVMType(outType));
					}
					else if (inType.GetSize() < outType.GetSize())
					{
						return _builder.BuildZExtOrBitCast(val, HapetTypeToLLVMType(outType));
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
					return _builder.BuildFPToSI(val, HapetTypeToLLVMType(intType1));
				}
				else if ((outType is IntType intType3 && !intType3.Signed) || outType is CharType)
				{
					return _builder.BuildFPToUI(val, HapetTypeToLLVMType(outType));
				}
				else if (outType is FloatType floatType)
				{
					if (inType.GetSize() > floatType.GetSize())
					{
						return _builder.BuildFPTrunc(val, HapetTypeToLLVMType(floatType));
					}
					else
					{
						return _builder.BuildFPExt(val, HapetTypeToLLVMType(floatType));
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
			// TODO: refactor similar code...
			if (varType is StringType && value.OutType is StringType && value.OutValue != null)
			{
				// generate the initializer value
				var x = GenerateExpressionCode(value);

				// it would work only with const string assignment. if you want smth like this to work
				// if they are trying to store a char* in string
				var tp = _typeMap[varType];
				// the 1 is because StringType struct has buf field as it's 1 param
				var buf = _builder.BuildStructGEP2(tp, varPtr, 1, "strBuf");
				_builder.BuildStore(x, buf);
				/// setting the string size. <see cref="_lastStringSizeInt"/> is set in <see cref="HapetValueToLLVMValue"/>
				var len = _builder.BuildStructGEP2(tp, varPtr, 0, "strLen");
				_builder.BuildStore(_lastStringSizeValueRef, len);
			}
			else
			{
				// generate the initializer value
				var x = GenerateExpressionCode(value);
				// just storing initializer value in the var
				_builder.BuildStore(x, varPtr);
			}
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
			var mallocSymbol = _currentFunction.Scope.GetSymbol("malloc") as DeclSymbol; // TODO: rewrite it when there would be a default project of Hapet
			var mallocFunc = _valueMap[mallocSymbol];
			LLVMTypeRef funcType = _typeMap[mallocSymbol.Decl.Type.OutType];
			// calc size to malloc = amount * typeSize
			var sizeToMalloc = _builder.BuildMul(amount, typeSize, "sizeToMalloc");
			return _builder.BuildCall2(funcType, mallocFunc, new LLVMValueRef[] { sizeToMalloc }, "allocated");
		}
		#endregion
	}
}
