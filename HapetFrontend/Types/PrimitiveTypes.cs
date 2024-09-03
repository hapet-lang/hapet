using System.Numerics;

namespace HapetFrontend.Types
{
	public class VoidType : HapetType
	{
		public static VoidType Instance { get; } = new VoidType();
		public override string ToString() => "void";

		private VoidType() : base(0, 0) { }
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

#warning align as size when it is 8 probably a bad idea because of x86 (should be checked somehow)
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
}
