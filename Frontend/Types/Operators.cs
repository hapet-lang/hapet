using Frontend.Ast.Expressions;

namespace Frontend.Types
{
	public interface INaryOperator
	{
		HapetType[] ArgTypes { get; }
		HapetType ResultType { get; }
		string Name { get; }

		int Accepts(params HapetType[] types);

		object Execute(params object[] args);
	}

	public interface IBinaryOperator
	{
		HapetType LhsType { get; }
		HapetType RhsType { get; }
		HapetType ResultType { get; }
		string Name { get; }

		int Accepts(HapetType lhs, HapetType rhs);

		object Execute(object left, object right);
	}

	public interface IUnaryOperator
	{
		HapetType SubExprType { get; }
		HapetType ResultType { get; }
		string Name { get; }

		int Accepts(HapetType sub);

		object Execute(object value);
	}

	public class BuiltInBinaryOperator : IBinaryOperator
	{
		public HapetType LhsType { get; private set; }
		public HapetType RhsType { get; private set; }
		public HapetType ResultType { get; private set; }

		public string Name { get; private set; }

		public delegate object ComptimeExecution(object left, object right);
		public ComptimeExecution Execution { get; }

		public BuiltInBinaryOperator(string name, HapetType resType, HapetType lhs, HapetType rhs, ComptimeExecution exe = null)
		{
			Name = name;
			ResultType = resType;
			LhsType = lhs;
			RhsType = rhs;
			Execution = exe;
		}

		public int Accepts(HapetType lhs, HapetType rhs)
		{
			var ml = LhsType.Match(lhs, null);
			var mr = RhsType.Match(rhs, null);
			if (ml == -1 || mr == -1)
				return -1;

			return ml + mr;
		}

		public override string ToString()
		{
			return $"({ResultType}) {LhsType} {Name} {RhsType}";
		}

		public object Execute(object left, object right)
		{
			return Execution?.Invoke(left, right);
		}
	}

	public class BuiltInPointerOperator : IBinaryOperator
	{
		public HapetType LhsType => PointerType.GetPointerType(VoidType.Instance);
		public HapetType RhsType => PointerType.GetPointerType(VoidType.Instance);
		public HapetType ResultType { get; private set; }

		public string Name { get; private set; }

		public BuiltInPointerOperator(string name)
		{
			this.Name = name;
			switch (name)
			{
				case "==": ResultType = BoolType.Instance; break;
				case "!=": ResultType = BoolType.Instance; break;

				default: ResultType = PointerType.GetPointerType(VoidType.Instance); break;
			}
		}

		public int Accepts(HapetType lhs, HapetType rhs)
		{
			if (lhs is PointerType lt && rhs is PointerType rt)
				return 0;
			return -1;
		}

		public object Execute(object left, object right)
		{
			throw new NotImplementedException();
		}
	}

	public class BuiltInClassNullOperator : IBinaryOperator
	{
		public HapetType LhsType => null;
		public HapetType RhsType => PointerType.NullLiteralType;
		public HapetType ResultType => BoolType.Instance;

		public string Name { get; private set; }

		public BuiltInClassNullOperator(string name)
		{
			this.Name = name;
		}

		public int Accepts(HapetType lhs, HapetType rhs)
		{
			if (lhs is ClassType && rhs == PointerType.NullLiteralType)
				return 0;
			return -1;
		}

		public object Execute(object left, object right)
		{
			throw new NotImplementedException();
		}
	}

	public class BuiltInEnumCompareOperator : IBinaryOperator
	{
		public HapetType LhsType => null;
		public HapetType RhsType => null;
		public HapetType ResultType => BoolType.Instance;

		public string Name { get; private set; }

		public BuiltInEnumCompareOperator(string name)
		{
			this.Name = name;
		}

		public int Accepts(HapetType lhs, HapetType rhs)
		{
			if (lhs is EnumType f1 && rhs is EnumType f2 && f1 == f2)
				return 0;
			return -1;
		}

		public object Execute(object left, object right)
		{
			throw new NotImplementedException();
		}
	}

	public class EnumFlagsCombineOperator : IBinaryOperator
	{
		public EnumType EnumType { get; }
		public HapetType LhsType => EnumType;
		public HapetType RhsType => EnumType;
		public HapetType ResultType => EnumType;

		public string Name => "|";

		public EnumFlagsCombineOperator(EnumType type)
		{
			EnumType = type;
		}

		public int Accepts(HapetType lhs, HapetType rhs)
		{
			if (lhs == rhs && lhs == EnumType)
				return 0;
			return -1;
		}

		public object Execute(object left, object right)
		{
			var l = left as EnumValue;
			var r = right as EnumValue;
			if (l == null || r == null)
				throw new ArgumentException($"'{nameof(left)}' and '{nameof(right)}' must be enum values, but are '{left}' and '{right}'");
			if (l.Type != r.Type)
				throw new ArgumentException($"'{nameof(left)}' and '{nameof(right)}' must have the same type, but have {l.Type} and {r.Type}");


			// TODO: enum shite
			//var members = new HashSet<AstEnumMemberNew>();
			//members.UnionWith(l.Members);
			//members.UnionWith(r.Members);
			//return new EnumValue(l.Type, members.ToArray());
			return new EnumValue(l.Type);
		}
	}

	public class EnumFlagsAndOperator : IBinaryOperator
	{
		public EnumType EnumType { get; }
		public HapetType LhsType => EnumType;
		public HapetType RhsType => EnumType;
		public HapetType ResultType => EnumType;

		public string Name => "&";

		public EnumFlagsAndOperator(EnumType type)
		{
			EnumType = type;
		}

		public int Accepts(HapetType lhs, HapetType rhs)
		{
			if (lhs == rhs && lhs == EnumType)
				return 0;
			return -1;
		}

		public object Execute(object left, object right)
		{
			var l = left as EnumValue;
			var r = right as EnumValue;
			if (l == null || r == null)
				throw new ArgumentException($"'{nameof(left)}' and '{nameof(right)}' must be enum values, but are '{left}' and '{right}'");
			if (l.Type != r.Type)
				throw new ArgumentException($"'{nameof(left)}' and '{nameof(right)}' must have the same type, but have {l.Type} and {r.Type}");

			// TODO: enum shite
			// return new EnumValue(l.Type, l.Members.Intersect(r.Members).ToArray());
			return new EnumValue(l.Type);
		}
	}

	public class EnumFlagsTestOperator : IBinaryOperator
	{
		public EnumType EnumType { get; }
		public HapetType LhsType => EnumType;
		public HapetType RhsType => EnumType;
		public HapetType ResultType => BoolType.Instance;

		public string Name => "in";

		public EnumFlagsTestOperator(EnumType type)
		{
			EnumType = type;
		}

		public int Accepts(HapetType lhs, HapetType rhs)
		{
			if (lhs == rhs && lhs == EnumType)
				return 0;
			return -1;
		}

		public object Execute(object left, object right)
		{
			var l = left as EnumValue;
			var r = right as EnumValue;
			if (l == null || r == null)
				throw new ArgumentException($"'{nameof(left)}' and '{nameof(right)}' must be enum values, but are '{left}' and '{right}'");
			if (l.Type != r.Type)
				throw new ArgumentException($"'{nameof(left)}' and '{nameof(right)}' must have the same type, but have {l.Type} and {r.Type}");

			// TODO: enum shite
			// var contains = l.Members.Intersect(r.Members).Count() > 0;
			var contains = true;
			return contains;
		}
	}

	public class EnumFlagsNotOperator : IUnaryOperator
	{
		public EnumType EnumType { get; }
		public HapetType SubExprType => EnumType;
		public HapetType ResultType => EnumType;

		public string Name => "!";

		public EnumFlagsNotOperator(EnumType type)
		{
			EnumType = type;
		}

		public int Accepts(HapetType sub)
		{
			if (SubExprType == sub)
				return 0;
			return -1;
		}

		public object Execute(object sub)
		{
			var l = sub as EnumValue;
			if (l == null)
				throw new ArgumentException($"'{nameof(sub)}' must be an enum value, but is '{sub}'");

			// TODO: enum shite
			// return new EnumValue(EnumType, EnumType.Declaration.Members.Except(l.Members).ToArray());
			return new EnumValue(EnumType);
		}
	}


	public class BuiltInFunctionOperator : IBinaryOperator
	{
		public HapetType LhsType => null;
		public HapetType RhsType => null;
		public HapetType ResultType => BoolType.Instance;

		public string Name { get; private set; }

		public BuiltInFunctionOperator(string name)
		{
			this.Name = name;
		}

		public int Accepts(HapetType lhs, HapetType rhs)
		{
			if (lhs is FunctionType f1 && rhs is FunctionType f2 && f1 == f2)
				return 0;
			return -1;
		}

		public object Execute(object left, object right)
		{
			throw new NotImplementedException();
		}
	}

	public class BuiltInUnaryOperator : IUnaryOperator
	{
		public HapetType SubExprType { get; private set; }
		public HapetType ResultType { get; private set; }

		public string Name { get; private set; }

		public delegate object ComptimeExecution(object value);
		public ComptimeExecution Execution { get; set; }

		public BuiltInUnaryOperator(string name, HapetType resType, HapetType sub, ComptimeExecution exe = null)
		{
			Name = name;
			ResultType = resType;
			SubExprType = sub;
			this.Execution = exe;
		}

		public override string ToString()
		{
			return $"({ResultType}) {Name} {SubExprType}";
		}

		public int Accepts(HapetType sub)
		{
			return SubExprType.Match(sub, null);
		}

		public object Execute(object value)
		{
			return Execution?.Invoke(value);
		}
	}

	public class UserDefinedUnaryOperator : IUnaryOperator
	{
		public HapetType SubExprType { get; set; }
		public HapetType ResultType { get; set; }
		public string Name { get; set; }

		public AstFuncExpr Declaration { get; set; }

		public UserDefinedUnaryOperator(string name, AstFuncExpr func)
		{
			this.Name = name;
			this.SubExprType = func.Parameters[0].Type;
			this.ResultType = func.ReturnType;
			this.Declaration = func;
		}

		public int Accepts(HapetType sub)
		{
			Dictionary<string, (HapetType type, object value)> polyTypes = null;

			// TODO: necessary?
			//if (SubExprType.IsPolyType)
			//{
			//    polyTypes = new Dictionary<string, HapetType>();
			//    Workspace.CollectPolyTypes(SubExprType, lhs, polyTypes);
			//}


			return SubExprType.Match(sub, polyTypes);
		}

		public object Execute(object value)
		{
			throw new NotImplementedException();
		}
	}

	public class UserDefinedBinaryOperator : IBinaryOperator
	{
		public HapetType LhsType { get; set; }
		public HapetType RhsType { get; set; }
		public HapetType ResultType { get; set; }
		public string Name { get; set; }
		public AstFuncExpr Declaration { get; set; }

		public UserDefinedBinaryOperator(string name, AstFuncExpr func)
		{
			this.Name = name;
			this.LhsType = func.Parameters[0].Type;
			this.RhsType = func.Parameters[1].Type;
			this.ResultType = func.ReturnType;
			this.Declaration = func;
		}

		public int Accepts(HapetType lhs, HapetType rhs)
		{
			Dictionary<string, (HapetType type, object value)> polyTypes = null;

			// TODO: poly shite
			//if (LhsType.IsPolyType || RhsType.IsPolyType)
			//{
			//	polyTypes = new Dictionary<string, (HapetType type, object value)>();
			//	Workspace.CollectPolyTypes(LhsType, lhs, polyTypes);
			//	Workspace.CollectPolyTypes(RhsType, rhs, polyTypes);
			//}


			var ml = LhsType.Match(lhs, polyTypes);
			var mr = RhsType.Match(rhs, polyTypes);
			if (ml == -1 || mr == -1)
				return -1;
			return ml + mr;
		}

		public object Execute(object left, object right)
		{
			throw new NotImplementedException();
		}
	}

	public class UserDefinedNaryOperator : INaryOperator
	{
		public HapetType[] ArgTypes { get; }
		public HapetType ResultType { get; }
		public string Name { get; set; }
		public AstFuncExpr Declaration { get; set; }


		public UserDefinedNaryOperator(string name, AstFuncExpr func)
		{
			this.Name = name;
			this.ArgTypes = func.Parameters.Select(p => p.Type).ToArray();
			this.ResultType = func.ReturnType;
			this.Declaration = func;
		}

		public int Accepts(params HapetType[] types)
		{
			if (types.Length != ArgTypes.Length)
				return -1;

			var polyTypes = new Dictionary<string, (HapetType type, object value)>();

			// TODO: poly shite
			//for (int i = 0; i < ArgTypes.Length; i++)
			//{
			//	Workspace.CollectPolyTypes(ArgTypes[i], types[i], polyTypes);
			//}

			var match = 0;
			for (int i = 0; i < ArgTypes.Length; i++)
			{
				var m = ArgTypes[i].Match(types[i], polyTypes);
				if (m == -1)
					return -1;
				match += m;
			}

			return match;
		}

		public object Execute(params object[] args)
		{
			throw new NotImplementedException();
		}
	}
}
