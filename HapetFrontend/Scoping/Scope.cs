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

		public Scope(string name, Scope parent = null)
		{
			this.Name = name;
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
	}
}
