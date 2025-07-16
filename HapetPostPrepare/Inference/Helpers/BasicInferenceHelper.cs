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

                decl.Type.OutType = HapetType.CurrentTypeContext.ObjectTypeInstance;
                HapetType.CurrentTypeContext.ObjectTypeInstance.Declaration = decl as AstClassDecl;
            }
            else if (typeName.Name == "System.Runtime.InteropServices.VaList")
            {
                decl.Type.OutType = HapetType.CurrentTypeContext.VaListType;
                HapetType.CurrentTypeContext.VaListType.Declaration = decl as AstStructDecl;
            }
            else if (typeName.Name == "System.Delegate")
            {
                decl.Type.OutType = HapetType.CurrentTypeContext.DelegateTypeInstance;
                HapetType.CurrentTypeContext.DelegateTypeInstance.Declaration = decl as AstClassDecl;
            }
            else if (typeName.Name == "System.String")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("string"), decl);
                decl.Type.OutType = HapetType.CurrentTypeContext.StringTypeInstance;
            }
            else if (typeName.Name == "System.Void")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("void"), decl);
                decl.Type.OutType = HapetType.CurrentTypeContext.VoidTypeInstance;
            }
            else if (typeName.Name == "System.Boolean")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("bool"), decl);
                decl.Type.OutType = HapetType.CurrentTypeContext.BoolTypeInstance;
            }
            // numeric types
            else if (typeName.Name == "System.Byte")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("byte"), decl);
                decl.Type.OutType = HapetType.CurrentTypeContext.GetIntType(1, false);
            }
            else if (typeName.Name == "System.SByte")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("sbyte"), decl);
                decl.Type.OutType = HapetType.CurrentTypeContext.GetIntType(1, true);
            }
            else if (typeName.Name == "System.UInt16")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("ushort"), decl);
                decl.Type.OutType = HapetType.CurrentTypeContext.GetIntType(2, false);
            }
            else if (typeName.Name == "System.Int16")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("short"), decl);
                decl.Type.OutType = HapetType.CurrentTypeContext.GetIntType(2, true);
            }
            else if (typeName.Name == "System.UInt32")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("uint"), decl);
                decl.Type.OutType = HapetType.CurrentTypeContext.GetIntType(4, false);
            }
            else if (typeName.Name == "System.Int32")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("int"), decl);
                decl.Type.OutType = HapetType.CurrentTypeContext.GetIntType(4, true);
            }
            else if (typeName.Name == "System.UInt64")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("ulong"), decl);
                decl.Type.OutType = HapetType.CurrentTypeContext.GetIntType(8, false);
            }
            else if (typeName.Name == "System.Int64")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("long"), decl);
                decl.Type.OutType = HapetType.CurrentTypeContext.GetIntType(8, true);
            }
            else if (typeName.Name == "System.UIntPtr")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("uintptr"), decl);
                decl.Type.OutType = HapetType.CurrentTypeContext.IntPtrTypeInstance;

                // also set size and alignment
                HapetType.CurrentTypeContext.IntPtrTypeInstance.SetSizeAndAlignment(HapetType.CurrentTypeContext.PointerSize, HapetType.CurrentTypeContext.PointerSize);
            }
            else if (typeName.Name == "System.PtrDiff")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("ptrdiff"), decl);
                decl.Type.OutType = HapetType.CurrentTypeContext.PtrDiffTypeInstance;

                // also set size and alignment
                HapetType.CurrentTypeContext.PtrDiffTypeInstance.SetSizeAndAlignment(HapetType.CurrentTypeContext.PointerSize, HapetType.CurrentTypeContext.PointerSize);
            }
            else if (typeName.Name == "System.Char")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("char"), decl);
                decl.Type.OutType = HapetType.CurrentTypeContext.CharTypeInstance;
            }
            else if (typeName.Name == "System.Double")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("double"), decl);
                decl.Type.OutType = HapetType.CurrentTypeContext.GetFloatType(8);
            }
            else if (typeName.Name == "System.Single")
            {
                _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("float"), decl);
                decl.Type.OutType = HapetType.CurrentTypeContext.GetFloatType(4);
            }
        }

        private void HandleBasicTypes(AstDeclaration decl, AstIdExpr idExpr)
        {
            // all basic types are structs
            if (decl is not AstStructDecl structDecl)
                return;

            // special handle for string type
            if (decl.Name.Name == "System.String")
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

                decl.Type.OutType = arrT;
            }
            else if (decl.Name.Name == "System.Void")
            {
                var tp = HapetType.CurrentTypeContext.VoidTypeInstance;
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name.Name == "System.Boolean")
            {
                var tp = HapetType.CurrentTypeContext.BoolTypeInstance;
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            // special handle for numeric types
            else if (decl.Name.Name == "System.Byte")
            {
                var tp = HapetType.CurrentTypeContext.GetIntType(1, false);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name.Name == "System.SByte")
            {
                var tp = HapetType.CurrentTypeContext.GetIntType(1, true);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name.Name == "System.UInt16")
            {
                var tp = HapetType.CurrentTypeContext.GetIntType(2, false);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name.Name == "System.Int16")
            {
                var tp = HapetType.CurrentTypeContext.GetIntType(2, true);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name.Name == "System.UInt32")
            {
                var tp = HapetType.CurrentTypeContext.GetIntType(4, false);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name.Name == "System.Int32")
            {
                var tp = HapetType.CurrentTypeContext.GetIntType(4, true);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name.Name == "System.UInt64")
            {
                var tp = HapetType.CurrentTypeContext.GetIntType(8, false);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name.Name == "System.Int64")
            {
                var tp = HapetType.CurrentTypeContext.GetIntType(8, true);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
                idExpr.OutType = tp;
            }
            else if (decl.Name.Name == "System.UIntPtr")
            {
                var tp = HapetType.CurrentTypeContext.IntPtrTypeInstance;
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name.Name == "System.PtrDiff")
            {
                var tp = HapetType.CurrentTypeContext.PtrDiffTypeInstance;
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name.Name == "System.Char")
            {
                var tp = HapetType.CurrentTypeContext.CharTypeInstance;
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name.Name == "System.Double")
            {
                var tp = HapetType.CurrentTypeContext.GetFloatType(8);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
            else if (decl.Name.Name == "System.Single")
            {
                var tp = HapetType.CurrentTypeContext.GetFloatType(4);
                tp.Declaration = structDecl;
                idExpr.OutType = tp;
            }
        }
    }
}
