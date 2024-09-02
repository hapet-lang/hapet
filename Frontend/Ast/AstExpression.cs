using Frontend.Parsing.Entities;
using Frontend.Scoping;
using Frontend.Types;
using Frontend.Visitors;
using System.Diagnostics;

namespace Frontend.Ast
{
	public enum ExprFlags
	{
		IsLValue = 0,
		Returns = 1,
		AssignmentTarget = 2,
		SetAccess = 3,
		Anonymous = 4,
		Link = 5,
		FromMacroExpansion = 6,
		IgnoreInCodeGen = 7,
		DontApplySymbolStatuses = 8,
		RequireInitializedSymbol = 9,
		Breaks = 10,
		ValueRequired = 11,
		IsDeclarationPattern = 13
	}

	public abstract class AstExpression : IVisitorAcceptor, ILocation, IAstNode
	{
		protected int mFlags { get; set; } = 0;

		public ILocation Location { get; set; }
		public TokenLocation Beginning => Location?.Beginning;
		public TokenLocation Ending => Location?.Ending;

		public HapetType Type { get; set; }

		private object _value;
		public object Value
		{
			get => _value;
			set
			{
				_value = value;
				IsCompileTimeValue = _value != null;
			}
		}
		public Scope Scope { get; set; }

		public bool IsCompileTimeValue { get; protected set; } = false;

		public IAstNode Parent { get; set; }

		[DebuggerStepThrough]
		public AstExpression(ILocation Location = null)
		{
			this.Location = Location;
		}

		public void SetFlag(ExprFlags f, bool b)
		{
			if (b)
			{
				mFlags |= 1 << (int)f;
			}
			else
			{
				mFlags &= ~(1 << (int)f);
			}
		}

		[DebuggerStepThrough]
		public bool GetFlag(ExprFlags f) => (mFlags & (1 << (int)f)) != 0;

		[DebuggerStepThrough]
		public abstract TReturn Accept<TReturn, TData>(IVisitor<TReturn, TData> visitor, TData data = default);

		[DebuggerStepThrough]
		public abstract AstExpression Clone();

		protected T CopyValuesTo<T>(T to)
			where T : AstExpression
		{
			to.Location = this.Location;
			to.Scope = this.Scope;
			to.mFlags = this.mFlags;
			to.Value = this.Value;
			to.IsCompileTimeValue = this.IsCompileTimeValue;
			return to;
		}

		protected void CopyValuesFrom(AstExpression from)
		{
			this.Location = from.Location;
			this.Scope = from.Scope;
			this.mFlags = from.mFlags;
			this.Value = from.Value;
			this.IsCompileTimeValue = from.IsCompileTimeValue;
		}

		//public override string ToString()
		//{
		//	return Accept(new AnalysedAstPrinter());
		//}

		public void Replace(AstExpression expr, Scope scope = null)
		{
			this.Scope = scope ?? expr.Scope;
			this.Parent = expr.Parent;
			this.mFlags = expr.mFlags;
		}

		public void AttachTo(IAstNode parent, Scope scope)
		{
			this.Scope = scope;
			this.Parent = parent;
		}

		public void AttachTo(AstExpression expr, Scope scope = null)
		{
			this.Scope = scope ?? expr.Scope;
			this.Parent = expr;
		}

		public void AttachTo(AstStatement stmt, Scope scope = null)
		{
			this.Scope = scope ?? stmt.Scope;
			this.Parent = stmt;
		}
	}

	public abstract class AstNestedExpression : AstExpression
	{
		public Scope SubScope { get; set; }

		public AstNestedExpression(ILocation Location = null)
			: base(Location: Location)
		{
		}
	}
}
