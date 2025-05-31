using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast;
using HapetFrontend.Types;
using HapetFrontend.Scoping;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public void PostPrepareAliases(AstIdExpr typeName, Scope scope, AstDeclaration decl)
        {
            // kostyl to create aliases :)
            if (typeName.Name == "System.Object")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("object"), decl);
            }
            else if (typeName.Name == "System.String")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("string"), decl);
            }
            // numeric types
            else if (typeName.Name == "System.Byte")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("byte"), decl);
            }
            else if (typeName.Name == "System.SByte")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("sbyte"), decl);
            }
            else if (typeName.Name == "System.UInt16")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("ushort"), decl);
            }
            else if (typeName.Name == "System.Int16")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("short"), decl);
            }
            else if (typeName.Name == "System.UInt32")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("uint"), decl);
            }
            else if (typeName.Name == "System.Int32")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("int"), decl);
            }
            else if (typeName.Name == "System.UInt64")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("ulong"), decl);
            }
            else if (typeName.Name == "System.Int64")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("long"), decl);
            }
            else if (typeName.Name == "System.UIntPtr")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("uintptr"), decl);
            }
            else if (typeName.Name == "System.PtrDiff")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("ptrdiff"), decl);
            }
        }

        private void HandleBasicTypes(AstDeclaration decl, AstIdExpr idExpr)
        {
            // all basic types are structs
            if (decl is not AstStructDecl structDecl)
                return;

            // special handle for string type
            if (decl.Name is AstIdExpr id && id.Name == "System.String")
            {
                // set the decl to the string type
                HapetType.CurrentTypeContext.StringTypeInstance.Declaration = structDecl;
                idExpr.OutType = HapetType.CurrentTypeContext.StringTypeInstance;
            }
            // special handle for array type
            else if (decl.Name is AstIdGenericExpr genId && genId.Name == "System.Array")
            {
                var targetType = genId.GenericRealTypes[0].OutType;
                if (targetType == null)
                    return;

                var arrT = HapetType.CurrentTypeContext.GetArrayType(targetType);
                // set decl if newly created
                if (arrT.Declaration == null)
                    arrT.Declaration = structDecl;
                idExpr.OutType = arrT;
            }
            // special handle for numeric types
            else if (decl.Name is AstIdExpr id2 && id2.Name == "System.Byte")
            {
                var tp = HapetType.CurrentTypeContext.GetIntType(1, false);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name is AstIdExpr id3 && id3.Name == "System.SByte")
            {
                var tp = HapetType.CurrentTypeContext.GetIntType(1, true);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name is AstIdExpr id4 && id4.Name == "System.UInt16")
            {
                var tp = HapetType.CurrentTypeContext.GetIntType(2, false);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name is AstIdExpr id5 && id5.Name == "System.Int16")
            {
                var tp = HapetType.CurrentTypeContext.GetIntType(2, true);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name is AstIdExpr id6 && id6.Name == "System.UInt32")
            {
                var tp = HapetType.CurrentTypeContext.GetIntType(4, false);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name is AstIdExpr id7 && id7.Name == "System.Int32")
            {
                var tp = HapetType.CurrentTypeContext.GetIntType(4, true);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name is AstIdExpr id8 && id8.Name == "System.UInt64")
            {
                var tp = HapetType.CurrentTypeContext.GetIntType(8, false);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name is AstIdExpr id9 && id9.Name == "System.Int64")
            {
                var tp = HapetType.CurrentTypeContext.GetIntType(8, true);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name is AstIdExpr id10 && id10.Name == "System.Int64")
            {
                var tp = HapetType.CurrentTypeContext.GetIntType(8, true);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name is AstIdExpr id11 && id11.Name == "System.UIntPtr")
            {
                var tp = HapetType.CurrentTypeContext.IntPtrTypeInstance;
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name is AstIdExpr id12 && id12.Name == "System.PtrDiff")
            {
                var tp = HapetType.CurrentTypeContext.PtrDiffTypeInstance;
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
        }
    }
}
