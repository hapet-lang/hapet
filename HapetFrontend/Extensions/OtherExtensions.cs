using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using System.Text;
using HapetFrontend.Entities;
using HapetFrontend.Errors;

namespace HapetFrontend.Extensions
{
    public static class OtherExtensions
    {
        public static bool Contains(this List<Token> tokens, TokenType tt)
        {
            return tokens.FirstOrDefault(x => x.Type == tt) != null;
        }

        public static Token GetType(this List<Token> tokens, TokenType tt)
        {
            return tokens.FirstOrDefault(x => x.Type == tt);
        }

        public static void Remove(this List<Token> tokens, TokenType tt, bool all = true)
        {
            foreach (var t in tokens.ToList())
            {
                if (t.Type == tt)
                {
                    tokens.Remove(t);
                    if (!all)
                        return;
                }
            }
        }

        #region Extensions
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
                // null is handled in another way
                var a = args[i];
                if (a.Expr is AstNullExpr)
                {
                    sb.Append("null");
                }
                else
                {
                    sb.Append(a.OutType != null ? HapetType.AsString(a.OutType) : string.Empty);
                }

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
                if (!p.IsArglist)
                    sb.Append(p.Type.OutType == null ? "" : HapetType.AsString(p.Type.OutType));
                else
                    sb.Append("arglist");

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
            AstFuncDecl bestMatch = null;
            // there is already params type in name like
            // TestClass::AnimeFunc(int:PivoCls)
            string searchName = searchFunc.Name.Name.GetPureFuncName();
            List<HapetType> types = searchFunc.Parameters.Select(x => x.Type?.OutType).ToList();
            if (skipFirst)
            {
                // remove the first param
                types = types.Skip(1).ToList();
            }
            for (int i = 0; i < delcs.Count; ++i)
            {
                var x = delcs[i];

                // return if the same
                if (x == searchFunc)
                {
                    index = i;
                    return x;
                }

                string currName = x.Name.Name.GetPureFuncName();
                List<HapetType> typesD = x.Parameters.Select(x => x.Type?.OutType).ToList();
                if (skipFirst)
                {
                    // remove the first param
                    typesD = typesD.Skip(1).ToList();
                }

                // check for parameter types
                bool areTypesTheSame = typesD.Count == types.Count;
                if (areTypesTheSame)
                    for (int j = 0; j < types.Count; ++j)
                    {
                        var t1 = types[j];
                        var t2 = typesD[j];
                        if (t1 != t2)
                        {
                            areTypesTheSame = false;
                            break;
                        }
                    }

                if ((currName == searchName) && x.Returns.OutType == searchFunc.Returns.OutType && areTypesTheSame)
                {
                    index = i;
                    bestMatch = x;
                }
            }

            // additional search for explicit declarations!!!
            // if any of them are like 'Namespace.BaseCls::Intrf.Func(...);'
            searchName = searchFunc.Name.Name.GetPureFuncName();
            string interfaceSearchName = "";
            if (searchFunc.Name.AdditionalData != null)
                interfaceSearchName = (searchFunc.Name.AdditionalData.OutType as ClassType).Declaration.Name.Name;
            string pureSearchName = searchName.GetClassNameWithoutNamespace();
            for (int i = 0; i < delcs.Count; ++i)
            {
                var x = delcs[i];
                string currName = x.Name.Name.GetPureFuncName();
                string interfaceName = "";
                if (x.Name.AdditionalData != null)
                    interfaceName = (x.Name.AdditionalData.OutType as ClassType).Declaration.Name.Name;
                string pureName = currName.GetClassNameWithoutNamespace();

                List<HapetType> typesD = x.Parameters.Select(x => x.Type?.OutType).ToList();
                if (skipFirst)
                {
                    // remove the first param
                    typesD = typesD.Skip(1).ToList();
                }

                // check for parameter types
                bool areTypesTheSame = typesD.Count == types.Count;
                if (areTypesTheSame)
                    for (int j = 0; j < types.Count; ++j)
                    {
                        var t1 = types[j];
                        var t2 = typesD[j];
                        if (t1 != t2)
                        {
                            areTypesTheSame = false;
                            break;
                        }
                    }

                bool areNamesEqual = false;
                if (string.IsNullOrWhiteSpace(interfaceSearchName) && !string.IsNullOrWhiteSpace(interfaceName))
                {
                    string parentSearch = searchFunc.Name.Name.GetClassNameFromFuncName();
                    if (parentSearch == interfaceName && pureName == pureSearchName)
                        areNamesEqual = true;
                }
                else if (!string.IsNullOrWhiteSpace(interfaceSearchName) && string.IsNullOrWhiteSpace(interfaceName))
                {
                    string parentSearch = x.Name.Name.GetClassNameFromFuncName();
                    if (parentSearch == interfaceSearchName && pureName == pureSearchName)
                        areNamesEqual = true;
                }

                if (areNamesEqual && x.Returns.OutType == searchFunc.Returns.OutType && areTypesTheSame)
                {
                    index = i;
                    bestMatch = x;
                }
            }
            return bestMatch;
        }

        public static AstDeclaration GetSameDeclByTypeAndNamePure(this List<AstDeclaration> decls, AstDeclaration decl, out int index)
        {
            for (int i = 0; i < decls.Count; ++i)
            {
                var x = decls[i];
                if (x.Name.Name == decl.Name.Name && x.Type.OutType == decl.Type.OutType)
                {
                    index = i;
                    return x;
                }

                // additional info checks
                string interfaceSearchName = "";
                if (decl.Name.AdditionalData != null)
                    interfaceSearchName = (decl.Name.AdditionalData.OutType as ClassType).Declaration.Name.Name;
                string pureSearchName = decl.Name.Name.GetClassNameWithoutNamespace();
                string interfaceName = "";
                if (x.Name.AdditionalData != null)
                    interfaceName = (x.Name.AdditionalData.OutType as ClassType).Declaration.Name.Name;
                string pureName = x.Name.Name.GetClassNameWithoutNamespace();

                bool areNamesEqual = false;
                if (string.IsNullOrWhiteSpace(interfaceSearchName) && !string.IsNullOrWhiteSpace(interfaceName))
                {
                    string parentSearch = decl.ContainingParent.Name.Name;
                    if (parentSearch == interfaceName && pureName == pureSearchName)
                        areNamesEqual = true;
                }
                else if (!string.IsNullOrWhiteSpace(interfaceSearchName) && string.IsNullOrWhiteSpace(interfaceName))
                {
                    string parentSearch = x.ContainingParent.Name.Name;
                    if (parentSearch == interfaceSearchName && pureName == pureSearchName)
                        areNamesEqual = true;
                }
                if (areNamesEqual && x.Type.OutType == decl.Type.OutType)
                {
                    index = i;
                    return x;
                }
            }
            index = -1;
            return null;
        }

        public static AstVarDecl GetSameDeclByTypeAndName(this List<AstVarDecl> decls, AstDeclaration decl, out int index)
        {
            return (new List<AstDeclaration>(decls)).GetSameDeclByTypeAndNamePure(decl, out index) as AstVarDecl;
        }

        public static AstPropertyDecl GetSameDeclByTypeAndName(this List<AstPropertyDecl> decls, AstDeclaration decl, out int index)
        {
            return (new List<AstDeclaration>(decls)).GetSameDeclByTypeAndNamePure(decl, out index) as AstPropertyDecl;
        }

        public static List<AstNestedExpr> GetNestedList(this List<AstExpression> exprs, IMessageHandler messageHandler)
        {
            List<AstNestedExpr> nests = new List<AstNestedExpr>();
            foreach (var expr in exprs)
            {
                nests.Add(expr.GetNested(messageHandler));
            }
            return nests;
        }

        public static AstNestedExpr GetNested(this AstExpression expr, IMessageHandler messageHandler)
        {
            if (expr is AstIdExpr idExpr)
                return new AstNestedExpr(idExpr, null, idExpr);
            else if (expr is AstNestedExpr nest)
                return nest;
            else
            {
                messageHandler?.ReportMessage(expr.SourceFile.Text, expr, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                return null;
            }
        }
        #endregion
    }
}
