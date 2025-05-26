using HapetFrontend.Ast;
using HapetFrontend.Types;

namespace HapetFrontend.Scoping
{
    public interface IUserDefinedOperator
    {
        FunctionType Function { get; set; }
    }

    public interface INaryOperator
    {
        HapetType[] ArgTypes { get; }
        HapetType ResultType { get; }
        string Name { get; }

        bool CanExecute { get; }

        int Accepts(params HapetType[] types);
        object Execute(params object[] args);
    }

    public interface IBinaryOperator
    {
        HapetType LhsType { get; }
        HapetType RhsType { get; }
        HapetType ResultType { get; }
        string Name { get; }

        bool CanExecute { get; }

        int Accepts(HapetType lhs, HapetType rhs);
        object Execute(object left, object right);
    }

    public interface IUnaryOperator
    {
        HapetType SubExprType { get; }
        HapetType ResultType { get; }
        string Name { get; }

        bool CanExecute { get; }

        int Accepts(HapetType sub);
        object Execute(object value);
    }

    /// <summary>
    /// To compare some shite
    /// </summary>
    public class BuiltInBinaryOperator : IBinaryOperator
    {
        public HapetType LhsType { get; private set; }
        public HapetType RhsType { get; private set; }
        public HapetType ResultType { get; private set; }

        public bool CanExecute { get; set; }

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

            CanExecute = exe != null;
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
            return ((LhsType.GetType() == lhs.GetType() || lhs.GetType().IsSubclassOf(LhsType.GetType())) && 
                (RhsType.GetType() == rhs.GetType() || rhs.GetType().IsSubclassOf(RhsType.GetType()))) ? 0 : -1;
        }
    }

    public class UserDefinedBinaryOperator : IBinaryOperator, IUserDefinedOperator
    {
        public HapetType LhsType { get; private set; }
        public HapetType RhsType { get; private set; }
        public HapetType ResultType { get; private set; }

        public FunctionType Function { get; set; }

        public bool CanExecute { get; set; }

        public string Name { get; private set; }

        public UserDefinedBinaryOperator(string name, HapetType resType, HapetType lhs, HapetType rhs)
        {
            Name = name;
            ResultType = resType;
            LhsType = lhs;
            RhsType = rhs;

            CanExecute = false;
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
            return null;
        }
    }

    public class BuiltInUnaryOperator : IUnaryOperator
    {
        public HapetType SubExprType { get; private set; }
        public HapetType ResultType { get; private set; }

        public bool CanExecute { get; set; }

        public string Name { get; private set; }

        public delegate object CompileTimeExecution(object value);
        public CompileTimeExecution Execution { get; set; }

        public BuiltInUnaryOperator(string name, HapetType resType, HapetType sub, CompileTimeExecution exe = null)
        {
            Name = name;
            ResultType = resType;
            SubExprType = sub;
            Execution = exe;

            CanExecute = exe != null;
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

    public class UserDefinedUnaryOperator : IUnaryOperator, IUserDefinedOperator
    {
        public HapetType SubExprType { get; private set; }
        public HapetType ResultType { get; private set; }

        public FunctionType Function { get; set; }

        public bool CanExecute { get; set; }

        public string Name { get; private set; }

        public UserDefinedUnaryOperator(string name, HapetType resType, HapetType sub)
        {
            Name = name;
            ResultType = resType;
            SubExprType = sub;

            CanExecute = false;
        }

        public int Accepts(HapetType sub)
        {
            return SubExprType.Match(sub);
        }

        public override string ToString()
        {
            return $"({ResultType}) {Name} {SubExprType}";
        }

        public object Execute(object value)
        {
            return null;
        }
    }
}
