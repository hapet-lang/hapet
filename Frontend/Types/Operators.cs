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
}
