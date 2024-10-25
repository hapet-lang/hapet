namespace HapetFrontend
{
	public enum CompilerErrors
	{
		Ok = 0,
		ParsingError = 1,
		PostPrepareError = 2,
		CodeGenerationError = 3,
		ProjectFileParseError = 4,

		HapetCommandError = 5,
		HapetCommandParamsError = 6,
	}
}
