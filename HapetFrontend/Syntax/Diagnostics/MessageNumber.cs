namespace HapetFrontend.Syntax.Diagnostics
{
    // Compile Time Error Number
    public enum MessageNumber
    {
        Teapot = 0x0000,

        // project errors up to 0x1000
        FullPathToHapetFileNotFound = 0x0001,

        // warnings are from 0xB000
    }
}
