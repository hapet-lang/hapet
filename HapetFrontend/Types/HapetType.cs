namespace HapetFrontend.Types
{
	public abstract class HapetType
	{
		/// <summary>
		/// Like "class Pivo<T>" where Pivo is a ClassType and IsGenericType - true (TODO: not sure)
		/// </summary>
		public virtual bool IsGenericType { get; } = false;

		public static int PointerSize
		{
			get
			{
				
			}
		}

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
	}
}
