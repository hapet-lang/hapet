using HapetFrontend.Enums;

namespace HapetFrontend.Types
{
	public class ClassType : HapetType
	{
		public AstClassDecl Declaration { get; }

		public ClassType(AstClassDecl decl)
			: base()
		{
			Declaration = decl;
		}

		public override string ToString()
		{
			return $"class {Declaration.Name}";
		}

		public override int Match(HapetType concrete)
		{
			if (concrete is ClassType)
			{
				int score = 0;
				return score;
			}
			return -1;
		}
	}

	/// <summary>
	/// Doesn't have Ast shite in it but being created every time like a new instance
	/// </summary>
	public class TupleType : HapetType
	{
		public static readonly TupleType LiteralType = GetTuple(Array.Empty<(HapetType, string)>());

		public (HapetType type, string name)[] Members { get; }

		private TupleType((HapetType type, string name)[] members) : base()
		{
			Members = members;
		}

		public static TupleType GetTuple((HapetType type, string name)[] members)
		{
			return new TupleType(members);
		}

		public override string ToString()
		{
			var members = string.Join(", ", Members.Select(m =>
			{
				if (m.name != null) return $"{m.type} {m.name}";
				return m.type.ToString();
			}));
			return $"tuple ({members})";
		}

		public override bool Equals(object obj)
		{
			if (obj is TupleType t)
			{
				if (Members.Length != t.Members.Length) return false;
				for (int i = 0; i < Members.Length; i++)
					if (Members[i].type != t.Members[i].type) return false;

				return true;
			}
			return false;
		}

		public override int GetHashCode()
		{
			var hash = new HashCode();
			foreach (var m in Members)
			{
				hash.Add(m.type.GetHashCode());
			}
			return hash.ToHashCode();
		}
	}

	public class StructType : HapetType
	{
		public AstStructDecl Declaration { get; }

		public StructType(AstStructDecl decl)
			: base()
		{
			Declaration = decl;
		}

		public override string ToString()
		{
			return $"struct {Declaration.Name}";
		}

		public int GetIndexOfMember(string member)
		{
			return Declaration.Members.FindIndex(m => m.Name == member);
		}

		public override bool Equals(object obj)
		{
			if (obj is StructType s)
			{
				if (Declaration != s.Declaration)
					return false;
				return true;
			}
			return false;
		}

		public override int Match(HapetType concrete)
		{
			if (concrete is StructType)
			{
				int score = 0;
				return score;
			}
			return -1;
		}

		public override int GetHashCode()
		{
			var hashCode = 1624555593;
			hashCode = hashCode * -1521134295 + base.GetHashCode();
			return hashCode;
		}
	}

	public class EnumType : HapetType
	{
		public AstEnumDecl Declaration { get; set; }

		public EnumType(AstEnumDecl decl) : base()
		{
			Declaration = decl;
		}

		public override string ToString()
		{
			return $"enum {Declaration.Name}";
		}

		public override int Match(HapetType concrete)
		{
			if (concrete is EnumType str)
			{
				int score = 0;
				return score;
			}
			return -1;
		}
	}

	public class FunctionType : HapetType
	{
		public AstFunctionDecl Declaration { get; set; }

		// TODO: SHOULD BE IN ASTDECL
		//public (string name, CheezType type, AstExpression defaultValue)[] Parameters { get; private set; }
		//public CheezType ReturnType { get; private set; }

		//public CallingConvention CallingConvention { get; } = CallingConvention.Default;

		public FunctionType(AstFunctionDecl decl)
			: base(PointerType.PointerSize, PointerType.PointerAlignment)
		{
			Declaration = decl;
		}

		public override string ToString()
		{
			var args = string.Join(", ", Declaration.Parameters.Select(p =>
			{
				if (p.name != null)
					return $"{p.type} {p.name}";
				return p.type.ToString();
			}));

			if (Declaration.ReturnType != VoidType.Instance)
				return $"{Declaration.ReturnType} {Declaration.Name.Name}({args})";
			else
				return $"void {Declaration.Name.Name}({args})";
		}

		public override bool Equals(object obj)
		{
			if (obj is FunctionType f)
			{
				if (Declaration.ReturnType != f.Declaration.ReturnType)
					return false;

				if (Declaration.Parameters.Length != f.Declaration.Parameters.Length)
					return false;

				if (Declaration.CallingConvension != f.Declaration.CallingConvension)
					return false;

				for (int i = 0; i < Declaration.Parameters.Length; i++)
					if (this.Declaration.Parameters[i].type != f.Declaration.Parameters[i].type)
						return false;

				return true;
			}

			return false;
		}

		public override int GetHashCode()
		{
			var hash = new HashCode();
			foreach (var p in Declaration.Parameters)
				hash.Add(p.type);
			hash.Add(Declaration.ReturnType);
			return hash.ToHashCode();
		}
	}
}
