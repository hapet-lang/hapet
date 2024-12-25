using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using System.Numerics;

namespace HapetFrontend.Types
{
	public class VoidType : HapetType
	{
		public static VoidType Instance { get; } = new VoidType();
		public override string ToString() => "void";

		public override string TypeName => "void";

		private VoidType() : base(0, 0) { }
	}

	public class BoolType : HapetType
	{
		public static BoolType Instance { get; } = new BoolType();
		public override string ToString() => "bool";

		public override string TypeName => "bool";

		private BoolType() : base(1, 1) { }
	}

	public class IntType : HapetType
	{
		private static readonly Dictionary<(int, bool), IntType> _types = new Dictionary<(int, bool), IntType>();
		public static IntType LiteralType { get; } = new IntType(0, 0, false);
		public static IntType DefaultType => GetIntType(4, true);

		public override string TypeName => ToString();

		protected IntType(int size, int align, bool sign) : base(size, align)
		{
			Signed = sign;
		}

		public bool Signed { get; private set; }

		public static IntType GetIntType(int sizeInBytes, bool signed)
		{
			var key = (sizeInBytes, signed);

			if (_types.ContainsKey(key))
			{
				return _types[key];
			}

			var type = new IntType(sizeInBytes, sizeInBytes, signed);
			_types[key] = type;
			return type;
		}

		public override string ToString()
		{
			if (Signed)
			{
				switch (GetSize())
				{
					case 1:
						return "sbyte";
					case 2:
						return "short";
					case 4:
						return "int";
					default:
						return "long";
				}
			}
			else
			{
				switch (GetSize())
				{
					case 1:
						return "byte";
					case 2:
						return "ushort";
					case 4:
						return "uint";
					default:
						return "ulong";
				}
			}
		}

		public override int Match(HapetType concrete)
		{
			if (concrete is ReferenceType r)
				concrete = r.TargetType;

			if (concrete is IntType t)
			{
				if (t.Signed != this.Signed)
					return -1;

				if (concrete.GetSize() != this.GetSize())
					return -1;
				return 0;
			}
			return -1;
		}

		public BigInteger MinValue => (Signed, GetSize()) switch
		{
			(true, 1) => sbyte.MinValue,
			(true, 2) => short.MinValue,
			(true, 4) => int.MinValue,
			(true, 8) => long.MinValue,
			(false, 1) => byte.MinValue,
			(false, 2) => ushort.MinValue,
			(false, 4) => uint.MinValue,
			(false, 8) => ulong.MinValue,
			_ => throw new NotImplementedException()
		};

		public BigInteger MaxValue => (Signed, GetSize()) switch
		{
			(true, 1) => sbyte.MaxValue,
			(true, 2) => short.MaxValue,
			(true, 4) => int.MaxValue,
			(true, 8) => long.MaxValue,
			(false, 1) => byte.MaxValue,
			(false, 2) => ushort.MaxValue,
			(false, 4) => uint.MaxValue,
			(false, 8) => ulong.MaxValue,
			_ => throw new NotImplementedException()
		};
	}

	public class FloatType : HapetType
	{
		private static Dictionary<int, FloatType> _types = new Dictionary<int, FloatType>();
		public static FloatType LiteralType { get; } = new FloatType(0, 0);
		public static FloatType DefaultType => GetFloatType(4);

		public override string TypeName => ToString();

		private FloatType(int size, int align) : base(size, align) { }

		public static FloatType GetFloatType(int bytes)
		{
			if (_types.ContainsKey(bytes))
			{
				return _types[bytes];
			}

			var type = new FloatType(bytes, bytes);

			_types[bytes] = type;
			return type;
		}

		public override string ToString()
		{
			switch (GetSize())
			{
				case 2:
					return "half";
				case 4:
					return "float";
				case 8:
					return "double";
			}
			return "float";
		}

		public override int Match(HapetType concrete)
		{
			if (concrete is ReferenceType r)
				concrete = r.TargetType;

			if (concrete is FloatType t)
			{
				if (concrete.GetSize() > this.GetSize())
					return -1;
				if (concrete.GetSize() < this.GetSize())
					return 1;
				return 0;
			}
			return -1;
		}

		public double MinValue => GetSize() switch
		{
			2 => (double)Half.MinValue,
			4 => float.MinValue,
			8 => double.MinValue,
			_ => throw new NotImplementedException()
		};

		public double MaxValue => GetSize() switch
		{
			2 => (double)Half.MaxValue,
			4 => float.MaxValue,
			8 => double.MaxValue,
			_ => throw new NotImplementedException()
		};
		public double NaN => GetSize() switch
		{
			2 => (double)Half.NaN,
			4 => float.NaN,
			8 => double.NaN,
			_ => throw new NotImplementedException()
		};

		public double PosInf => GetSize() switch
		{
			2 => (double)Half.PositiveInfinity,
			4 => float.PositiveInfinity,
			8 => double.PositiveInfinity,
			_ => throw new InvalidOperationException()
		};

		public double NegInf => GetSize() switch
		{
			2 => (double)Half.NegativeInfinity,
			4 => float.NegativeInfinity,
			8 => double.NegativeInfinity,
			_ => throw new InvalidOperationException()
		};
	}

	public class PointerType : HapetType
	{
		public static int PointerAlignment => PointerSize;

		private static Dictionary<HapetType, PointerType> _types = new Dictionary<HapetType, PointerType>();
		public static PointerType NullLiteralType { get; } = new PointerType(null);

		public override string TypeName => "ptr";

		private PointerType(HapetType target) : base(
			target switch
			{
				_ => PointerSize * 1,
			}, PointerAlignment)
		{
			TargetType = target;
		}

		public HapetType TargetType { get; set; }

		public static PointerType GetPointerType(HapetType targetType)
		{
			if (targetType == null)
				return null;

			if (_types.ContainsKey((targetType)))
			{
				return _types[(targetType)];
			}

			var type = new PointerType(targetType);

			_types[(targetType)] = type;
			return type;
		}

		public override string ToString()
		{
			return $"{(TargetType == null ? "null" : TargetType)}*";
		}

		public override bool Equals(object obj)
		{
			if (obj is PointerType p)
			{
                return TargetType == p.TargetType;
            }
			return false;
		}

		public override int Match(HapetType concrete)
		{
			if (concrete is ReferenceType r)
				concrete = r.TargetType;

			if (concrete is PointerType p)
			{
				if (TargetType == null && p.TargetType == null)
					return 0;

				var targetMatch = this.TargetType.Match(p.TargetType);
				if (targetMatch == -1)
					return -1;
				return targetMatch;
			}
			return -1;
		}

		public override int GetHashCode()
		{
			var hashCode = -1663075914;
			hashCode = hashCode * -1521134295 + base.GetHashCode();
			hashCode = hashCode * -1521134295 + EqualityComparer<HapetType>.Default.GetHashCode(TargetType);
			return hashCode;
		}
	}

	public class ReferenceType : HapetType
	{
		private static Dictionary<HapetType, ReferenceType> _types = new Dictionary<HapetType, ReferenceType>();

		public HapetType TargetType { get; set; }

		public override string TypeName => "ref";

		private ReferenceType(HapetType target) : base(PointerType.PointerSize, PointerType.PointerAlignment)
		{
			TargetType = target;
		}

		public static ReferenceType GetRefType(HapetType targetType)
		{
			if (targetType == null)
				return null;

			if (_types.ContainsKey((targetType)))
			{
				return _types[(targetType)];
			}

			var type = new ReferenceType(targetType);

			_types[(targetType)] = type;
			return type;
		}

		public override string ToString()
		{
			return $"&{(TargetType == null ? "null" : TargetType)}";
		}

		public override int Match(HapetType concrete)
		{
			if (concrete is ReferenceType r)
			{
				var targetMatch = this.TargetType.Match(r.TargetType);
				if (targetMatch == -1)
					return -1;
				return targetMatch;
			}

			return -1;
		}

		public override bool Equals(object obj)
		{
			if (obj is ReferenceType r)
			{
				return TargetType == r.TargetType;
			}
			return false;
		}

		public override int GetHashCode()
		{
			var hashCode = -1663075914;
			hashCode = hashCode * -1521134295 + base.GetHashCode();
			hashCode = hashCode * -1521134295 + EqualityComparer<HapetType>.Default.GetHashCode(TargetType);
			return hashCode;
		}
	}

	public class ArrayType : HapetType
	{
		private static Dictionary<HapetType, ArrayType> _types = new Dictionary<HapetType, ArrayType>();

		public HapetType TargetType { get; set; }

		public override string TypeName => $"array";

		// array size is like that because of the array struct:
		// { int32, ptr }
		private ArrayType(HapetType target) : base(4 + PointerType.PointerSize, PointerType.PointerSize)
		{
			TargetType = target;
		}

		public static ArrayType GetArrayType(HapetType targetType)
		{
			if (targetType == null)
				return null;

			var existing = _types.FirstOrDefault(t => t.Value.TargetType == targetType).Value;
			if (existing != null)
				return existing;

			var type = new ArrayType(targetType);

			_types[targetType] = type;
			return type;
		}

		public override string ToString()
		{
			return $"{TargetType}[]";
		}

		public PointerType ToPointerType()
		{
			return PointerType.GetPointerType(TargetType);
		}

		public override int Match(HapetType concrete)
		{
			if (concrete is ArrayType p)
				return this.TargetType.Match(p.TargetType);
			return -1;
		}

		public override bool Equals(object obj)
		{
			if (obj is ArrayType r)
			{
				return TargetType == r.TargetType;
			}
			return false;
		}

		public override int GetHashCode()
		{
			var hashCode = -687864485;
			hashCode = hashCode * -1521134295 + base.GetHashCode();
			hashCode = hashCode * -1521134295 + EqualityComparer<HapetType>.Default.GetHashCode(TargetType);
			return hashCode;
		}
	}

	public class CharType : HapetType
	{
		private static Dictionary<int, CharType> _types = new Dictionary<int, CharType>();
		public static CharType LiteralType { get; } = new CharType(0);
		public static CharType DefaultType => GetCharType(2);

		public override string TypeName => "char";

		private CharType(int size) : base(size, size)
		{
		}

		private static CharType GetCharType(int sizeInBytes)
		{
			var key = sizeInBytes;

			if (_types.ContainsKey(key))
			{
				return _types[key];
			}

			var type = new CharType(sizeInBytes);
			_types[key] = type;
			return type;
		}

		public override string ToString()
		{
			return "char";
		}

		public override int Match(HapetType concrete)
		{
			if (concrete is ReferenceType r)
				concrete = r.TargetType;

			if (concrete is CharType)
			{
				if (concrete.GetSize() != this.GetSize())
					return -1;
				return 0;
			}
			return -1;
		}

		public BigInteger MinValue => GetSize() switch
		{
			2 => ushort.MinValue,
			_ => throw new NotImplementedException()
		};

		public BigInteger MaxValue => GetSize() switch
		{
			2 => ushort.MaxValue,
			_ => throw new NotImplementedException()
		};
	}

	public class StringType : HapetType
	{
		private static StringType _instance;
		public static StringType Instance 
		{ 
			get 
			{ 
				// string size is like that because of the string struct:
				// { int32, ptr }
				if (_instance == null)
					_instance = new StringType(4 + PointerType.PointerSize, PointerType.PointerSize);
				return _instance; 
			} 
		} 
		public static StringType LiteralType { get; } = new StringType(0, 0);

		public override string TypeName => "string";

		private StringType(int size, int align) : base(size, align) { }
		public override string ToString() => "string";
	}

    public class IntPtrType : IntType
    {
        private static IntPtrType _instance;
        public static IntPtrType Instance
        {
            get
            {
                // intptr size is like that because of the intptr struct:
                // { ptr }
                if (_instance == null)
                    _instance = new IntPtrType(PointerType.PointerSize, PointerType.PointerSize);
                return _instance;
            }
        }

        public override string TypeName => "uintptr";

        private IntPtrType(int size, int align) : base(size, align, false) { }
        public override string ToString() => "uintptr";
    }

    public class PtrDiffType : IntType
    {
        private static PtrDiffType _instance;
        public static PtrDiffType Instance
        {
            get
            {
                // ptrdiff size is like that because of the ptrdiff struct:
                // { ptr }
                if (_instance == null)
                    _instance = new PtrDiffType(PointerType.PointerSize, PointerType.PointerSize);
                return _instance;
            }
        }

        public override string TypeName => "ptrdiff";

        private PtrDiffType(int size, int align) : base(size, align, true) { }
        public override string ToString() => "ptrdiff";
    }
}
