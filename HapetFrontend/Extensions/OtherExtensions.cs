using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using System.Text;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Helpers;

namespace HapetFrontend.Extensions
{
    public static class OtherExtensions
    {
        public static bool Contains(this List<Token> tokens, TokenType tt)
        {
            return tokens.FirstOrDefault(x => x.Type == tt) != null;
        }

        public static bool ContainsAny(this List<Token> tokens, params TokenType[] tt)
        {
            return tokens.Any(x => tt.Contains(x.Type));
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

                // skip the cast if it is generic
                if (val is AstCastExpr cst && GenericsHelper.HasAnyGenericTypes(cst.TypeExpr))
                    continue;

                args[i].Expr = val;
                args[i].OutType = casts[i].OutType;
            }
        }

        public static List<AstArgumentExpr> GetArgsFromParams(this List<AstParamDecl> pars, HapetType containingClass = null)
        {
            List<AstArgumentExpr> args = new List<AstArgumentExpr>();
            if (containingClass != null)
            {
                var arg = new AstArgumentExpr(new AstEmptyExpr()
                {
                    OutType = containingClass
                })
                {
                    OutType = containingClass
                };
                args.Add(arg);
            }
            foreach (var p in pars)
            {
                var arg = new AstArgumentExpr(p.Type);
                arg.SetDataFromStmt(p.Type);
                args.Add(arg);
            }
            return args;
        }

        /// <summary>
        /// WARN!!! Use only for codegen
        /// </summary>
        /// <param name="pars"></param>
        /// <param name="containingClass"></param>
        /// <returns></returns>
        public static string GetParamsString(this List<AstParamDecl> pars, HapetType containingClass = null)
        {
            // WARN: ':' is used so linker would work :)))
            StringBuilder sb = new StringBuilder();
            sb.Append('(');

            // class is passed as a first parameter
            if (containingClass != null)
            {
                sb.Append(GetName(containingClass));
                if (pars.Count > 0)
                    sb.Append(':');
            }

            for (int i = 0; i < pars.Count; i++)
            {
                var p = pars[i];
                if (p.ParameterModificator != Enums.ParameterModificator.Arglist)
                {
                    if (p.ParameterModificator == Enums.ParameterModificator.Ref)
                        sb.Append("ref_");
                    else if (p.ParameterModificator == Enums.ParameterModificator.Out)
                        sb.Append("out_");
                    else if (p.ParameterModificator == Enums.ParameterModificator.Params)
                        sb.Append("params_");
                    sb.Append(p.Type.OutType == null ? "" : GetName(p.Type.OutType));
                }
                else
                    sb.Append("arglist");

                if (i != pars.Count - 1)
                    sb.Append(':');
            }
            sb.Append(')');
            return sb.ToString();

            static string GetName(HapetType t)
            {
                if (t is ClassType clsT)
                    return GenericsHelper.GetCodegenGenericName(clsT.Declaration.Name.GetCopy(clsT.Declaration.NameWithNs), null);
                else if (t is StructType strT)
                    return GenericsHelper.GetCodegenGenericName(strT.Declaration.Name.GetCopy(strT.Declaration.NameWithNs), null);
                else if (t is DelegateType delT)
                    return GenericsHelper.GetCodegenGenericName(delT.Declaration.Name.GetCopy(delT.Declaration.NameWithNs), null);
                else if (t is EnumType enmT)
                    return GenericsHelper.GetCodegenGenericName(enmT.Declaration.Name.GetCopy(enmT.Declaration.NameWithNs), null);
                else if (t is PointerType ptrT)
                    return $"{GetName(ptrT.TargetType)}*";
                return HapetType.AsString(t);
            }
        }

        public static List<AstVarDecl> GetStructFields(this List<AstDeclaration> delcs)
        {
            return delcs.Where(x =>
                (x is AstVarDecl vD &&
                !vD.SpecialKeys.Contains(Parsing.TokenType.KwStatic) &&
                !vD.SpecialKeys.Contains(Parsing.TokenType.KwConst)) &&
                x is not AstPropertyDecl).Select(x => x as AstVarDecl).ToList();
        }

        public static AstFuncDecl GetSameByNameAndTypes(this List<AstFuncDecl> delcs, AstFuncDecl searchFunc, out int index, bool skipFirst = true)
        {
            index = -1;
            AstFuncDecl bestMatch = null;
            string searchName = null;
            List<HapetType> types = null;

            types = searchFunc.Parameters.Select(x => x.Type?.OutType).ToList();
            if (skipFirst)
            {
                // remove the first param
                types = types.Skip(1).ToList();
            }

            // if not additional data
            if (searchFunc.Name.AdditionalData == null)
            {
                // there is already params type in name like
                // TestClass::AnimeFunc(int:PivoCls)
                searchName = searchFunc.Name.Name;
                for (int i = 0; i < delcs.Count; ++i)
                {
                    var x = delcs[i];
                    if (x.Name.AdditionalData != null)
                        continue;

                    // return if the same
                    if (x == searchFunc)
                    {
                        index = i;
                        return x;
                    }

                    string currName = x.Name.Name;
                    if (currName != searchName)
                        continue;

                    List<HapetType> typesD = x.Parameters.Select(x => x.Type?.OutType).ToList();
                    if (skipFirst)
                    {
                        // remove the first param
                        typesD = typesD.Skip(1).ToList();
                    }

                    // check for parameter types
                    bool areTypesTheSame = typesD.Count == types.Count;
                    if (!areTypesTheSame)
                        continue;

                    if (areTypesTheSame)
                        for (int j = 0; j < types.Count; ++j)
                        {
                            var t1 = types[j];
                            var t2 = typesD[j];
                            if (!GenericType.AreTypesTheSameIncludingGenerics(t1, t2))
                            {
                                areTypesTheSame = false;
                                break;
                            }
                        }
                    if (!areTypesTheSame)
                        continue;

                    index = i;
                    bestMatch = x;
                }
            }

            // additional search for explicit declarations!!!
            // if any of them are like 'Namespace.BaseCls::Intrf.Func(...);'
            ClassType interfaceSearchType = null;
            if (searchFunc.Name.AdditionalData != null)
                interfaceSearchType = searchFunc.Name.AdditionalData.OutType as ClassType;
            for (int i = 0; i < delcs.Count; ++i)
            {
                var x = delcs[i];
                string currName = x.Name.Name;
                ClassType interfaceType = null;
                if (x.Name.AdditionalData != null)
                    interfaceType = x.Name.AdditionalData.OutType as ClassType;

                List<HapetType> typesD = x.Parameters.Select(x => x.Type?.OutType).ToList();
                if (skipFirst)
                {
                    // remove the first param
                    typesD = typesD.Skip(1).ToList();
                }

                bool areNamesEqual = false;
                if (interfaceSearchType == null && interfaceType != null)
                {
                    var parentSearch = searchFunc.ContainingParent;
                    if (parentSearch.Type.OutType is ClassType clsTt && clsTt == interfaceType && currName == searchName)
                        areNamesEqual = true;
                    else
                    {
                        // need to check inheritance. imagine we have 
                        // struct Array : IList { void IList.Add()... }
                        // where IList itself do not have Add method but its parent - ICollection
                        // so we need to check this inheritance and allow it
                        var theExplicitDecl = x.Name.AdditionalData.OutType;
                        bool isInherited = theExplicitDecl.IsInheritedFrom(searchFunc.ContainingParent.Type.OutType as ClassType);
                        if (isInherited && currName == searchName)
                            areNamesEqual = true;
                    }
                }
                else if (interfaceSearchType != null && interfaceType == null)
                {
                    var parentSearch = x.ContainingParent;
                    if (parentSearch.Type.OutType is ClassType clsTt && clsTt == interfaceSearchType && currName == searchName)
                        areNamesEqual = true;
                }
                if (!areNamesEqual)
                    continue;

                // check for parameter types
                bool areTypesTheSame = typesD.Count == types.Count;
                if (!areTypesTheSame)
                    continue;

                if (areTypesTheSame)
                    for (int j = 0; j < types.Count; ++j)
                    {
                        var t1 = types[j];
                        var t2 = typesD[j];
                        if (!GenericType.AreTypesTheSameIncludingGenerics(t1, t2))
                        {
                            areTypesTheSame = false;
                            break;
                        }
                    }
                if (!areTypesTheSame)
                    continue;

                index = i;
                bestMatch = x;
            }
            return bestMatch;
        }

        public static AstDeclaration GetSameDeclByTypeAndNamePure(this List<AstDeclaration> decls, AstDeclaration decl, out int index)
        {
            // there are probably errors before
            if (decl.Name == null)
            {
                index = -1;
                return null;
            }

            for (int i = 0; i < decls.Count; ++i)
            {
                var x = decls[i];
                if (x.Name.Name == decl.Name.Name && 
                    x.Type.OutType == decl.Type.OutType &&
                    (x.Name.AdditionalData == null && decl.Name.AdditionalData == null))
                {
                    index = i;
                    return x;
                }

                // additional info checks
                ClassType interfaceSearchType = null;
                if (decl.Name.AdditionalData != null)
                    interfaceSearchType = decl.Name.AdditionalData.OutType as ClassType;
                string pureSearchName = decl.Name.Name.GetClassNameWithoutNamespace();
                ClassType interfaceType = null;
                if (x.Name.AdditionalData != null)
                    interfaceType = x.Name.AdditionalData.OutType as ClassType;
                string pureName = x.Name.Name.GetClassNameWithoutNamespace();

                bool areNamesEqual = false;
                if (interfaceSearchType == null && interfaceType != null)
                {
                    var parentSearch = decl.ContainingParent;
                    if (parentSearch.Type.OutType is ClassType clsTParent && clsTParent == interfaceType && pureName == pureSearchName)
                        areNamesEqual = true;
                }
                else if (interfaceSearchType != null && interfaceType == null)
                {
                    var parentSearch = x.ContainingParent;
                    if (parentSearch.Type.OutType is ClassType clsTParent && clsTParent == interfaceSearchType && pureName == pureSearchName)
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
                return new AstNestedExpr(idExpr, null, idExpr)
                {
                    OutType = idExpr.OutType,
                    Scope = idExpr.Scope,
                };
            else if (expr is AstNestedExpr nest)
                return nest;
            else
            {
                messageHandler?.ReportMessage(expr.SourceFile, expr, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                return null;
            }
        }
        #endregion
    }
}
