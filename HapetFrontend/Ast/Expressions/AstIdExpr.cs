using HapetFrontend.Scoping;
using Newtonsoft.Json;

namespace HapetFrontend.Ast.Expressions
{
    public class AstIdExpr : AstExpression
    {
        public string Name { get; set; }

        [JsonIgnore]
        private ISymbol _foundSymbol = null;
        /// <summary>
        /// Getting symbol of itself
        /// </summary>
        [JsonIgnore]
        public ISymbol FindSymbol
        {
            get
            {
                if (_foundSymbol == null)
                    _foundSymbol = Scope.GetSymbol(Name);
                return _foundSymbol;
            }
            set
            {
                _foundSymbol = value;
            }
        }

        /// <summary>
        /// Additional string in front of main. 
        /// For example used for dtors like '~Anime()'
        /// </summary>
        public string Suffix { get; set; }

        public override string AAAName => nameof(AstIdExpr);

        public AstIdExpr(string name, ILocation Location = null) : base(Location)
        {
            this.Name = name;
        }

        public override string ToString()
        {
            return Name;
        }

        public AstIdExpr GetCopy(string name = "")
        {
            string newName = string.IsNullOrWhiteSpace(name) ? Name : name;
            var newId = new AstIdExpr(newName, Location)
            {
                Suffix = this.Suffix,
                Parent = this.Parent,
                Scope = this.Scope,
                IsCompileTimeValue = this.IsCompileTimeValue,
                OutType = this.OutType,
                OutValue = this.OutValue,
                SourceFile = this.SourceFile,
            };
            return newId;
        }
    }
}
