using Frontend.Ast;
using Frontend.Ast.Declarations;
using Frontend.Ast.Expressions;
using Frontend.Parsing.Entities;
using Frontend.Scoping.Entities;
using Frontend.Types;
using System.Net.NetworkInformation;

namespace Frontend.Scoping
{
	public class Scope
	{
		public string Name { get; set; }
		public Scope Parent { get; set; }
		/// <summary>
		/// Scopes added via "using"
		/// </summary>
		private List<Scope> _usedScopes = null;

		// decls in the scope
		private Dictionary<string, ISymbol> _symbolTable = new Dictionary<string, ISymbol>();
		private Dictionary<string, List<INaryOperator>> _naryOperatorTable = new Dictionary<string, List<INaryOperator>>();
		private Dictionary<string, List<IBinaryOperator>> _binaryOperatorTable = new Dictionary<string, List<IBinaryOperator>>();
		private Dictionary<string, List<IUnaryOperator>> _unaryOperatorTable = new Dictionary<string, List<IUnaryOperator>>();

		private (string label, IBreakable loopOrAction)? _break = null;
		private (string label, IContinuable loopOrAction)? _continue = null;

		public Scope(string name, Scope parent = null)
		{
			this.Name = name;
			this.Parent = parent;
		}

		public Scope Clone()
		{
			return new Scope(Name, Parent)
			{
				_symbolTable = new Dictionary<string, ISymbol>(_symbolTable),
				_binaryOperatorTable = new Dictionary<string, List<IBinaryOperator>>(_binaryOperatorTable),
				_unaryOperatorTable = new Dictionary<string, List<IUnaryOperator>>(_unaryOperatorTable)
				// TODO: mImplTable?, rest?
			};
		}

		#region Built in shite
		internal void DefineBuiltInTypes()
		{
			DefineTypeSymbol("sbyte", IntType.GetIntType(1, true));
			DefineTypeSymbol("short", IntType.GetIntType(2, true));
			DefineTypeSymbol("int", IntType.GetIntType(4, true));
			DefineTypeSymbol("long", IntType.GetIntType(8, true));

			DefineTypeSymbol("byte", IntType.GetIntType(1, false));
			DefineTypeSymbol("ushort", IntType.GetIntType(2, false));
			DefineTypeSymbol("uint", IntType.GetIntType(4, false));
			DefineTypeSymbol("ulong", IntType.GetIntType(8, false));

			DefineTypeSymbol("half", FloatType.GetFloatType(2));
			DefineTypeSymbol("float", FloatType.GetFloatType(4));
			DefineTypeSymbol("double", FloatType.GetFloatType(8));

			DefineTypeSymbol("char", CharType.GetCharType());
			DefineTypeSymbol("bool", BoolType.Instance);
			DefineTypeSymbol("string", StringType.Instance);
			DefineTypeSymbol("void", VoidType.Instance);
			
			DefineTypeSymbol("type", HapetTypeType.Instance);
			// TODO: do i need them?
			//DefineTypeSymbol("Code", CheezType.Code);
		}

		internal void DefineBuiltInOperators()
		{
			DefineLiteralOperators();

			DefineLogicOperators(new HapetType[] { BoolType.Instance },
				("&&", (a, b) => (bool)a && (bool)b),
				("||", (a, b) => (bool)a || (bool)b),
				("==", (a, b) => (bool)a == (bool)b),
				("!=", (a, b) => (bool)a != (bool)b));

			DefinePointerOperators();

			DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, HapetTypeType.Instance, HapetTypeType.Instance, (a, b) => a == b));
			DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, HapetTypeType.Instance, HapetTypeType.Instance, (a, b) => a != b));

			DefineBinaryOperator(new BuiltInClassNullOperator("=="));
			DefineBinaryOperator(new BuiltInClassNullOperator("!="));
			DefineBinaryOperator(new BuiltInFunctionOperator("=="));
			DefineBinaryOperator(new BuiltInFunctionOperator("!="));
			DefineBinaryOperator(new BuiltInEnumCompareOperator("=="));
			DefineBinaryOperator(new BuiltInEnumCompareOperator("!="));

			// TODO: ???
			//DefineBinaryOperator(new BuiltInPolyValueBinaryOperator("+", CheezType.PolyValue));
			//DefineBinaryOperator(new BuiltInPolyValueBinaryOperator("-", CheezType.PolyValue));
			//DefineBinaryOperator(new BuiltInPolyValueBinaryOperator("*", CheezType.PolyValue));
			//DefineBinaryOperator(new BuiltInPolyValueBinaryOperator("/", CheezType.PolyValue));
			//DefineBinaryOperator(new BuiltInPolyValueBinaryOperator("%", CheezType.PolyValue));

			// TODO: ???
			//DefineBinaryOperator(new BuiltInPolyValueBinaryOperator("==", CheezType.Bool));
			//DefineBinaryOperator(new BuiltInPolyValueBinaryOperator("!=", CheezType.Bool));
			//DefineBinaryOperator(new BuiltInPolyValueBinaryOperator("<", CheezType.Bool));
			//DefineBinaryOperator(new BuiltInPolyValueBinaryOperator("<=", CheezType.Bool));
			//DefineBinaryOperator(new BuiltInPolyValueBinaryOperator(">", CheezType.Bool));
			//DefineBinaryOperator(new BuiltInPolyValueBinaryOperator(">=", CheezType.Bool));

		}

		private void DefineLiteralOperators()
		{
			// literal types
			DefineUnaryOperator("!", BoolType.Instance, b => !(bool)b);
			DefineUnaryOperator("-", IntType.LiteralType, a => ((NumberData)a).Negate());
			DefineUnaryOperator("-", FloatType.LiteralType, a => ((NumberData)a).Negate());

			DefineBinaryOperator(new BuiltInBinaryOperator("+", StringLiteralType.Instance, StringLiteralType.Instance, StringLiteralType.Instance, (a, b) => $"{a}{b}"));

			DefineBinaryOperator(new BuiltInBinaryOperator("+", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a + (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("-", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a - (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("*", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a * (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("/", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a / (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("%", IntType.LiteralType, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a % (NumberData)b));

			DefineBinaryOperator(new BuiltInBinaryOperator("+", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a + (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("-", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a - (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("*", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a * (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("/", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a / (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("%", CharType.LiteralType, CharType.LiteralType, CharType.LiteralType, (a, b) => (NumberData)a % (NumberData)b));

			DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a == (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a != (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("<", BoolType.Instance, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a < (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("<=", BoolType.Instance, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a <= (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator(">", BoolType.Instance, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a > (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator(">=", BoolType.Instance, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a >= (NumberData)b));


			DefineBinaryOperator(new BuiltInBinaryOperator("+", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a + (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("-", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a - (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("*", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a * (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("/", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a / (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("%", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a % (NumberData)b));

			DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a == (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a != (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("<", BoolType.Instance, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a < (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("<=", BoolType.Instance, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a <= (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator(">", BoolType.Instance, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a > (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator(">=", BoolType.Instance, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a >= (NumberData)b));


			// basic types

			foreach (var type in new HapetType[]
				{
					IntType.GetIntType(1, true),
					IntType.GetIntType(2, true),
					IntType.GetIntType(4, true),
					IntType.GetIntType(8, true),
					IntType.GetIntType(1, false),
					IntType.GetIntType(2, false),
					IntType.GetIntType(4, false),
					IntType.GetIntType(8, false),
					CharType.GetCharType(),
				})
			{
				DefineUnaryOperator("-", type, a => ((NumberData)a).Negate());

				DefineBinaryOperator(new BuiltInBinaryOperator("+", type, type, type, (a, b) => (NumberData)a + (NumberData)b));
				DefineBinaryOperator(new BuiltInBinaryOperator("-", type, type, type, (a, b) => (NumberData)a - (NumberData)b));
				DefineBinaryOperator(new BuiltInBinaryOperator("*", type, type, type, (a, b) => (NumberData)a * (NumberData)b));
				DefineBinaryOperator(new BuiltInBinaryOperator("/", type, type, type, (a, b) => (NumberData)a / (NumberData)b));
				DefineBinaryOperator(new BuiltInBinaryOperator("%", type, type, type, (a, b) => (NumberData)a % (NumberData)b));

				DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, type, type, (a, b) => (NumberData)a == (NumberData)b));
				DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, type, type, (a, b) => (NumberData)a != (NumberData)b));
				DefineBinaryOperator(new BuiltInBinaryOperator("<", BoolType.Instance, type, type, (a, b) => (NumberData)a < (NumberData)b));
				DefineBinaryOperator(new BuiltInBinaryOperator("<=", BoolType.Instance, type, type, (a, b) => (NumberData)a <= (NumberData)b));
				DefineBinaryOperator(new BuiltInBinaryOperator(">", BoolType.Instance, type, type, (a, b) => (NumberData)a > (NumberData)b));
				DefineBinaryOperator(new BuiltInBinaryOperator(">=", BoolType.Instance, type, type, (a, b) => (NumberData)a >= (NumberData)b));
			}

			foreach (var type in new FloatType[] { FloatType.GetFloatType(4), FloatType.GetFloatType(8) })
			{
				DefineUnaryOperator("-", type, a => ((NumberData)a).Negate());

				DefineBinaryOperator(new BuiltInBinaryOperator("+", type, type, type, (a, b) => ((NumberData)a).AsDouble() + ((NumberData)b).AsDouble()));
				DefineBinaryOperator(new BuiltInBinaryOperator("-", type, type, type, (a, b) => ((NumberData)a).AsDouble() - ((NumberData)b).AsDouble()));
				DefineBinaryOperator(new BuiltInBinaryOperator("*", type, type, type, (a, b) => ((NumberData)a).AsDouble() * ((NumberData)b).AsDouble()));
				DefineBinaryOperator(new BuiltInBinaryOperator("/", type, type, type, (a, b) => ((NumberData)a).AsDouble() / ((NumberData)b).AsDouble()));
				DefineBinaryOperator(new BuiltInBinaryOperator("%", type, type, type, (a, b) => ((NumberData)a).AsDouble() % ((NumberData)b).AsDouble()));

				DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, type, type, (a, b) => ((NumberData)a).AsDouble() == ((NumberData)b).AsDouble()));
				DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, type, type, (a, b) => ((NumberData)a).AsDouble() != ((NumberData)b).AsDouble()));
				DefineBinaryOperator(new BuiltInBinaryOperator("<", BoolType.Instance, type, type, (a, b) => ((NumberData)a).AsDouble() < ((NumberData)b).AsDouble()));
				DefineBinaryOperator(new BuiltInBinaryOperator("<=", BoolType.Instance, type, type, (a, b) => ((NumberData)a).AsDouble() <= ((NumberData)b).AsDouble()));
				DefineBinaryOperator(new BuiltInBinaryOperator(">", BoolType.Instance, type, type, (a, b) => ((NumberData)a).AsDouble() > ((NumberData)b).AsDouble()));
				DefineBinaryOperator(new BuiltInBinaryOperator(">=", BoolType.Instance, type, type, (a, b) => ((NumberData)a).AsDouble() >= ((NumberData)b).AsDouble()));
			}
		}
		#endregion

		#region Operators
		public List<INaryOperator> GetNaryOperators(string name, params HapetType[] types)
		{
			var result = new List<INaryOperator>();
			int level = int.MaxValue;
			GetOperator(name, result, ref level, false, types);
			return result;
		}

		private void GetOperator(string name, List<INaryOperator> result, ref int level, bool localOnly, params HapetType[] types)
		{
			if (_naryOperatorTable.TryGetValue(name, out var ops))
			{
				foreach (var op in ops)
				{
					var l = op.Accepts(types);
					if (l == -1)
						continue;

					if (l < level)
					{
						level = l;
						result.Clear();
						result.Add(op);
					}
					else if (l == level)
					{
						result.Add(op);
					}
				}
			}

			if (localOnly)
				return;

			if (_usedScopes != null)
			{
				foreach (var scope in _usedScopes)
				{
					scope.GetOperator(name, result, ref level, true, types);
				}
			}

			Parent?.GetOperator(name, result, ref level, false, types);
		}

		public List<IBinaryOperator> GetBinaryOperators(string name, HapetType lhs, HapetType rhs)
		{
			var result = new List<IBinaryOperator>();
			int level = int.MaxValue;
			GetOperator(name, lhs, rhs, result, ref level);
			return result;
		}

		private void GetOperator(string name, HapetType lhs, HapetType rhs, List<IBinaryOperator> result, ref int level, bool localOnly = false)
		{
			if (_binaryOperatorTable.TryGetValue(name, out var ops))
			{
				foreach (var op in ops)
				{
					var l = op.Accepts(lhs, rhs);
					if (l == -1)
						continue;

					if (l < level)
					{
						level = l;
						result.Clear();
						result.Add(op);
					}
					else if (l == level)
					{
						result.Add(op);
					}
				}
			}

			if (localOnly)
				return;

			if (_usedScopes != null && !localOnly)
			{
				foreach (var scope in _usedScopes)
				{
					scope.GetOperator(name, lhs, rhs, result, ref level, true);
				}
			}

			Parent?.GetOperator(name, lhs, rhs, result, ref level);
		}

		public List<IUnaryOperator> GetUnaryOperators(string name, HapetType sub)
		{
			var result = new List<IUnaryOperator>();
			int level = int.MaxValue;
			GetOperator(name, sub, result, ref level);
			return result;
		}

		private void GetOperator(string name, HapetType sub, List<IUnaryOperator> result, ref int level, bool localOnly = false)
		{
			if (_unaryOperatorTable.TryGetValue(name, out var ops))
			{
				foreach (var op in ops)
				{
					var l = op.Accepts(sub);
					if (l == -1)
						continue;

					if (l < level)
					{
						level = l;
						result.Clear();
						result.Add(op);
					}
					else if (l == level)
					{
						result.Add(op);
					}
				}
			}

			if (localOnly)
				return;

			if (_usedScopes != null && !localOnly)
			{
				foreach (var scope in _usedScopes)
				{
					scope.GetOperator(name, sub, result, ref level, true);
				}
			}

			Parent?.GetOperator(name, sub, result, ref level);
		}
		#endregion

		#region Opartors defines
		private void DefinePointerOperators()
		{
			foreach (var op in new string[] { "==", "!=" })
			{
				List<IBinaryOperator> list;
				if (_binaryOperatorTable.ContainsKey(op))
					list = _binaryOperatorTable[op];
				else
				{
					list = new List<IBinaryOperator>();
					_binaryOperatorTable[op] = list;
				}

				list.Add(new BuiltInPointerOperator(op));
			}
		}

		private void DefineUnaryOperator(string name, HapetType type, BuiltInUnaryOperator.ComptimeExecution exe)
		{
			DefineUnaryOperator(new BuiltInUnaryOperator(name, type, type, exe));
		}

		public void DefineUnaryOperator(IUnaryOperator op)
		{
			List<IUnaryOperator> list = null;
			if (_unaryOperatorTable.ContainsKey(op.Name))
				list = _unaryOperatorTable[op.Name];
			else
			{
				list = new List<IUnaryOperator>();
				_unaryOperatorTable[op.Name] = list;
			}
			list.Add(op);
		}

		public void DefineBinaryOperator(IBinaryOperator op)
		{
			List<IBinaryOperator> list;
			if (_binaryOperatorTable.ContainsKey(op.Name))
				list = _binaryOperatorTable[op.Name];
			else
			{
				list = new List<IBinaryOperator>();
				_binaryOperatorTable[op.Name] = list;
			}
			list.Add(op);
		}

		private void DefineLogicOperators(HapetType[] types, params (string name, BuiltInBinaryOperator.ComptimeExecution exe)[] ops)
		{
			foreach (var op in ops)
			{
				List<IBinaryOperator> list = null;
				if (_binaryOperatorTable.ContainsKey(op.name))
					list = _binaryOperatorTable[op.name];
				else
				{
					list = new List<IBinaryOperator>();
					_binaryOperatorTable[op.name] = list;
				}

				foreach (var t in types)
				{
					list.Add(new BuiltInBinaryOperator(op.name, BoolType.Instance, t, t, op.exe));
				}
			}
		}
		#endregion

		#region Other
		public (bool ok, ILocation other) DefineLocalSymbol(ISymbol symbol, string name = null)
		{
			name ??= symbol.Name;

			if (name == "_")
				return (true, null);

			if (_symbolTable.TryGetValue(name, out var other))
				return (false, other.Location);

			_symbolTable[name] = symbol;
			return (true, null);
		}

		public (bool ok, ILocation other) DefineSymbol(ISymbol symbol, string name = null)
		{
#warning WHO THE FUCK IS TRANSPARENT PARENT
			//if (TransparentParent != null)
			//	return TransparentParent.DefineSymbol(symbol, name);
			return DefineLocalSymbol(symbol, name);
		}

		public (bool ok, ILocation other) DefineUse(string name, AstExpression expr, bool replace, out Using use)
		{
			use = new Using(expr, replace);
			return DefineSymbol(use, name);
		}

		public (bool ok, ILocation other) DefineConstant(string name, HapetType type, object value)
		{
			return DefineSymbol(new ConstSymbol(name, type, value));
		}

		public bool DefineTypeSymbol(string name, HapetType symbol)
		{
			return DefineSymbol(new TypeSymbol(name, symbol)).ok;
		}

		public (bool ok, ILocation other) DefineDeclaration(AstDeclaration decl)
		{
			return DefineSymbol(decl, decl.Name.Name);
		}

		public ISymbol GetSymbol(string name, bool searchUsedScopes = true, bool searchParentScope = true)
		{
			if (_symbolTable.ContainsKey(name))
			{
				var v = _symbolTable[name];
				return v;
			}

			if (_usedScopes != null && searchUsedScopes)
			{
				List<ISymbol> found = new List<ISymbol>();
				foreach (var scope in _usedScopes)
				{
					var sym = scope.GetSymbol(name, false, false);
					if (sym == null)
						continue;
					found.Add(sym);
				}

				if (found.Count == 1)
					return found[0];
				if (found.Count > 1)
					return new AmbiguousSymbol(found);
			}

			if (searchParentScope)
				return Parent?.GetSymbol(name);
			return null;
		}

		public AstClassTypeExpr GetClass(string name)
		{
			var sym = GetSymbol(name);
			if (sym is AstConstantDecl c && c.Initializer is AstClassTypeExpr s)
				return s;
			return null;
		}

		public AstStructTypeExpr GetStruct(string name)
		{
			var sym = GetSymbol(name);
			if (sym is AstConstantDecl c && c.Initializer is AstStructTypeExpr s)
				return s;
			return null;
		}

		public AstEnumTypeExpr GetEnum(string name)
		{
			var sym = GetSymbol(name);
			if (sym is AstConstantDecl c && c.Initializer is AstEnumTypeExpr s)
				return s;
			return null;
		}
		#endregion

		public override string ToString()
		{
			if (Parent != null)
				return $"{Parent}.{Name}";
			return $".{Name}";
		}
	}
}
