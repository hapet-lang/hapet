using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast;
using HapetFrontend.Scoping;
using System.Text;
using HapetFrontend.Extensions;
using System.Xml.Linq;
using HapetFrontend.Types;

namespace HapetFrontend.Helpers
{
    public static class GenericsHelper
    {
        /// <summary>
        /// 'true' if accessing smth like List{T} where T is a generic type
        /// </summary>
        /// <param name="genericTypes"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Makes from List-T- to List-int-
        /// But really just does from List_GB_DAWDAWDAWD_g_T_GE to List_GB_int_GE
        /// </summary>
        /// <param name="decl">The decl</param>
        /// <param name="generics">List of real types</param>
        /// <returns>New name</returns>
        public static string GetRealFromGenericName(AstDeclaration decl, List<AstNestedExpr> generics)
        {
            if (decl is AstFuncDecl func)
            {
                // cringe
                int indexOfParen = func.Name.Name.IndexOf('(');
                string name = func.Name.Name;
                if (indexOfParen != -1)
                    name = name.Substring(0, indexOfParen);

                // also reset generic shite if exists
                if (name.Contains(GENERIC_BEGIN))
                {
                    name = name.Substring(0, name.IndexOf(GENERIC_BEGIN));
                }

                string realName = GetRealFromGenericName(name, generics);

                // cringe
                if (indexOfParen != -1)
                    realName += func.Name.Name.Substring(indexOfParen, func.Name.Name.Length - indexOfParen);
                return realName;
            }
            else if (decl is AstClassDecl || decl is AstPropertyDecl || decl is AstStructDecl || decl is AstDelegateDecl)
            {
                return GetRealFromGenericName(decl.Name.Name, generics);
            }
            return decl.Name.Name;
        }

        public static string GetRealFromGenericName(string namee, List<AstNestedExpr> generics)
        {
            if (generics.Count == 0)
                return namee;

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

        #region Prettifier
        public static string GetPrettyGenericName(string name)
        {
            // getting the index of func/cls delim
            int delimIndex = name.IndexOf("::");
            if (delimIndex == -1)
                return GetPrettyGenericImplName(name); // probably pure func

            string otherPart = name.Substring(delimIndex + 2);
            int parenIndex = otherPart.IndexOf('(');
            string funcNamePart = otherPart.Substring(0, parenIndex);

            return GetPrettyGenericImplName(funcNamePart);
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

            int currentIndex = genIndex + GENERIC_BEGIN.Length;
            while (currentIndex + 3 < namee.Length)
            {
                string toCheck = string.Concat(namee[currentIndex], namee[currentIndex + 1], namee[currentIndex + 2], namee[currentIndex + 3]);
                if (toCheck == GENERIC_DELIM)
                {
                    sb.Append(':'); // param delim
                    sb.Append(GetPrettyGenericTypeName(currentName.ToString())); // the name
                    currentName.Clear();

                    currentIndex += GENERIC_DELIM.Length;
                }
                else if (toCheck == GENERIC_BEGIN)
                {
                    sb.Append('<'); // begin
                    currentName.Clear();

                    currentIndex += GENERIC_BEGIN.Length;
                }
                else if (toCheck == GENERIC_END)
                {
                    sb.Append(GetPrettyGenericTypeName(currentName.ToString())); // the name
                    sb.Append('>'); // end
                    currentName.Clear();

                    currentIndex += GENERIC_END.Length;
                }
                else
                {
                    currentName.Append(namee[currentIndex]);
                    currentIndex++;
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
        #endregion

        /// <summary>
        /// Replaces all System.Anime::Func(Pivo) with just Func and etc.
        /// Saves all generic entries!!!
        /// </summary>
        /// <param name="decl"></param>
        public static void ResetDeclarationNames(AstDeclaration decl)
        {
            if (decl is AstClassDecl || decl is AstStructDecl)
            {
                decl.Name = decl.Name.GetCopy(GetResetedName(decl));
                var decls = decl is AstClassDecl ? (decl as AstClassDecl).Declarations : (decl as AstStructDecl).Declarations;
                foreach (var dec in decls)
                {
                    dec.Name = dec.Name.GetCopy(GetResetedName(dec));
                }
            }
            else if (decl is AstFuncDecl || decl is AstDelegateDecl)
            {
                decl.Name = decl.Name.GetCopy(GetResetedName(decl));
            }

            static string GetResetedName(AstDeclaration d)
            {
                if (d is AstClassDecl || d is AstStructDecl || d is AstDelegateDecl)
                {
                    return d.Name.Name.GetClassNameWithoutNamespace();
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

        /// <summary>
        /// Creates an AstIdExpr (or generic one) from string like
        /// 'Pivo' or 'Pivo<Cringe>'
        /// </summary>
        /// <param name="name"></param>
        /// <param name="location"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Inversed func of <see cref="GetAstIdFromName"/>
        /// </summary>
        /// <param name="idExpr"></param>
        /// <returns></returns>
        public static string GetNameFromAst(AstIdExpr idExpr)
        {
            if (idExpr is not AstIdGenericExpr genId)
                return GetPrettyGenericName(idExpr.Name);

            StringBuilder sb = new StringBuilder("<");
            for (int i = 0; i < genId.GenericRealTypes.Count; ++i)
            {
                var g = genId.GenericRealTypes[i];
                sb.Append(g.GetNested().TryFlatten(null, null));
                if (i < genId.GenericRealTypes.Count - 1)
                    sb.Append(", ");
            }
            sb.Append('>');
            return $"{GetPrettyGenericName(genId.Name)}{sb}";
        }

        /// <summary>
        /// Generates pretty string from <see cref="HapetType"/>. 
        /// At least used for metadata gen
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetNameFromType(HapetType type)
        {
            if (type is PointerType ptr)
            {
                return $"{GetNameFromType(ptr.TargetType)}*";
            }
            else if (type is ArrayType arr)
            {
                return $"{GetNameFromType(arr.TargetType)}[]";
            }
            else if (type is ClassType || type is StructType)
            {
                return GetPrettyGenericName(type.ToString());
            }
            return type.ToString();
        }

        /// <summary>
        /// Converts generic astId into smth like
        /// 'Pivo_GB__GD__GE_'
        /// Used at least for generic type/func searching via candidating
        /// </summary>
        /// <param name="idExpr"></param>
        /// <returns></returns>
        public static string GetCringeGenericName(AstIdExpr idExpr)
        {
            if (idExpr is not AstIdGenericExpr genExpr)
                return idExpr.Name;

            List<AstNestedExpr> tmp = Enumerable.Repeat(new AstNestedExpr(null, null), genExpr.GenericRealTypes.Count).ToList();
            return GetRealFromGenericName(genExpr.Name, tmp);
        }

        /// <summary>
        /// Returns amount of generics in string
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static int GetGenericsAmount(this string name)
        {
            int amount = 0;

            // getting the index of the first entry
            int genIndex = name.IndexOf(GENERIC_BEGIN);
            if (genIndex == -1)
                return 0; // there are no generics

            // at least one generic exists if the _GB_ exists
            amount++;

            // if > 0 then there was a _GB_ and no _GE_ yet. if < 0 - probably error :)
            int currentState = 0;
            int currentIndex = (genIndex + GENERIC_BEGIN.Length);

            while (currentIndex + 3 < name.Length)
            {
                string toCheck = string.Concat(name[currentIndex], name[currentIndex + 1], name[currentIndex + 2], name[currentIndex + 3]);
                if (currentState == 0 && toCheck == GENERIC_DELIM)
                {
                    amount++;
                    currentIndex += GENERIC_DELIM.Length;
                    continue;
                }
                else if (currentState == 0 && toCheck == GENERIC_END)
                {
                    // all found
                    break;
                }
                else if (toCheck == GENERIC_BEGIN)
                {
                    currentState++;
                }
                else if (toCheck == GENERIC_END)
                {
                    currentState--;
                }
                currentIndex++;
            }
            return amount;
        }
    }
}
