using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Types;
using System.Diagnostics;
using System.Text;

namespace HapetFrontend
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class SkipInStackFrameAttribute : Attribute
	{ }

	public static class CompilerUtils
	{
		#region Extensions
		public static string PathNormalize(this string path)
		{
			return Path.GetFullPath(new Uri(path).LocalPath)
					   .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		}

		public static string GetArgsString(this List<AstArgumentExpr> args, HapetType containingClass = null)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append('(');

			// class is passed as a first parameter
			if (containingClass != null)
			{
				sb.Append(containingClass.ToString());
				if (args.Count > 0)
					sb.Append(", ");
			}

			for (int i = 0; i < args.Count; i++)
			{
				var a = args[i];
				sb.Append(a.OutType.ToString());

				if (i != args.Count - 1)
					sb.Append(", ");
			}
			sb.Append(')');
			return sb.ToString();
		}

		public static string GetParamsString(this List<AstParamDecl> pars)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append('(');
			for (int i = 0; i < pars.Count; i++)
			{
				var p = pars[i];
				sb.Append(p.Type.OutType.ToString());

				if (i != pars.Count - 1)
					sb.Append(", ");
			}
			sb.Append(')');
			return sb.ToString();
		}
		#endregion

		#region Parsing shite helpers
		[DebuggerStepThrough]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Exception not required. This is for error reporting only.")]
		public static (string function, string file, int line)? GetCallingFunction()
		{
			try
			{
				var trace = new StackTrace(true);
				var frames = trace.GetFrames();

				foreach (var frame in frames)
				{
					var method = frame.GetMethod();
					var attribute = method.GetCustomAttributesData().FirstOrDefault(d => d.AttributeType == typeof(SkipInStackFrameAttribute));
					if (attribute != null)
						continue;

					return (method.Name, frame.GetFileName(), frame.GetFileLineNumber());
				}
			}
			catch (Exception)
			{ }

			return null;
		}
		#endregion

		public static bool ValidateFilePath(string dir, string filePath, bool isRel, IErrorHandler eh, (string file, ILocation loc)? from, out string path)
		{
			path = filePath;

			var extension = Path.GetExtension(path);
			if (string.IsNullOrEmpty(extension))
			{
				path += ".hpt";
			}
			else if (extension != ".hpt")
			{
				eh.ReportError($"Invalid extension '{extension}'. Hapet source files must have the extension .hpt");
				return false;
			}

			if (isRel)
			{
				path = Path.Combine(dir, path);
			}

			path = Path.GetFullPath(path);
			path = path.PathNormalize();

			if (!File.Exists(path))
			{
				if (from != null)
				{
					eh.ReportError(from.Value.file, from.Value.loc, $"File '{path}' does not exist");
				}
				else
				{
					eh.ReportError($"File '{path}' does not exist");
				}

				return false;
			}

			return true;
		}

		public static Process StartProcess(string exe, List<string> argList = null, string workingDirectory = null, DataReceivedEventHandler stdout = null, DataReceivedEventHandler stderr = null)
		{
			argList = argList ?? new List<string>();
			var args = string.Join(" ", argList.Select(a =>
			{
				if (a.Contains(" ", StringComparison.InvariantCulture))
					return $"\"{a}\"";
				return a;
			}));
			return StartProcess(exe, args, workingDirectory, stdout, stderr);
		}

		public static Process StartProcess(string exe, string args = null, string workingDirectory = null, DataReceivedEventHandler stdout = null, DataReceivedEventHandler stderr = null, bool useShellExecute = false, bool createNoWindow = true)
		{
			// Console.WriteLine($"{exe} {args}");

			var process = new Process();
			process.StartInfo.FileName = exe;
			if (workingDirectory != null)
				process.StartInfo.WorkingDirectory = workingDirectory;
			if (args != null)
				process.StartInfo.Arguments = args;
			process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
			process.StartInfo.UseShellExecute = useShellExecute;
			process.StartInfo.CreateNoWindow = createNoWindow;

			if (stdout != null)
			{
				process.StartInfo.RedirectStandardOutput = true;
				process.OutputDataReceived += stdout;
			}

			if (stderr != null)
			{
				process.StartInfo.RedirectStandardError = true;
				process.ErrorDataReceived += stderr;
			}

			process.Start();

			if (stdout != null)
				process.BeginOutputReadLine();
			if (stderr != null)
				process.BeginErrorReadLine();

			return process;
		}
	}
}
