using HapetFrontend.Ast.Declarations;

namespace HapetFrontend.Types
{
	public class ClassType : HapetType
	{
		public AstClassDecl Declaration { get; }

		public override string TypeName => "class";

		public ClassType(AstClassDecl decl)
			: base()
		{
			Declaration = decl;
		}

		public override string ToString()
		{
			return $"{Declaration.Name.Name}";
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

		public override string TypeName => "tuple";

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
			return $"({members})";
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

		public override string TypeName => "struct";

        /// <summary>
        /// The property is set to 'true' in code gen when StructLayoutAttribute found
        /// </summary>
        public bool IsUserDefinedAlignment { get; set; } = false;

		public StructType(AstStructDecl decl)
			: base() // TODO: WARN: hard coded struct alignment (probably undependent on platform target)
		{
			Declaration = decl;
		}

		public void ChangeSize(int size)
		{
			_size = size;
		}

		public void ChangeAlignment(int alignment)
		{
			_alignment = alignment;
		}

		public override string ToString()
		{
			return $"{Declaration.Name}";
		}

		public int GetIndexOfMember(string member)
		{
			return Declaration.Declarations.FindIndex(m => m.Name.Name == member);
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

		public override string TypeName => "enum";

		public EnumType(AstEnumDecl decl) : base()
		{
			Declaration = decl;
		}

		public override string ToString()
		{
			return $"{Declaration.Name.Name}";
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
		public AstFuncDecl Declaration { get; set; }

		public override string TypeName => "func";

		public FunctionType(AstFuncDecl decl)
			: base(PointerType.PointerSize, PointerType.PointerAlignment)
		{
			Declaration = decl;
		}

		public override string ToString()
		{
			var args = string.Join(", ", Declaration.Parameters.Select(p =>
			{
				if (p.Name != null)
					return $"{p.Type} {p.Name.Name}";
				return p.Type.ToString();
			}));

			if (Declaration.Returns.OutType != VoidType.Instance)
				return $"({Declaration.Returns.OutType} {Declaration.Name.Name}({args}))";
			else
				return $"(void {Declaration.Name.Name}({args}))";
		}

		public override bool Equals(object obj)
		{
			if (obj is FunctionType f)
			{
				if (Declaration.Returns.OutType != f.Declaration.Returns.OutType)
					return false;

				if (Declaration.Parameters.Count != f.Declaration.Parameters.Count)
					return false;

				if (Declaration.CallingConvention != f.Declaration.CallingConvention)
					return false;

				for (int i = 0; i < Declaration.Parameters.Count; i++)
					if (this.Declaration.Parameters[i].Type.OutType != f.Declaration.Parameters[i].Type.OutType)
						return false;

				return true;
			}

			return false;
		}

		public override int GetHashCode()
		{
			var hash = new HashCode();
			foreach (var p in Declaration.Parameters)
				hash.Add(p.Type.OutType);
			hash.Add(Declaration.Returns.OutType);
			return hash.ToHashCode();
		}
	}

	public class DelegateType : HapetType
	{
		public AstDelegateDecl Declaration { get; set; }

		public override string TypeName => "delegate";

		public DelegateType(AstDelegateDecl decl)
			: base(PointerType.PointerSize, PointerType.PointerAlignment)
		{
			Declaration = decl;
		}

		public override string ToString()
		{
			return $"{Declaration.Name.Name}";
		}

		public override bool Equals(object obj)
		{
			if (obj is FunctionType f)
			{
				if (Declaration.Returns.OutType != f.Declaration.Returns.OutType)
					return false;

				if (Declaration.Parameters.Count != f.Declaration.Parameters.Count)
					return false;

				for (int i = 0; i < Declaration.Parameters.Count; i++)
					if (this.Declaration.Parameters[i].Type.OutType != f.Declaration.Parameters[i].Type.OutType)
						return false;

				return true;
			}

			return false;
		}

		public override int GetHashCode()
		{
			var hash = new HashCode();
			foreach (var p in Declaration.Parameters)
				hash.Add(p.Type.OutType);
			hash.Add(Declaration.Returns.OutType);
			return hash.ToHashCode();
		}
	}
}
