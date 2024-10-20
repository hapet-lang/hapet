using HapetFrontend.Enums;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace HapetFrontend.Types
{
	public struct NumberData
	{
		/// <summary>
		/// Int base like 2 (0 or 1), 8, 10, 16
		/// </summary>
		public int IntBase { get; private set; }
		/// <summary>
		/// The string repr of the number
		/// </summary>
		public string StringValue { get; private set; }
		/// <summary>
		/// The type of the number float/int
		/// </summary>
		public NumberType Type { get; private set; }
		/// <summary>
		/// Int value is holded here
		/// </summary>
		public BigInteger IntValue { get; private set; }
		/// <summary>
		/// Float value is holded here
		/// </summary>
		public double DoubleValue { get; private set; }
		/// <summary>
		/// An error that can be occured when trying to create NumberData
		/// </summary>
		public string Error { get; private set; }

		public NumberData(NumberType type, string val, int b)
		{
			IntBase = b;
			StringValue = val;
			Type = type;
			IntValue = default;
			DoubleValue = default;
			Error = null;

			if (type == NumberType.Int)
			{
				if (b == 10)
					IntValue = BigInteger.Parse("0" + val, CultureInfo.InvariantCulture);
				else if (b == 16)
					IntValue = BigInteger.Parse("0" + val, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
				else if (b == 2)
				{
					BigInteger currentDigit = 1;
					IntValue = 0;

					for (int i = val.Length - 1; i >= 0; i--)
					{
						if (val[i] == '1')
							IntValue += currentDigit;
						else if (val[i] == '0') { }  // do nothing
						else throw new NotImplementedException();
						currentDigit *= 2;
					}
				}
			}
			else if (type == NumberType.Float)
			{
				double v;
				if (double.TryParse(val, System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out v))
				{
					DoubleValue = v;
				}
				else
				{
					Error = "Literal is too big to fit in a double";
				}
			}
		}

		public override string ToString()
		{
			return StringValue;
		}

		// TODO: why do you want to use it? just get size, sign and value and alloc it
		//public ulong ToUlong()
		//{
		//	if (IntValue > long.MaxValue)
		//		return (ulong)IntValue;

		//	unsafe
		//	{
		//		long p = (long)IntValue;
		//		return *(ulong*)&p;
		//	}
		//}

		//public long ToLong()
		//{
		//	return (long)IntValue;
		//}

		public double ToDouble()
		{
			if (Type == NumberType.Int)
				return (double)IntValue;
			else
				return DoubleValue;
		}

		public uint ToUInt()
		{
			return (uint)IntValue;
		}

		public ulong ToULong()
		{
			return (ulong)IntValue;
		}

		public NumberData Negate()
		{
			switch (Type)
			{
				case NumberType.Int:
					return new NumberData
					{
						StringValue = "-" + StringValue,
						IntBase = IntBase,
						IntValue = -IntValue,
						Type = Type
					};


				case NumberType.Float:
					return new NumberData
					{
						StringValue = "-" + StringValue,
						IntBase = IntBase,
						DoubleValue = -DoubleValue,
						Type = Type
					};

				default:
					throw new NotImplementedException();
			}
		}

		public void ResetError()
		{
			Error = string.Empty;
		}

		public override bool Equals(object obj)
		{
			if (obj is NumberData o) return this == o;
			return false;
		}

		public static NumberData FromInt(BigInteger num)
		{
			return new NumberData
			{
				IntBase = 10,
				StringValue = num.ToString(CultureInfo.InvariantCulture),
				Type = NumberType.Int,
				IntValue = num,
				DoubleValue = default,
				Error = null,
			};
		}

		public static NumberData FromDouble(double num)
		{
			return new NumberData
			{
				IntBase = 10,
				StringValue = num.ToString(CultureInfo.InvariantCulture),
				Type = NumberType.Float,
				IntValue = default,
				DoubleValue = num,
				Error = null
			};
		}

		public override int GetHashCode()
		{
			var hashCode = 1753786285;
			hashCode = hashCode * -1521134295 + Type.GetHashCode();
			hashCode = hashCode * -1521134295 + EqualityComparer<BigInteger>.Default.GetHashCode(IntValue);
			hashCode = hashCode * -1521134295 + DoubleValue.GetHashCode();
			return hashCode;
		}

		public bool Equals([AllowNull] NumberData other)
		{
			return this == other;
		}

		public static implicit operator NumberData(BigInteger bi) => FromInt(bi);
		public static implicit operator NumberData(int i) => FromInt(new BigInteger(i));
		public static implicit operator NumberData(long l) => FromInt(new BigInteger(l));
		public static implicit operator NumberData(double d) => FromDouble(d);

		public static bool operator ==(NumberData a, NumberData b)
		{
			if (a.Type == NumberType.Int && b.Type == NumberType.Int) return a.IntValue == b.IntValue;
			return a.ToDouble() == b.ToDouble();
		}
		public static bool operator !=(NumberData a, NumberData b) => !(a == b);
		public static bool operator >(NumberData a, NumberData b) => (a.Type == NumberType.Int && b.Type == NumberType.Int) ? a.IntValue > b.IntValue : a.ToDouble() > b.ToDouble();
		public static bool operator >=(NumberData a, NumberData b) => (a.Type == NumberType.Int && b.Type == NumberType.Int) ? a.IntValue >= b.IntValue : a.ToDouble() >= b.ToDouble();
		public static bool operator <(NumberData a, NumberData b) => (a.Type == NumberType.Int && b.Type == NumberType.Int) ? a.IntValue < b.IntValue : a.ToDouble() < b.ToDouble();
		public static bool operator <=(NumberData a, NumberData b) => (a.Type == NumberType.Int && b.Type == NumberType.Int) ? a.IntValue <= b.IntValue : a.ToDouble() <= b.ToDouble();

		public static NumberData operator +(NumberData a, NumberData b) => (a.Type == NumberType.Int && b.Type == NumberType.Int) ? FromInt(a.IntValue + b.IntValue) : FromDouble(a.ToDouble() + b.ToDouble());
		public static NumberData operator -(NumberData a, NumberData b) => (a.Type == NumberType.Int && b.Type == NumberType.Int) ? FromInt(a.IntValue - b.IntValue) : FromDouble(a.ToDouble() - b.ToDouble());
		public static NumberData operator *(NumberData a, NumberData b) => (a.Type == NumberType.Int && b.Type == NumberType.Int) ? FromInt(a.IntValue * b.IntValue) : FromDouble(a.ToDouble() * b.ToDouble());
		public static NumberData operator /(NumberData a, NumberData b) => (a.Type == NumberType.Int && b.Type == NumberType.Int) ? FromInt(a.IntValue / b.IntValue) : FromDouble(a.ToDouble() / b.ToDouble());
		public static NumberData operator %(NumberData a, NumberData b) => (a.Type == NumberType.Int && b.Type == NumberType.Int) ? FromInt(a.IntValue % b.IntValue) : FromDouble(a.ToDouble() % b.ToDouble());


		public bool IsInRangeOfType(HapetType hptType)
		{
			double min = 0;
			double max = 0;

			// checking what is the type
			if (hptType is IntType intType)
			{
				min = (double)intType.MinValue;
				max = (double)intType.MaxValue;
			}
			else if (hptType is CharType charType)
			{
				min = (double)charType.MinValue;
				max = (double)charType.MaxValue;
			}
			else if (hptType is FloatType floatType)
			{
				min = floatType.MinValue;
				max = floatType.MaxValue;
			}
			else
			{
				Error = "The type cannot be checked for range";
			}

			var val = ToDouble();
			return val <= max && (val >= min);
		}
	}
}
