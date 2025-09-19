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
            switch (typeName.Name)
            {
                case "System.Object":
                    _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("object"), decl);
                    decl.Type.OutType = HapetType.CurrentTypeContext.ObjectTypeInstance;
                    HapetType.CurrentTypeContext.ObjectTypeInstance.Declaration = decl as AstClassDecl;
                    break;
                case "System.Runtime.InteropServices.VaList":
                    decl.Type.OutType = HapetType.CurrentTypeContext.VaListTypeInstance;
                    HapetType.CurrentTypeContext.VaListTypeInstance.Declaration = decl as AstStructDecl;
                    break;
                case "System.Type":
                    decl.Type.OutType = HapetType.CurrentTypeContext.TypeTypeInstance;
                    HapetType.CurrentTypeContext.TypeTypeInstance.Declaration = decl as AstStructDecl;
                    break;
                case "System.Nullable":
                    decl.Type.OutType = HapetType.CurrentTypeContext.NullableTypeInstance;
                    HapetType.CurrentTypeContext.NullableTypeInstance.Declaration = decl as AstStructDecl;
                    break;
                case "System.Delegate":
                    decl.Type.OutType = HapetType.CurrentTypeContext.DelegateTypeInstance;
                    HapetType.CurrentTypeContext.DelegateTypeInstance.Declaration = decl as AstStructDecl;
                    break;
                case "System.String":
                    _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("string"), decl);
                    decl.Type.OutType = HapetType.CurrentTypeContext.StringTypeInstance;
                    break;
                case "System.Void":
                    _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("void"), decl);
                    decl.Type.OutType = HapetType.CurrentTypeContext.VoidTypeInstance;
                    break;
                case "System.Boolean":
                    _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("bool"), decl);
                    decl.Type.OutType = HapetType.CurrentTypeContext.BoolTypeInstance;
                    break;
                // numeric types
                case "System.Byte":
                    _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("byte"), decl);
                    decl.Type.OutType = HapetType.CurrentTypeContext.GetIntType(1, false);
                    break;
                case "System.SByte":
                    _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("sbyte"), decl);
                    decl.Type.OutType = HapetType.CurrentTypeContext.GetIntType(1, true);
                    break;
                case "System.UInt16":
                    _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("ushort"), decl);
                    decl.Type.OutType = HapetType.CurrentTypeContext.GetIntType(2, false);
                    break;
                case "System.Int16":
                    _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("short"), decl);
                    decl.Type.OutType = HapetType.CurrentTypeContext.GetIntType(2, true);
                    break;
                case "System.UInt32":
                    _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("uint"), decl);
                    decl.Type.OutType = HapetType.CurrentTypeContext.GetIntType(4, false);
                    break;
                case "System.Int32":
                    _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("int"), decl);
                    decl.Type.OutType = HapetType.CurrentTypeContext.GetIntType(4, true);
                    break;
                case "System.UInt64":
                    _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("ulong"), decl);
                    decl.Type.OutType = HapetType.CurrentTypeContext.GetIntType(8, false);
                    break;
                case "System.Int64":
                    _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("long"), decl);
                    decl.Type.OutType = HapetType.CurrentTypeContext.GetIntType(8, true);
                    break;
                case "System.UIntPtr":
                    _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("uintptr"), decl);
                    decl.Type.OutType = HapetType.CurrentTypeContext.IntPtrTypeInstance;
                    // also set size and alignment
                    HapetType.CurrentTypeContext.IntPtrTypeInstance.SetSizeAndAlignment(HapetType.CurrentTypeContext.PointerSize, HapetType.CurrentTypeContext.PointerSize);
                    break;
                case "System.PtrDiff":
                    _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("ptrdiff"), decl);
                    decl.Type.OutType = HapetType.CurrentTypeContext.PtrDiffTypeInstance;
                    // also set size and alignment
                    HapetType.CurrentTypeContext.PtrDiffTypeInstance.SetSizeAndAlignment(HapetType.CurrentTypeContext.PointerSize, HapetType.CurrentTypeContext.PointerSize);
                    break;
                case "System.Char":
                    _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("char"), decl);
                    decl.Type.OutType = HapetType.CurrentTypeContext.CharTypeInstance;
                    break;
                case "System.Double":
                    _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("double"), decl);
                    decl.Type.OutType = HapetType.CurrentTypeContext.GetFloatType(8);
                    break;
                case "System.Single":
                    _compiler.GlobalScope.DefineDeclSymbol(typeName.GetCopy("float"), decl);
                    decl.Type.OutType = HapetType.CurrentTypeContext.GetFloatType(4);
                    break;
            }
        }

        private void HandleBasicTypes(AstDeclaration decl, AstIdExpr idExpr)
        {
            // all basic types are structs
            if (decl is not AstStructDecl structDecl)
                return;

            switch (decl.Name.Name)
            {
                // special handle for string type
                case "System.String":
                    // set the decl to the string type
                    HapetType.CurrentTypeContext.StringTypeInstance.Declaration = structDecl;
                    idExpr.OutType = HapetType.CurrentTypeContext.StringTypeInstance;
                    break;
                // special handle for array type
                case "System.Array" when decl.Name is AstIdGenericExpr genId:
                    var targetType = genId.GenericRealTypes[0].OutType;
                    if (targetType == null)
                        return;
                    var arrT = HapetType.CurrentTypeContext.GetArrayType(targetType);
                    // set decl if newly created
                    arrT.Declaration ??= structDecl;
                    idExpr.OutType = arrT;
                    decl.Type.OutType = arrT;
                    break;
                // special handle for nullable type
                case "System.Nullable" when decl.Name is AstIdGenericExpr genId2:
                    var targetType2 = genId2.GenericRealTypes[0].OutType;
                    if (targetType2 == null)
                        return;
                    var nullT = HapetType.CurrentTypeContext.GetNullableType(targetType2);
                    // set decl if newly created
                    nullT.Declaration ??= structDecl;
                    idExpr.OutType = nullT;
                    decl.Type.OutType = nullT;
                    break;
                case "System.Void":
                    var tp = HapetType.CurrentTypeContext.VoidTypeInstance;
                    tp.Declaration = structDecl;
                    idExpr.OutType = tp;
                    break;
                case "System.Boolean":
                    var tp2 = HapetType.CurrentTypeContext.BoolTypeInstance;
                    tp2.Declaration = structDecl;
                    idExpr.OutType = tp2;
                    break;
                // special handle for numeric types
                case "System.Byte":
                    var tp3 = HapetType.CurrentTypeContext.GetIntType(1, false);
                    tp3.Declaration = structDecl;
                    idExpr.OutType = tp3;
                    break;
                case "System.SByte":
                    var tp4 = HapetType.CurrentTypeContext.GetIntType(1, true);
                    tp4.Declaration = structDecl;
                    idExpr.OutType = tp4;
                    break;
                case "System.UInt16":
                    var tp5 = HapetType.CurrentTypeContext.GetIntType(2, false);
                    tp5.Declaration = structDecl;
                    idExpr.OutType = tp5;
                    break;
                case "System.Int16":
                    var tp6 = HapetType.CurrentTypeContext.GetIntType(2, true);
                    tp6.Declaration = structDecl;
                    idExpr.OutType = tp6;
                    break;
                case "System.UInt32":
                    var tp7 = HapetType.CurrentTypeContext.GetIntType(4, false);
                    tp7.Declaration = structDecl;
                    idExpr.OutType = tp7;
                    break;
                case "System.Int32":
                    var tp8 = HapetType.CurrentTypeContext.GetIntType(4, true);
                    tp8.Declaration = structDecl;
                    idExpr.OutType = tp8;
                    break;
                case "System.UInt64":
                    var tp9 = HapetType.CurrentTypeContext.GetIntType(8, false);
                    tp9.Declaration = structDecl;
                    idExpr.OutType = tp9;
                    break;
                case "System.Int64":
                    var tp10 = HapetType.CurrentTypeContext.GetIntType(8, true);
                    tp10.Declaration = structDecl;
                    idExpr.OutType = tp10;
                    break;
                case "System.UIntPtr":
                    var tp11 = HapetType.CurrentTypeContext.IntPtrTypeInstance;
                    tp11.Declaration = structDecl;
                    idExpr.OutType = tp11;
                    break;
                case "System.PtrDiff":
                    var tp12 = HapetType.CurrentTypeContext.PtrDiffTypeInstance;
                    tp12.Declaration = structDecl;
                    idExpr.OutType = tp12;
                    break;
                case "System.Char":
                    var tp13 = HapetType.CurrentTypeContext.CharTypeInstance;
                    tp13.Declaration = structDecl;
                    idExpr.OutType = tp13;
                    break;
                case "System.Double":
                    var tp14 = HapetType.CurrentTypeContext.GetFloatType(8);
                    tp14.Declaration = structDecl;
                    idExpr.OutType = tp14;
                    break;
                case "System.Single":
                    var tp15 = HapetType.CurrentTypeContext.GetFloatType(4);
                    tp15.Declaration = structDecl;
                    idExpr.OutType = tp15;
                    break;
            }
        }
    }
}
