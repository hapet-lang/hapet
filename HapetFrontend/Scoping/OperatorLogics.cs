using HapetFrontend.Ast.Declarations;
using HapetFrontend.Types;

namespace HapetFrontend.Scoping
{
	public partial class Scope
	{
		private Dictionary<string, ISymbol> _symbolTable = new Dictionary<string, ISymbol>();
		private Dictionary<string, List<INaryOperator>> _naryOperatorTable = new Dictionary<string, List<INaryOperator>>();
		private Dictionary<string, List<IBinaryOperator>> _binaryOperatorTable = new Dictionary<string, List<IBinaryOperator>>();
		private Dictionary<string, List<IUnaryOperator>> _unaryOperatorTable = new Dictionary<string, List<IUnaryOperator>>();

		#region Operators gets
		/// <summary>
		/// Returns all operators defined for the specified <param name="types"/> and <param name="name"/>
		/// </summary>
		/// <param name="name">The name of the operator</param>
		/// <param name="types">Types that are applied to the operator</param>
		/// <returns>Found operators</returns>
		public List<INaryOperator> GetNaryOperators(string name, params HapetType[] types)
		{
			var result = new List<INaryOperator>();
			int level = int.MaxValue;
			GetNaryOperatorsInternal(name, result, ref level, false, types);
			return result;
		}

		private void GetNaryOperatorsInternal(string name, List<INaryOperator> result, ref int level, bool localOnly, params HapetType[] types)
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
					scope.GetNaryOperatorsInternal(name, result, ref level, true, types);
				}
			}
			Parent?.GetNaryOperatorsInternal(name, result, ref level, false, types);
		}

		/// <summary>
		/// Returns all operators defined for the specified <param name="lhs"/>, <param name="rhs"/> and <param name="name"/>
		/// </summary>
		/// <param name="name">The name of the operator</param>
		/// <param name="lhs">Left operand</param>
		/// <param name="rhs">Right operand</param>
		/// <returns>Found operators</returns>
		public List<IBinaryOperator> GetBinaryOperators(string name, HapetType lhs, HapetType rhs)
		{
			var result = new List<IBinaryOperator>();
			int level = int.MaxValue;
			GetBinaryOperatorsInternal(name, lhs, rhs, result, ref level);
			return result;
		}

		private void GetBinaryOperatorsInternal(string name, HapetType lhs, HapetType rhs, List<IBinaryOperator> result, ref int level, bool localOnly = false)
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
					scope.GetBinaryOperatorsInternal(name, lhs, rhs, result, ref level, true);
				}
			}
			Parent?.GetBinaryOperatorsInternal(name, lhs, rhs, result, ref level);
		}

		/// <summary>
		/// Returns all operators defined for the specified <param name="sub"/> and <param name="name"/>
		/// </summary>
		/// <param name="name">The name of the operator</param>
		/// <param name="sub">The parameter to be applied to the operator</param>
		/// <returns>The operators</returns>
		public List<IUnaryOperator> GetUnaryOperators(string name, HapetType sub)
		{
			var result = new List<IUnaryOperator>();
			int level = int.MaxValue;
			GetUnaryOperatorsInternal(name, sub, result, ref level);
			return result;
		}

		private void GetUnaryOperatorsInternal(string name, HapetType sub, List<IUnaryOperator> result, ref int level, bool localOnly = false)
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
					scope.GetUnaryOperatorsInternal(name, sub, result, ref level, true);
				}
			}
			Parent?.GetUnaryOperatorsInternal(name, sub, result, ref level);
		}
		#endregion

		#region Operator and symbols defines
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

			DefineTypeSymbol("char8", CharType.GetCharType(1));
			DefineTypeSymbol("char16", CharType.GetCharType(2));
			DefineTypeSymbol("char32", CharType.GetCharType(4));
			DefineTypeSymbol("char", CharType.GetCharType(4));
			DefineTypeSymbol("bool", BoolType.Instance);
			DefineTypeSymbol("string", StringType.Instance);
			DefineTypeSymbol("void", VoidType.Instance);
			// DefineTypeSymbol("type", CheezType.Type); // TODO: ...
			// DefineTypeSymbol("Code", CheezType.Code); // TODO: do i need it?
		}

		internal void DefineBuiltInOperators()
		{
			DefineLiteralOperators();

			DefineLogicOperators(new HapetType[] { BoolType.Instance },
				("&&", (a, b) => (bool)a && (bool)b),
				("||", (a, b) => (bool)a || (bool)b),
				("==", (a, b) => (bool)a == (bool)b),
				("!=", (a, b) => (bool)a != (bool)b));

			// TODO: ...
			// DefinePointerOperators();

			// TODO: ...
			//DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, CheezType.Type, CheezType.Type, (a, b) => a == b));
			//DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, CheezType.Type, CheezType.Type, (a, b) => a != b));

			DefineBinaryOperator(new BuiltInClassNullOperator("=="));
			DefineBinaryOperator(new BuiltInClassNullOperator("!="));
			DefineBinaryOperator(new BuiltInFunctionOperator("=="));
			DefineBinaryOperator(new BuiltInFunctionOperator("!="));
			DefineBinaryOperator(new BuiltInEnumCompareOperator("=="));
			DefineBinaryOperator(new BuiltInEnumCompareOperator("!="));
		}

		private void DefineLiteralOperators()
		{
			// literal types
			DefineUnaryOperator("!", BoolType.Instance, b => !(bool)b);
			DefineUnaryOperator("-", IntType.LiteralType, a => ((NumberData)a).Negate());
			DefineUnaryOperator("-", FloatType.LiteralType, a => ((NumberData)a).Negate());

			DefineBinaryOperator(new BuiltInBinaryOperator("+", StringType.LiteralType, StringType.LiteralType, StringType.LiteralType, (a, b) => $"{a}{b}"));

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
			DefineBinaryOperator(new BuiltInBinaryOperator("<",  BoolType.Instance, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a < (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("<=", BoolType.Instance, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a <= (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator(">",  BoolType.Instance, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a > (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator(">=", BoolType.Instance, IntType.LiteralType, IntType.LiteralType, (a, b) => (NumberData)a >= (NumberData)b));


			DefineBinaryOperator(new BuiltInBinaryOperator("+", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a + (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("-", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a - (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("*", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a * (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("/", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a / (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("%", FloatType.LiteralType, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a % (NumberData)b));

			DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a == (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a != (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("<",  BoolType.Instance, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a < (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator("<=", BoolType.Instance, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a <= (NumberData)b));
			DefineBinaryOperator(new BuiltInBinaryOperator(">",  BoolType.Instance, FloatType.LiteralType, FloatType.LiteralType, (a, b) => (NumberData)a > (NumberData)b));
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
					CharType.GetCharType(1),
					CharType.GetCharType(2),
					CharType.GetCharType(4),
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
				DefineBinaryOperator(new BuiltInBinaryOperator("<",  BoolType.Instance, type, type, (a, b) => (NumberData)a < (NumberData)b));
				DefineBinaryOperator(new BuiltInBinaryOperator("<=", BoolType.Instance, type, type, (a, b) => (NumberData)a <= (NumberData)b));
				DefineBinaryOperator(new BuiltInBinaryOperator(">",  BoolType.Instance, type, type, (a, b) => (NumberData)a > (NumberData)b));
				DefineBinaryOperator(new BuiltInBinaryOperator(">=", BoolType.Instance, type, type, (a, b) => (NumberData)a >= (NumberData)b));
			}

			foreach (var type in new FloatType[] 
			{ 
				FloatType.GetFloatType(2), 
				FloatType.GetFloatType(4), 
				FloatType.GetFloatType(8) 
			})
			{
				DefineUnaryOperator("-", type, a => ((NumberData)a).Negate());

				DefineBinaryOperator(new BuiltInBinaryOperator("+", type, type, type, (a, b) => ((NumberData)a) + ((NumberData)b)));
				DefineBinaryOperator(new BuiltInBinaryOperator("-", type, type, type, (a, b) => ((NumberData)a) - ((NumberData)b)));
				DefineBinaryOperator(new BuiltInBinaryOperator("*", type, type, type, (a, b) => ((NumberData)a) * ((NumberData)b)));
				DefineBinaryOperator(new BuiltInBinaryOperator("/", type, type, type, (a, b) => ((NumberData)a) / ((NumberData)b)));
				DefineBinaryOperator(new BuiltInBinaryOperator("%", type, type, type, (a, b) => ((NumberData)a) % ((NumberData)b)));

				DefineBinaryOperator(new BuiltInBinaryOperator("==", BoolType.Instance, type, type, (a, b) => ((NumberData)a) == ((NumberData)b)));
				DefineBinaryOperator(new BuiltInBinaryOperator("!=", BoolType.Instance, type, type, (a, b) => ((NumberData)a) != ((NumberData)b)));
				DefineBinaryOperator(new BuiltInBinaryOperator("<",  BoolType.Instance, type, type, (a, b) => ((NumberData)a) < ((NumberData)b)));
				DefineBinaryOperator(new BuiltInBinaryOperator("<=", BoolType.Instance, type, type, (a, b) => ((NumberData)a) <= ((NumberData)b)));
				DefineBinaryOperator(new BuiltInBinaryOperator(">",  BoolType.Instance, type, type, (a, b) => ((NumberData)a) > ((NumberData)b)));
				DefineBinaryOperator(new BuiltInBinaryOperator(">=", BoolType.Instance, type, type, (a, b) => ((NumberData)a) >= ((NumberData)b)));
			}
		}

		private void DefineUnaryOperator(string name, HapetType type, BuiltInUnaryOperator.CompileTimeExecution exe)
		{
			DefineUnaryOperator(new BuiltInUnaryOperator(name, type, type, exe));
		}

		public void DefineUnaryOperator(IUnaryOperator op)
		{
			List<IUnaryOperator> list;
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

		private void DefineOperator(INaryOperator op)
		{
			List<INaryOperator> list;
			if (_naryOperatorTable.ContainsKey(op.Name))
				list = _naryOperatorTable[op.Name];
			else
			{
				list = new List<INaryOperator>();
				_naryOperatorTable[op.Name] = list;
			}

			list.Add(op);
		}

		private void DefineLogicOperators(HapetType[] types, params (string name, BuiltInBinaryOperator.CompileTimeExecution exe)[] ops)
		{
			foreach (var op in ops)
			{
				List<IBinaryOperator> list;
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

		public bool DefineLocalSymbol(ISymbol symbol, string name = null)
		{
			name ??= symbol.Name;
			if (name == "_")
				return true;

			if (_symbolTable.TryGetValue(name, out var other))
				return false;

			_symbolTable[name] = symbol;
			return true;
		}

		public bool DefineSymbol(ISymbol symbol, string name = null)
		{
			return DefineLocalSymbol(symbol, name);
		}

		public bool DefineTypeSymbol(string name, HapetType symbol)
		{
			return DefineSymbol(new TypeSymbol(name, symbol));
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
					return new AmbiguousSymol(found);
			}

			if (searchParentScope)
				return Parent?.GetSymbol(name);
			return null;
		}
		#endregion

		public AstClassDecl GetClass(string name)
		{
			var sym = GetSymbol(name);
			if (sym is AstClassDecl s)
				return s;
			return null;
		}

		public AstStructDecl GetStruct(string name)
		{
			var sym = GetSymbol(name);
			if (sym is AstStructDecl s)
				return s;
			return null;
		}

		public AstEnumDecl GetEnum(string name)
		{
			var sym = GetSymbol(name);
			if (sym is AstEnumDecl s)
				return s;
			return null;
		}
	}
}
