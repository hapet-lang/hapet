using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast;
using HapetFrontend.Scoping;
using System.Text;
using HapetFrontend.Extensions;
using System.Xml.Linq;

namespace HapetFrontend.Helpers
{
    public static class GenericsHelper
    {
        public const string GENERIC_TYPE_BEGIN = "_gb_";
        public const string GENERIC_TYPE_END = "_ge_";

        public const string GENERIC_BEGIN = "_GB_";
        public const string GENERIC_DELIM = "_GD_";
        public const string GENERIC_END = "_GE_";

        public static bool HasGenericTypesInRealTypes(List<AstNestedExpr> genericTypes)
        {
            bool hasGeneric = false;
            foreach (var g in genericTypes)
            {
                if (g.LeftPart == null && g.RightPart is AstIdExpr id)
                {
                    var smb = id.Scope.GetSymbol(id.Name);
                    if (smb is DeclSymbol dS && dS.Decl is AstClassDecl clsD && clsD.IsGenericType)
                    {
                        hasGeneric = true;
                        break;
                    }
                }
            }
            return hasGeneric;
        }

        public static string GetRealFromGenericName(AstDeclaration decl, List<AstNestedExpr> generics)
        {
            if (decl is AstFuncDecl func)
            {
                // cringe
                string name = func.Name.Name;
                int indexOfParen = 0;
                bool containsClsName = func.Name.Name.Contains("::");
                if (containsClsName)
                {
                    indexOfParen = func.Name.Name.IndexOf('(');
                    name = func.Name.Name.Substring(0, indexOfParen);
                }

                // also reset generic shite if exists
                if (name.Contains(GENERIC_BEGIN))
                {
                    name = name.Substring(0, name.IndexOf(GENERIC_BEGIN));
                }

                string realName = GetRealFromGenericName(name, generics);

                // cringe
                if (containsClsName)
                    realName += func.Name.Name.Substring(indexOfParen, func.Name.Name.Length - indexOfParen);

                return realName;
            }
            else if (decl is AstClassDecl cls)
            {
                return GetRealFromGenericName(cls.Name.Name, generics);
            }
            return decl.Name.Name;
        }

        public static string GetRealFromGenericName(string namee, List<AstNestedExpr> generics)
        {
            StringBuilder sb = new StringBuilder(namee);
            sb.Append(GENERIC_BEGIN);
            for (int i = 0; i < generics.Count; ++i)
            {
                var g = generics[i];
                if (g.RightPart is AstIdExpr idExpr)
                {
                    sb.Append(idExpr.FindSymbol.Name);
                }

                // if not last - append delimeter
                if (i != generics.Count - 1)
                    sb.Append(GENERIC_DELIM);
            }
            sb.Append(GENERIC_END);
            return sb.ToString();
        }

        public static string GetPrettyGenericFuncName(string name)
        {
            // getting the index of func/cls delim
            int delimIndex = name.IndexOf("::");
            if (delimIndex == -1)
                return GetPrettyGenericImplName(name); // probably pure func

            string otherPart = name.Substring(delimIndex + 2);

            StringBuilder sb = new StringBuilder();

            int parenIndex = otherPart.IndexOf('(');
            string funcNamePart = otherPart.Substring(0, parenIndex);

            sb.Append(GetPrettyGenericImplName(funcNamePart));

            return sb.ToString();
        }

        public static string GetPrettyGenericImplName(string namee)
        {
            // getting the index of the first entry
            int genIndex = namee.IndexOf(GENERIC_BEGIN);
            if (genIndex == -1)
                return GetPrettyGenericTypeName(namee); // there are no generics

            StringBuilder sb = new StringBuilder(namee.Substring(0, genIndex));
            sb.Append('<');

            StringBuilder currentName = new StringBuilder();

            string currentSearchString = namee[(genIndex + GENERIC_BEGIN.Length)..];
            while (currentSearchString.Length > 0)
            {
                if (currentSearchString.StartsWith(GENERIC_DELIM))
                {
                    sb.Append(':'); // param delim
                    sb.Append(GetPrettyGenericTypeName(currentName.ToString())); // the name

                    currentName.Clear();
                    currentSearchString = currentSearchString[GENERIC_DELIM.Length..];
                }
                else if (currentSearchString.StartsWith(GENERIC_BEGIN))
                {
                    sb.Append('<'); // begin

                    currentName.Clear();
                    currentSearchString = currentSearchString[GENERIC_BEGIN.Length..];
                }
                else if (currentSearchString.StartsWith(GENERIC_END))
                {
                    sb.Append(GetPrettyGenericTypeName(currentName.ToString())); // the name
                    sb.Append('>'); // end

                    currentName.Clear();
                    currentSearchString = currentSearchString[GENERIC_END.Length..];
                }
                else
                {
                    currentName.Append(currentSearchString[0]);
                    currentSearchString = currentSearchString[1..];
                }
            }
            sb.Append(currentName); // ostatki sladki
            return sb.ToString();
        }

        public static string GetPrettyGenericTypeName(string namee)
        {
            if (!namee.Contains(GENERIC_TYPE_BEGIN))
                return namee;

            var gbInd = namee.IndexOf(GENERIC_TYPE_BEGIN);
            var geInd = namee.IndexOf(GENERIC_TYPE_END);
            return namee.Substring(gbInd + GENERIC_TYPE_BEGIN.Length, (geInd - (gbInd + GENERIC_TYPE_BEGIN.Length)));
        }

        public static void ResetDeclarationNames(AstDeclaration decl)
        {
            if (decl is AstClassDecl clsDecl)
            {
                clsDecl.Name = clsDecl.Name.GetCopy(GetName(clsDecl));
                foreach (var dec in clsDecl.Declarations)
                {
                    dec.Name = dec.Name.GetCopy(GetName(dec));
                }
            }
            else if (decl is AstFuncDecl funcDecl)
            {
                funcDecl.Name = funcDecl.Name.GetCopy(GetName(funcDecl));
            }

            static string GetName(AstDeclaration d)
            {
                if (d is AstClassDecl c)
                {
                    return c.Name.Name.GetClassNameWithoutNamespace();
                }
                else if (d is AstFuncDecl f)
                {
                    // check if it is really infered
                    if (f.Name.Name.Contains("::"))
                        return f.Name.Name.GetPureFuncName();
                }
                return d.Name.Name;
            }
        }

        public static AstIdExpr GetAstIdFromName(string name, ILocation location)
        {
            // no generics
            if (!name.Contains('<'))
                return new AstIdExpr(name, location);

            int genInd = name.IndexOf('<');
            string nameWithout = name.Substring(0, genInd);
            string genTypes = name.Substring(genInd + 1, name.Length - genInd - 2); // why -2? because we want to remove both < and >

            // generating/adding generics
            List<AstExpression> gens = new List<AstExpression>();
            foreach (var g in genTypes.Split(':'))
            {
                gens.Add(GetAstIdFromName(g, location));
            }

            var astGen = new AstIdGenericExpr(nameWithout, gens, location);
            return astGen;
        }
    }
}
