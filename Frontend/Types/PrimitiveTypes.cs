using Frontend.Parsing.Entities;
using System.Numerics;
using System.Security.AccessControl;

namespace Frontend.Types
{
	public class VoidType : HapetType
	{
		public static VoidType Instance { get; } = new VoidType();
		public override string ToString() => "void";

		private VoidType() : base(0, 1) { }
	}

	public class BoolType : HapetType
	{
		public static BoolType Instance { get; } = new BoolType();
		public override string ToString() => "bool";

		private BoolType() : base(1, 1) { }
	}

	public class IntType : HapetType
	{
		private static readonly Dictionary<(int, bool), IntType> _types = new Dictionary<(int, bool), IntType>();
		public static IntType LiteralType { get; } = new IntType(0, false);
		public static IntType DefaultType => GetIntType(4, true);

		private IntType(int size, bool sign) : base(size, size)
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

			var type = new IntType(sizeInBytes, signed);
			_types[key] = type;
			return type;
		}

		public override string ToString()
		{
			return (Signed ? "Int" : "UInt") + (GetSize() * 8);
		}

		public override int Match(HapetType concrete, Dictionary<string, (HapetType type, object value)> genericTypes)
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
		public static FloatType LiteralType { get; } = new FloatType(0);
		public static FloatType DefaultType => GetFloatType(4);
		public override bool IsGenericType => false;

		private FloatType(int size) : base(size, size) { }

		public static FloatType GetFloatType(int bytes)
		{
			if (_types.ContainsKey(bytes))
			{
				return _types[bytes];
			}

			var type = new FloatType(bytes);
			_types[bytes] = type;
			return type;
		}

		public override string ToString()
		{
			switch (GetSize())
			{
				case 2:
					return "Half";
				case 4:
					return "Float";
				case 8:
					return "Double";
			}
			return "Float";
		}

		public override int Match(HapetType concrete, Dictionary<string, (HapetType type, object value)> genericTypes)
		{
			if (concrete is ReferenceType r)
				concrete = r.TargetType;

			if (concrete is FloatType)
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
#warning probably depends on a platform
		public const int PointerSize = 8;
		public const int PointerAlignment = 8;

		private static Dictionary<HapetType, PointerType> _types = new Dictionary<HapetType, PointerType>();
		public static PointerType NullLiteralType { get; } = new PointerType(null);

		private PointerType(HapetType target) : base(
		target switch
			{
				ClassType t => PointerSize * 2,
				_ => PointerSize * 1,
			}, PointerAlignment)
		{
			TargetType = target;
			IsFatPointer = target is ClassType;
		}

		public bool IsFatPointer { get; set; }

		public HapetType TargetType { get; set; }
		public override bool IsGenericType => TargetType?.IsGenericType ?? false;

		public static PointerType GetPointerType(HapetType targetType)
		{
			if (targetType == null)
				return null;

			if (_types.ContainsKey(targetType))
			{
				return _types[targetType];
			}

			var type = new PointerType(targetType);
			_types[targetType] = type;
			return type;
		}

		public override string ToString()
		{
			return $"*{TargetType}";
		}

		public override bool Equals(object obj)
		{
			if (obj is PointerType p)
				return TargetType == p.TargetType;
			return false;
		}

		public override int GetHashCode()
		{
			var hashCode = -1663075914;
			hashCode = hashCode * -1521134295 + base.GetHashCode();
			hashCode = hashCode * -1521134295 + EqualityComparer<HapetType>.Default.GetHashCode(TargetType);
			return hashCode;
		}

		public override int Match(HapetType concrete, Dictionary<string, (HapetType type, object value)> genericTypes)
		{
			if (concrete is ReferenceType r)
				concrete = r.TargetType;

			if (concrete is PointerType p)
			{
				var targetMatch = this.TargetType.Match(p.TargetType, genericTypes);
				return targetMatch;
			}
			return -1;
		}
	}

	public class ReferenceType : HapetType
	{
		private static Dictionary<HapetType, ReferenceType> _types = new Dictionary<HapetType, ReferenceType>();

		public HapetType TargetType { get; set; }
		public override bool IsGenericType => TargetType.IsGenericType;

		private ReferenceType(HapetType target) : base(target is ClassType ? PointerType.PointerSize * 2 : PointerType.PointerSize, PointerType.PointerAlignment)
		{
			TargetType = target;
			IsFatReference = target is ClassType;
		}

		public bool IsFatReference { get; set; }

		public static ReferenceType GetRefType(HapetType targetType)
		{
			if (targetType == null)
				return null;

			if (_types.ContainsKey(targetType))
			{
				return _types[targetType];
			}

			var type = new ReferenceType(targetType);
			_types[targetType] = type;
			return type;
		}

		public override string ToString()
		{
			return $"&{TargetType}";
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

		public override int Match(HapetType concrete, Dictionary<string, (HapetType type, object value)> genericTypes)
		{
			if (concrete is ReferenceType r)
			{
				var targetMatch = this.TargetType.Match(r.TargetType, genericTypes);
				return targetMatch;
			}
			return -1;
		}
	}

	public class ArrayType : HapetType
	{
		private static Dictionary<HapetType, ArrayType> _types = new Dictionary<HapetType, ArrayType>();

		public HapetType TargetType { get; set; }
		public object Length { get; private set; }
		public override bool IsGenericType => TargetType.IsGenericType;

		private ArrayType(HapetType target, object length) : base()
		{
			TargetType = target;
			Length = length;
		}

		public static ArrayType GetArrayType(HapetType targetType, int length)
		{
			return GetArrayType(targetType, NumberData.FromBigInt(length));
		}

		public static ArrayType GetArrayType(HapetType targetType, object length)
		{
			if (targetType == null)
				return null;

			var existing = _types.FirstOrDefault(t => t.Value.TargetType == targetType && t.Value.Length.Equals(length)).Value;
			if (existing != null)
				return existing;

			var type = new ArrayType(targetType, length);
			_types[targetType] = type;
			return type;
		}

		public override string ToString()
		{
			return $"{TargetType}[{Length}]";
		}

		public PointerType ToPointerType()
		{
			return PointerType.GetPointerType(TargetType);
		}

		public override bool Equals(object obj)
		{
			if (obj is ArrayType r)
			{
				return TargetType == r.TargetType && Length.Equals(r.Length);
			}
			return false;
		}

		public override int GetHashCode()
		{
			var hashCode = -687864485;
			hashCode = hashCode * -1521134295 + base.GetHashCode();
			hashCode = hashCode * -1521134295 + EqualityComparer<HapetType>.Default.GetHashCode(TargetType);
			hashCode = hashCode * -1521134295 + Length.GetHashCode();
			return hashCode;
		}

		public override int Match(HapetType concrete, Dictionary<string, (HapetType type, object value)> genericTypes)
		{
			if (concrete is ArrayType p && Length.Equals(p.Length))
				return this.TargetType.Match(p.TargetType, genericTypes);
			return -1;
		}
	}

	public class RangeType : HapetType
	{
		private static Dictionary<HapetType, RangeType> _types = new Dictionary<HapetType, RangeType>();

		public HapetType TargetType { get; set; }
		public override bool IsGenericType => TargetType.IsGenericType;

		public static RangeType GetRangeType(HapetType targetType)
		{
			if (targetType == null)
				return null;

			if (_types.ContainsKey(targetType))
				return _types[targetType];

			var type = new RangeType
			{
				TargetType = targetType
			};

			_types[targetType] = type;
			return type;
		}

		public override string ToString()
		{
			return $"{TargetType}..{TargetType}";
		}

		public override bool Equals(object obj)
		{
			if (obj is RangeType r)
			{
				return TargetType == r.TargetType;
			}
			return false;
		}

		public override int GetHashCode()
		{
			var hashCode = -1576707978;
			hashCode = hashCode * -1521134295 + base.GetHashCode();
			hashCode = hashCode * -1521134295 + EqualityComparer<HapetType>.Default.GetHashCode(TargetType);
			hashCode = hashCode * -1521134295 + IsGenericType.GetHashCode();
			return hashCode;
		}

		public override int Match(HapetType concrete, Dictionary<string, (HapetType type, object value)> genericTypes)
		{
			if (concrete is RangeType p)
				return this.TargetType.Match(p.TargetType, genericTypes);
			return -1;
		}
	}

	public class CharType : HapetType
	{
		private static Dictionary<int, CharType> _types = new Dictionary<int, CharType>();
		public static CharType DefaultType => GetCharType();

		private CharType(int size) : base(size, size)
		{
		}

		public override bool IsGenericType => false;

		public static CharType GetCharType()
		{
			// ATTENTION: char size is always 1 byte
			int sizeInBytes = 1;

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

		public override int Match(HapetType concrete, Dictionary<string, (HapetType type, object value)> genericTypes)
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
			1 => byte.MinValue,
			2 => ushort.MinValue,
			4 => uint.MinValue,
			_ => throw new NotImplementedException()
		};

		public BigInteger MaxValue => GetSize() switch
		{
			1 => byte.MaxValue,
			2 => ushort.MaxValue,
			4 => uint.MaxValue,
			_ => throw new NotImplementedException()
		};
	}

	public class StringType : HapetType
	{
		public static StringType Instance { get; } = new StringType();

		public override bool IsGenericType => false;

		// ATTENTION: pointer size is 16
		private StringType() : base(16, 8) { }
		public override string ToString() => "string";
	}
}
