namespace HapetFrontend.Scoping
{
	public partial class Scope
	{
		public string Name { get; set; }
		public Scope Parent { get; }

		/// <summary>
		/// Scopes that are connected via 'using' and so on. 
		/// </summary>
		private List<Scope> _usedScopes;

		/// <summary>
		/// So all the scopes would be unique
		/// </summary>
		private static ulong _scopeCounter = 0;

		public Scope(string name, Scope parent = null)
		{
			this.Name = $"{name}_{_scopeCounter++}";
			this.Parent = parent;
		}

		public bool AddUsedScope(Scope scope)
		{
			_usedScopes ??= new List<Scope>();
			if (_usedScopes.Contains(scope))
				return false;
			_usedScopes.Add(scope);
			return true;
		}

		public override string ToString()
		{
			return $"{Name}, Parent: {Parent?.Name}";
		}
	}
}
