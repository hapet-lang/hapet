using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Types;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace HapetFrontend.Helpers
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

        public static void ReplaceWithCasts(this List<AstArgumentExpr> args, List<AstExpression> casts)
        {
            for (int i = 0; i < args.Count; ++i)
            {
                args[i].Expr = casts[i];
                args[i].OutType = casts[i].OutType;
            }
        }

        public static string GetArgsString(this List<AstArgumentExpr> args, HapetType containingClass = null)
        {
            // WARN: ':' is used so linker would work :)))
            StringBuilder sb = new StringBuilder();
            sb.Append('(');

            // class is passed as a first parameter
            if (containingClass != null)
            {
                sb.Append(containingClass.ToString());
                if (args.Count > 0)
                    sb.Append(':');
            }

            for (int i = 0; i < args.Count; i++)
            {
                var a = args[i];
                sb.Append(a.OutType != null ? a.OutType.ToString() : string.Empty);

                if (i != args.Count - 1)
                    sb.Append(':');
            }
            sb.Append(')');
            return sb.ToString();
        }

        public static string GetParamsString(this List<AstParamDecl> pars, HapetType containingClass = null)
        {
            // WARN: ':' is used so linker would work :)))
            StringBuilder sb = new StringBuilder();
            sb.Append('(');

            // class is passed as a first parameter
            if (containingClass != null)
            {
                sb.Append(containingClass.ToString());
                if (pars.Count > 0)
                    sb.Append(':');
            }

            for (int i = 0; i < pars.Count; i++)
            {
                var p = pars[i];
                sb.Append(p.Type.OutType.ToString());

                if (i != pars.Count - 1)
                    sb.Append(':');
            }
            sb.Append(')');
            return sb.ToString();
        }

        public static List<AstDeclaration> GetStructFields(this List<AstDeclaration> delcs)
        {
            return delcs.Where(x =>
                (x is AstVarDecl vD &&
                !vD.SpecialKeys.Contains(Parsing.TokenType.KwStatic) &&
                !vD.SpecialKeys.Contains(Parsing.TokenType.KwConst)) &&
                x is not AstPropertyDecl).ToList();
        }

        public static AstDeclaration GetSameDeclByTypeAndName(this List<AstDeclaration> delcs, AstDeclaration decl)
        {
            return delcs.FirstOrDefault(x => x.Name.Name == decl.Name.Name && x.Type.OutType == decl.Type.OutType);
        }

        public static AstVarDecl GetSameDeclByTypeAndName(this List<AstVarDecl> delcs, AstDeclaration decl)
        {
            return delcs.FirstOrDefault(x => x.Name.Name == decl.Name.Name && x.Type.OutType == decl.Type.OutType);
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

        public static bool ValidateFilePath(string dir, string filePath, bool isRel, IMessageHandler mh, (string file, ILocation loc)? from, out string path)
        {
            path = filePath;

            // TODO: check is obsolete probably - remove if everything would be ok :)
            //var extension = Path.GetExtension(path);
            //if (string.IsNullOrEmpty(extension))
            //{
            //    path += ".hpt";
            //}
            //else if (extension != ".hpt")
            //{
            //    mh.ReportMessage($"Invalid extension '{extension}'. Hapet source files must have the extension .hpt");
            //    return false;
            //}

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
                    mh.ReportMessage(from.Value.file, from.Value.loc, $"File '{path}' does not exist", ErrorCode.Get(CTEN.FullPathToHapetFileNotFound));
                }
                else
                {
                    mh.ReportMessage($"File '{path}' does not exist", ErrorCode.Get(CTEN.FullPathToHapetFileNotFound));
                }

                return false;
            }

            return true;
        }

        public static string GetNamespace(string projectPath, string rootNamespace, string filePath)
        {
            var projectPathNormalized = Path.GetDirectoryName(projectPath).Replace("\\", "/").TrimEnd('/');
            var filePathNormalized = Path.GetDirectoryName(filePath).Replace("\\", "/").TrimEnd('/');

            StringBuilder uniquePath = new StringBuilder();
            for (int i = 0; i < filePathNormalized.Length; ++i)
            {
                if (i >= projectPathNormalized.Length)
                {
                    uniquePath.Append(filePathNormalized[i]);
                }
            }

            var uniquePathNormalized = uniquePath.ToString().Trim('/').Replace('/', '.');
            // it could be empty if the file is in the same directory as project file
            if (string.IsNullOrWhiteSpace(uniquePathNormalized))
            {
                return rootNamespace;
            }

            return $"{rootNamespace}.{uniquePathNormalized}";
        }
    }
}
