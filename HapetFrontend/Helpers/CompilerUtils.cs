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
                // pass value of argument on cast!
                AstExpression val = casts[i];
                if (casts[i] is AstArgumentExpr argE)
                    val = argE.Expr;

                args[i].Expr = val;
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
                sb.Append(HapetType.AsString(containingClass));
                if (args.Count > 0)
                    sb.Append(':');
            }

            for (int i = 0; i < args.Count; i++)
            {
                var a = args[i];
                sb.Append(a.OutType != null ? HapetType.AsString(a.OutType) : string.Empty);

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
                sb.Append(HapetType.AsString(containingClass));
                if (pars.Count > 0)
                    sb.Append(':');
            }

            for (int i = 0; i < pars.Count; i++)
            {
                var p = pars[i];
                sb.Append(p.Type.OutType == null ? "" : HapetType.AsString(p.Type.OutType));

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

        public static AstFuncDecl GetSameByNameAndTypes(this List<AstFuncDecl> delcs, AstFuncDecl searchFunc, out int index, bool skipFirst = true)
        {
            index = -1;
            // there is already params type in name like
            // TestClass::AnimeFunc(int:PivoCls)
            string searchName = searchFunc.Name.Name.Split("::")[1];
            if (skipFirst)
            {
                // remove the first param
                searchName = GetSkipped(searchName);
            }
            for (int i = 0; i < delcs.Count; ++i)
            {
                var x = delcs[i];
                string currName = x.Name.Name.Split("::")[1];
                if (skipFirst)
                {
                    // remove the first param
                    currName = GetSkipped(currName);
                }
                if ((currName == searchName) && x.Returns.OutType == searchFunc.Returns.OutType)
                {
                    index = i;
                    return x;
                }
            }
            return null;

            static string GetSkipped(string name)
            {
                var parenIndex = name.IndexOf('(');
                var firstPart = name.Substring(0, parenIndex + 1);
                var secondPart = name.Substring(parenIndex + 1);
                var skipped = string.Concat(secondPart.SkipWhile(x => x != ':' && x != ')'));
                if (skipped[0] == ':')
                    skipped = skipped.Substring(1);
                return $"{firstPart}{skipped}";
            }
        }

        public static AstDeclaration GetSameDeclByTypeAndName(this List<AstDeclaration> decls, AstDeclaration decl)
        {
            return decls.FirstOrDefault(x => x.Name.Name == decl.Name.Name && x.Type.OutType == decl.Type.OutType);
        }

        public static AstVarDecl GetSameDeclByTypeAndName(this List<AstVarDecl> decls, AstDeclaration decl)
        {
            return decls.FirstOrDefault(x => x.Name.Name == decl.Name.Name && x.Type.OutType == decl.Type.OutType);
        }

        public static AstPropertyDecl GetSameDeclByTypeAndName(this List<AstPropertyDecl> decls, AstDeclaration decl, out int index)
        {
            for (int i = 0; i < decls.Count; ++i)
            {
                var x = decls[i];
                if (x.Name.Name == decl.Name.Name && x.Type.OutType == decl.Type.OutType)
                {
                    index = i;
                    return x;
                }
            }
            index = -1;
            return null;
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
                    mh.ReportMessage(from.Value.file, from.Value.loc, [path], ErrorCode.Get(CTEN.FullPathToHapetFileNotFound));
                }
                else
                {
                    mh.ReportMessage([path], ErrorCode.Get(CTEN.FullPathToHapetFileNotFound));
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
