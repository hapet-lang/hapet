namespace Frontend.Types
{
	public class ClassType : HapetType
	{
		public override bool IsGenericType => GenericParams?.Any(a => a.IsGenericType) ?? false;

		public HapetType[] GenericParams { get; }

		public ClassType(HapetType[] genericParams) : base()
		{
			GenericParams = genericParams;
		}

		public override string ToString()
		{
			if (GenericParams?.Length > 0)
			{
				var args = string.Join(", ", GenericParams.Select(a => a.ToString()));
				return $"class<{args}>";
			}
			return "class";
		}

		public override int Match(HapetType concrete, Dictionary<string, (HapetType type, object value)> genericTypes)
		{
			if (concrete is ClassType str)
			{
				int score = 0;
				for (int i = 0; i < GenericParams.Length; i++)
				{
					int s = HapetType.GenericValuesMatch((null, this.GenericParams[i]), (null, str.GenericParams[i]), genericTypes);
					if (s == -1)
						return -1;
					score += s;
				}
				return score;
			}
			return -1;
		}
	}

	public class TupleType : HapetType
	{
		public static readonly TupleType UnitLiteral = GetTuple(Array.Empty<HapetType>());

		public HapetType[] Members { get; }
		public override bool IsGenericType => Members.Any(m => m.IsGenericType);

		private TupleType(HapetType[] members) : base()
		{
			Members = members;
		}

		public static TupleType GetTuple(HapetType[] members)
		{
			return new TupleType(members);
		}

		public override string ToString()
		{
			var members = string.Join(", ", Members.Select(m =>
			{
				return m.ToString();
			}));
			return "(" + members + ")";
		}

		public override bool Equals(object obj)
		{
			if (obj is TupleType t)
			{
				if (Members.Length != t.Members.Length) return false;
				for (int i = 0; i < Members.Length; i++)
					if (Members[i] != t.Members[i]) return false;

				return true;
			}

			return false;
		}

		public override int GetHashCode()
		{
			var hash = new HashCode();
			foreach (var m in Members)
			{
				hash.Add(m.GetHashCode());
			}

			return hash.ToHashCode();
		}

		public override int Match(HapetType concrete, Dictionary<string, (HapetType type, object value)> genericTypes)
		{
			if (concrete is TupleType str)
			{
				int score = 0;
				for (int i = 0; i < Members.Length; i++)
				{
					int s = HapetType.GenericValuesMatch((null, Members[i]), (null, Members[i]), genericTypes);
					if (s == -1)
						return -1;
					score += s;
				}
				return score;
			}
			return -1;
		}
	}

	public class StructType : HapetType
	{
		public override bool IsGenericType => GenericParams?.Any(a => a is HapetType t && t.IsGenericType) ?? false;

		public HapetType[] GenericParams { get; }

		public StructType(HapetType[] genericParams) : base()
		{
			GenericParams = genericParams;
		}

		public override string ToString()
		{
			if (GenericParams?.Length > 0)
			{
				var args = string.Join(", ", GenericParams.Select(a => a?.ToString()));
				return $"struct<{args}>";
			}
			return "struct";
		}

		public override bool Equals(object obj)
		{
			if (obj is StructType s)
			{
				if (GenericParams?.Length != s.GenericParams?.Length)
					return false;

				for (int i = 0; i < GenericParams?.Length; i++)
				{
					if (GenericParams[i] != s.GenericParams[i])
						return false;
				}
				return true;
			}
			return false;
		}

		public override int GetHashCode()
		{
			var hashCode = 1624555593;
			hashCode = hashCode * -1521134295 + base.GetHashCode();
			hashCode = hashCode * -1521134295 + EqualityComparer<HapetType[]>.Default.GetHashCode(GenericParams);
			return hashCode;
		}

		public override int Match(HapetType concrete, Dictionary<string, (HapetType type, object value)> genericTypes)
		{
			if (concrete is StructType str)
			{
				int score = 0;
				for (int i = 0; i < GenericParams.Length; i++)
				{
					int s = HapetType.GenericValuesMatch((null, this.GenericParams[i]), (null, str.GenericParams[i]), genericTypes);
					if (s == -1)
						return -1;
					score += s;
				}
				return score;
			}

			return -1;
		}
	}

	public class EnumType : HapetType
	{
		public override bool IsGenericType => GenericParams?.Any(a => (a as HapetType)?.IsGenericType ?? false) ?? false;

		public HapetType[] GenericParams { get; }

		public EnumType(HapetType[] genericParams) : base()
		{
			GenericParams = genericParams;
		}

		public override string ToString()
		{
			if (GenericParams?.Length > 0)
			{
				var args = string.Join(", ", GenericParams.Select(a => a?.ToString()));
				return $"enum<{args}>";
			}
			return "enum";
		}

		public override int Match(HapetType concrete, Dictionary<string, (HapetType type, object value)> genericTypes)
		{
			if (concrete is EnumType str)
			{
				int score = 0;
				for (int i = 0; i < GenericParams.Length; i++)
				{
					int s = HapetType.GenericValuesMatch((null, this.GenericParams[i]), (null, str.GenericParams[i]), genericTypes);
					if (s == -1)
						return -1;
					score += s;
				}
				return score;
			}

			return -1;
		}
	}

	public class FunctionType : HapetType
	{
		public enum CallingConvention
		{
			Default,
			Stdcall,
		}

		public override bool IsGenericType => ReturnType.IsGenericType || Parameters.Any(p => p.IsGenericType);
		public HapetType[] Parameters { get; private set; }
		public HapetType ReturnType { get; private set; }
		public CallingConvention CC { get; } = CallingConvention.Default;

		public FunctionType(HapetType[] parameterTypes, HapetType returnType, CallingConvention cc)
			: base(PointerType.PointerSize, PointerType.PointerAlignment)
		{
			if (parameterTypes.Any(p => p == null))
			{
				throw new ArgumentNullException(nameof(parameterTypes));
			}

			this.Parameters = parameterTypes;
			this.ReturnType = returnType;
			this.CC = cc;
		}

		public FunctionType()
			: base(PointerType.PointerSize, PointerType.PointerAlignment)
		{
		}

		public override string ToString()
		{
			var args = string.Join(", ", Parameters.Select(p =>
			{
				return p.ToString();
			}));
			return $"{ReturnType} func({args})";
		}

		public override bool Equals(object obj)
		{
			if (obj is FunctionType f)
			{
				if (ReturnType != f.ReturnType)
					return false;

				if (Parameters.Length != f.Parameters.Length)
					return false;

				if (CC != f.CC)
					return false;

				for (int i = 0; i < Parameters.Length; i++)
					if (this.Parameters[i] != f.Parameters[i])
						return false;

				return true;
			}

			return false;
		}

		public override int GetHashCode()
		{
			var hash = new HashCode();
			hash.Add(IsGenericType);
			foreach (var p in Parameters)
				hash.Add(p);
			hash.Add(ReturnType);
			return hash.ToHashCode();
		}

		public override int Match(HapetType concrete, Dictionary<string, (HapetType type, object value)> genericTypes)
		{
			if (concrete is FunctionType str)
			{
#warning parameters checked only (should check generics and return value also)
				int score = 0;
				for (int i = 0; i < Parameters.Length; i++)
				{
					int s = HapetType.GenericValuesMatch((null, this.Parameters[i]), (null, str.Parameters[i]), genericTypes);
					if (s == -1)
						return -1;
					score += s;
				}
				return score;
			}

			return -1;
		}
	}
}
