namespace Frontend.Visitors
{
	public interface IVisitorAcceptor
	{
		T Accept<T, D>(IVisitor<T, D> visitor, D data = default);
	}

	public interface IVisitor<ReturnType, DataType>
	{

	}

	public abstract class VisitorBase<ReturnType, DataType> : IVisitor<ReturnType, DataType>
	{

	}
}
