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

        public bool IsParentOf(Scope scope)
        {
            var curr = scope;
            while (curr != null)
            {
                if (curr == this)
                    return true;
                curr = curr.Parent;
            }
            return false;
        }

        public bool IsChildOf(Scope scope)
        {
            var curr = this;
            while (curr != null)
            {
                if (curr == scope)
                    return true;
                curr = curr.Parent;
            }
            return false;
        }
    }
}
