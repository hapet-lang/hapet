using Frontend.Parsing.Entities;
using Frontend.Scoping.Entities;

namespace Frontend.Types
{
	public abstract class HapetType
	{
		/// <summary>
		/// Like "class Pivo<T>" where Pivo is a ClassType and IsGenericType - true
		/// </summary>
		public virtual bool IsGenericType { get; } = false;

		private int _size = -1;
		private int _alignment = -1;

		protected HapetType(int size, int align)
		{
			if (size < 0)
				throw new ArgumentOutOfRangeException(nameof(size));
			if (align < 0)
				throw new ArgumentOutOfRangeException(nameof(align));
			_size = size;
			_alignment = align;
		}

		protected HapetType()
		{
		}

		public int GetSize() => _size;
		public int GetAlignment() => _alignment;

		public void SetSizeAndAlignment(int size, int align)
		{
			_size = size;
			_alignment = align;
		}

		public static bool operator ==(HapetType a, HapetType b)
		{
			if (a is null && b is null) return true;
			if (a is null || b is null) return false;
			return a.Equals(b);
		}

		public static bool operator !=(HapetType a, HapetType b)
		{
			return !(a == b);
		}

		public override bool Equals(object obj)
		{
			return base.Equals(obj);
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		public virtual int Match(HapetType concrete, Dictionary<string, (HapetType type, object value)> genericTypes)
		{
			if (concrete is ReferenceType r)
				concrete = r.TargetType;

			if (this == concrete)
				return 0;
			return -1;
		}

		#region Other Matches
		public static int GenericValuesMatch((HapetType type, object value) a, (HapetType type, object value) b, Dictionary<string, (HapetType type, object value)> polyTypes)
		{
			switch (a.value, b.value)
			{
				case (HapetType ta, HapetType tb):
					return ta.Match(tb, polyTypes);

				case (NumberData va, NumberData vb): return va == vb ? 0 : -1;
				case (bool va, bool vb): return va == vb ? 0 : -1;
				case (string va, string vb): return va == vb ? 0 : -1;
				case (char va, char vb): return va == vb ? 0 : -1;

				// generic values
				case (GenericValue _, object _): return 1;
				case (object _, GenericValue _): return 1;

				default:
					return -1;
			}
		}
		#endregion
	}
}
