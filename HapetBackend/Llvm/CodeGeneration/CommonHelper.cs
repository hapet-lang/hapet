using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast;
using HapetFrontend.Enums;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using HapetFrontend.Extensions;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;

namespace HapetBackend.Llvm
{
    public partial class LlvmCodeGenerator
    {
        private bool IsFunctionShouldBeSkipped(AstFuncDecl func)
        {
            // do not skip these stors
            if (func.ClassFunctionType == ClassFunctionType.StaticCtor &&
                (func.ContainingParent.Name.Name == "System.StackTrace" || func.ContainingParent.Name.Name == "System.Runtime.InteropServices.ExceptionHelper"))
                return false;

            // also we need to skip here stors of generic impls
            if (func.ContainingParent != null && func.ContainingParent.IsImplOfGeneric &&
                func.ClassFunctionType == ClassFunctionType.StaticCtor)
                return true;

            // skip generation of imported-not-used functions
            if (func.IsImported && !GetNormalDeclIsUsed(func) &&
                !func.SpecialKeys.Contains(TokenType.KwVirtual) &&
                !func.SpecialKeys.Contains(TokenType.KwAbstract) &&
                !func.SpecialKeys.Contains(TokenType.KwOverride))
                return true;

            // if the containing type is used - then vtable and typeinfo would be generated - so need to all virtual funcs
            // except for imported shite - their vtable and typeinfo are imported
            if (func.ContainingParent != null && GetNormalDeclIsUsed(func.ContainingParent) && (
                func.SpecialKeys.Contains(TokenType.KwVirtual) ||
                func.SpecialKeys.Contains(TokenType.KwAbstract) ||
                func.SpecialKeys.Contains(TokenType.KwOverride)) && !func.IsImported)
                return false;

            if (!GetNormalDeclIsUsed(func))
                return true;

            return false;
        }

        private bool IsTypeShouldBeSkipped(AstDeclaration decl)
        {
            // special case for required types
            if (decl.Type.OutType == HapetType.CurrentTypeContext.GetArrayType(HapetType.CurrentTypeContext.ObjectTypeInstance))
                return false;

            // skip generation of imported-not-used decls
            if (!GetNormalDeclIsUsed(decl))
                return true;
            return false;
        }

        private bool GetNormalDeclIsUsed(AstDeclaration decl)
        {
            // is decl used or
            // decl is propa field/func and propa is used
            bool isUsed = decl.IsDeclarationUsed ||
                          _compiler.CurrentProjectSettings.TargetFormat == HapetFrontend.TargetFormat.Library || // all used for library
                          (decl is AstVarDecl vd1 && vd1.IsPropertyField && vd1.NormalParent is AstDeclaration pd1 && pd1.IsDeclarationUsed) ||
                          (decl is AstFuncDecl fd1 && fd1.IsPropertyFunction && fd1.NormalParent is AstDeclaration pd2 && pd2.IsDeclarationUsed);

            var attr = decl.Attributes.FirstOrDefault(x => (x.AttributeName.OutType as ClassType).Declaration.Name.Name == "System.DeclarationUsedAttribute");
            isUsed = isUsed || (attr != null);

            return isUsed;
        }
    }
}
