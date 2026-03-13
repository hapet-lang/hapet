<p align="center">
  <img alt="hapet logo" src="build-resources/logo.png" width="145px" />
  <h1 align="center">The hapet Compiler Platform</h1>
</p>

This repository contains a compiler project that is used to compile **hapet** programming language projects.  
**hapet** programming language is almost the same as [C# programming language](https://github.com/dotnet/csharplang) but with [small differencies](https://hapetlang.com/docs/diffs/).  

## Installation 
### Download precompiled installers 
You can [download precompiled installer](https://hapetlang.com/#downloads) for you operating system.  
### Running from source 
You can run compiler from source code:
```bash
dotnet restore
cd HapetCompiler
dotnet run
```

## Example Hello World  
```csharp
using System;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("Hello world!");
        return 0;
    }
}
```

## Documentation
Visit [hapet documentation](https://hapetlang.com/docs/get-started/) to get started with **hapet** programming language.  

## Licenses
- This repository is under [MIT license](https://github.com/hapet-lang/hapet/blob/main/LICENSE);
- Lexer and Parser are almost fully rewritten from [CheezLang](https://github.com/CheezLang/CheezLang). Their [license](https://github.com/CheezLang/CheezLang/blob/master/LICENSE);
- Compiler backend uses [LLVM project](https://github.com/llvm/llvm-project) to emit *.obj*/*.o* files. Their [license](https://github.com/llvm/llvm-project/blob/main/LICENSE.TXT);
- Some Standard Library files are rewritten [Roslyn](https://github.com/dotnet/roslyn) files. Their [license](https://github.com/dotnet/roslyn/blob/main/License.txt) for the files;
- Installer creation pipeline uses [HashComputer](https://github.com/CrackAndDie/HashComputer) project to make faster **hapet** compiler updates. Their [license](https://github.com/CrackAndDie/HashComputer/blob/main/LICENSE);
- This repository uses [xwin project](https://github.com/Jake-Shadle/xwin) to distribute Windows required *.lib* files with compiler. By using **hapet** compiler you agree with [xwin](https://github.com/Jake-Shadle/xwin/blob/main/LICENSE-MIT), [Microsoft Windows SDK](https://github.com/microsoft/WindowsAppSDK/blob/main/LICENSE) and [Microsoft CRT](https://visualstudio.microsoft.com/ru/license-terms/) licenses.
