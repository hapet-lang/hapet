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
		/// Getting all the built in binary operators
		/// </summary>
		/// <returns>The built in operators</returns>
		public List<BuiltInBinaryOperator> GetBuiltInBinaryOperators()
		{
			return _binaryOperatorTable.SelectMany(x => x.Value).Where(x => x is BuiltInBinaryOperator).Select(x => x as BuiltInBinaryOperator).ToList();
		}

		/// <summary>
		/// Getting all the built in unary operators
		/// </summary>
		/// <returns>The built in operators</returns>
		public List<BuiltInUnaryOperator> GetBuiltInUnaryOperators()
		{
			return _unaryOperatorTable.SelectMany(x => x.Value).Where(x => x is BuiltInUnaryOperator).Select(x => x as BuiltInUnaryOperator).ToList();
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

		#region Operator defines
		private void DefineUnaryOperator(string name, HapetType resultType, HapetType type, BuiltInUnaryOperator.CompileTimeExecution exe)
		{
			DefineUnaryOperator(new BuiltInUnaryOperator(name, resultType, type, exe));
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
		#endregion
	}
}
