using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast;
using HapetFrontend.Scoping;
using System.Text;
using HapetFrontend.Extensions;
using System.Xml.Linq;
using HapetFrontend.Types;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Parsing;

namespace HapetFrontend.Helpers
{
    public static class GenericsHelper
    {
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
                    return f.Name.Name;
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
        public static string GetNameFromAst(AstIdExpr idExpr, IMessageHandler messageHandler)
        {
            if (idExpr is not AstIdGenericExpr genId)
                return idExpr.Name;

            StringBuilder sb = new StringBuilder("<");
            for (int i = 0; i < genId.GenericRealTypes.Count; ++i)
            {
                var g = genId.GenericRealTypes[i];
                sb.Append(g.GetNested(messageHandler).TryFlatten(null, null));
                if (i < genId.GenericRealTypes.Count - 1)
                    sb.Append(", ");
            }
            sb.Append('>');
            return $"{genId.Name}{sb}";
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
                return type.ToString();
            }
            return type.ToString();
        }

        /// <summary>
        /// Returns list of generics from name
        /// </summary>
        /// <param name="idExpr"></param>
        /// <returns></returns>
        public static List<AstIdExpr> GetGenericsFromName(AstIdGenericExpr idExpr, IMessageHandler messageHandler)
        {
            var generics = new List<AstIdExpr>();
            foreach (var g in idExpr.GenericRealTypes)
            {
                if (g is AstNestedExpr nest)
                    generics.Add(nest.UnrollToRightPart<AstIdExpr>());
                else if (g is AstIdExpr id)
                    generics.Add(id);
                else
                {
                    messageHandler.ReportMessage(idExpr.SourceFile.Text, g.Location, [], ErrorCode.Get(CTEN.CommonIdentifierExpected));
                    generics.Add(null); // ERROR HERE
                }
            }
            return generics;
        }

        /// <summary>
        /// Extracts all generic types like T from ValueType<T>
        /// </summary>
        /// <param name="names"></param>
        /// <returns></returns>
        public static List<AstIdExpr> ExtractAllGenericTypes(List<AstExpression> types)
        {
            var generics = new List<AstIdExpr>();
            foreach (var g in types)
            {
                if (g is AstIdGenericExpr genId)
                {
                    generics.AddRange(ExtractAllGenericTypes(genId.GenericRealTypes));
                }
                else if (g is AstIdExpr id && id.OutType is GenericType)
                {
                    generics.Add(id);
                }
                else if (g is AstNestedExpr nst && nst.RightPart is AstIdGenericExpr genId2)
                {
                    generics.AddRange(ExtractAllGenericTypes(genId2.GenericRealTypes));
                }
                else if (g is AstNestedExpr nst2 && nst2.RightPart is AstIdExpr id2 && id2.OutType is GenericType)
                {
                    generics.Add(id2);
                }
            }
            return generics;
        }

        public static bool ShouldTheDeclBeSkippedFromCodeGen(AstDeclaration decl)
        {
            // DO NOT SKIP ANY STORS!
            if (decl is AstFuncDecl fnc && fnc.ClassFunctionType == Enums.ClassFunctionType.StaticCtor)
                return false;

            // skip generic (non-real) parents
            if (decl.ContainingParent?.HasGenericTypes ?? false)
                return true;
            // skip generic (non-real) funcs
            if (decl.HasGenericTypes)
                return true;
            // also skip if parent has generic types
            if (decl.IsNestedDecl && decl.ParentDecl.HasGenericTypes)
                return true;
            // skip genericDecl parents
            if (decl.ContainingParent is AstGenericDecl)
                return true;
            // happens at least when 'decl' is a func in a normal struct and the struct
            // is nested into a generic class
            if (decl.ContainingParent != null && decl.ContainingParent.IsNestedDecl &&
                decl.ContainingParent.ParentDecl.HasGenericTypes)
                return true;
            return false;
        }

        /// <summary>
        /// Creates SomeType[int:string] string for codegen
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetCodegenGenericName(AstIdExpr name, IMessageHandler messageHandler)
        {
            if (name is not AstIdGenericExpr genId)
                return name.Name;

            StringBuilder sb = new StringBuilder("[");
            for (int i = 0; i < genId.GenericRealTypes.Count; ++i)
            {
                var g = genId.GenericRealTypes[i];
                sb.Append(g.GetNested(messageHandler).TryFlatten(null, null, true));
                if (i < genId.GenericRealTypes.Count - 1)
                    sb.Append(':');
            }
            sb.Append(']');
            return $"{genId.Name}{sb}";
        }
    }
}
