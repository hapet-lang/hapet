using HapetFrontend.Ast;
using HapetFrontend.Types;

namespace HapetFrontend.Scoping
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

	/// <summary>
	/// To check if class is null
	/// </summary>
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

		// TODO: probably should be in Execute
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

	/// <summary>
	/// Check if enums are equal
	/// </summary>
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

		// TODO: probably should be in Execute
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


	/// <summary>
	/// To check if funcs are equal
	/// </summary>
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

		// TODO: probably should be in Execute
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

	/// <summary>
	/// To compare some shite
	/// </summary>
	public class BuiltInBinaryOperator : IBinaryOperator
	{
		public HapetType LhsType { get; private set; }
		public HapetType RhsType { get; private set; }
		public HapetType ResultType { get; private set; }

		public string Name { get; private set; }

		public delegate object CompileTimeExecution(object left, object right);
		public CompileTimeExecution Execution { get; }

		public BuiltInBinaryOperator(string name, HapetType resType, HapetType lhs, HapetType rhs, CompileTimeExecution exe = null)
		{
			Name = name;
			ResultType = resType;
			LhsType = lhs;
			RhsType = rhs;
			Execution = exe;
		}

		public virtual int Accepts(HapetType lhs, HapetType rhs)
		{
			var ml = LhsType.Match(lhs);
			var mr = RhsType.Match(rhs);
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

    public class BuiltInCommonBinaryOperator : BuiltInBinaryOperator
    {
        public BuiltInCommonBinaryOperator(string name, HapetType resType, HapetType lhs, HapetType rhs, CompileTimeExecution exe = null)
			: base(name, resType, lhs, rhs, exe)
        {
        }

        public override int Accepts(HapetType lhs, HapetType rhs)
        {
			return (LhsType.GetType() == lhs.GetType() && RhsType.GetType() == rhs.GetType()) ? 0 : -1;
        }
    }

    public class BuiltInUnaryOperator : IUnaryOperator
	{
		public HapetType SubExprType { get; private set; }
		public HapetType ResultType { get; private set; }

		public string Name { get; private set; }

		public delegate object CompileTimeExecution(object value);
		public CompileTimeExecution Execution { get; set; }

		public BuiltInUnaryOperator(string name, HapetType resType, HapetType sub, CompileTimeExecution exe = null)
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
			return SubExprType.Match(sub);
		}

		public object Execute(object value)
		{
			return Execution?.Invoke(value);
		}
	}
}
