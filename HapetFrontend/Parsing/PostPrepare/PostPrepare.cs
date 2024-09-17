namespace HapetFrontend.Parsing.PostPrepare
{
	public partial class PostPrepare
	{
		private readonly Compiler _compiler;

		public PostPrepare(Compiler compiler)
		{
			_compiler = compiler;
		}

		public void StartPreparation()
		{
			PostPrepareScoping();
			PostPrepareTypeInference();
		}
	}
}
