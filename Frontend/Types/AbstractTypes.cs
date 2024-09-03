namespace Frontend.Types
{
	public class ThisType : HapetType
	{
		public override bool IsGenericType => false;
		public HapetType ClassType { get; }

		public ThisType(HapetType classType) : base(0, 1)
		{
			ClassType = classType;
		}

		public override string ToString()
		{
			return $"this -> ({ClassType})";
		}
	}
	
	/// <summary>
	/// Like T in "public class Anime<T> ..."
	/// </summary>
	public class GenericType : HapetType
	{
		public override bool IsGenericType => true;

		public string GenericName { get; }

		public GenericType(string genericTypeName)
		{
			GenericName = genericTypeName;
		}

		public override string ToString()
		{
			return $"generic {GenericName}";
		}
	}

	public class HapetTypeType : HapetType
	{
		public static HapetTypeType Instance { get; } = new HapetTypeType();

		public override string ToString() => "type";

		private HapetTypeType() : base(0, 1) { }
	}
}
