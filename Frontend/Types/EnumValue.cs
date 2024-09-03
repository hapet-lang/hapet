namespace Frontend.Types
{
	public class EnumValue
	{
		// TODO: many shite removed
		public EnumType Type { get; }
		public long Tag { get; }

		public EnumValue(EnumType type)
		{
			Type = type;
			Tag = 0;
		}
	}
}
