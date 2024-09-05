using HapetFrontend.Ast.Declarations;

namespace HapetFrontend.Types
{
	public abstract class AbstractType : HapetType
	{
		protected AbstractType() : base(0, 0) { }
	}

	/// <summary>
	/// This is like 'this.a = ...' in a class
	/// </summary>
	public class ThisType : AbstractType
	{
		public HapetType ClassType { get; }

		public ThisType(HapetType classType)
		{
			this.ClassType = classType;
		}

		public override string ToString()
		{
			return "this";
		}
	}

	/// <summary>
	/// This is like 'var a = ...'
	/// </summary>
	public class VarType : AbstractType
	{
		public AstVarDecl Declaration { get; }

		public VarType(AstVarDecl decl)
		{
			Declaration = decl;
		}

		public override string ToString() => $"var {Declaration.Name.Name}";
	}
}
