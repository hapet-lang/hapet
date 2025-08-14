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

            // do not skip dtors of used classes/structs
            if (func.ContainingParent != null && GetNormalDeclIsUsed(func.ContainingParent) &&
                func.ClassFunctionType == ClassFunctionType.Dtor)
                return false;

            if (!GetNormalDeclIsUsed(func))
                return true;

            return false;
        }

        private bool IsTypeShouldBeSkipped(AstDeclaration decl)
        {
            // skip generation of imported-not-used decls
            if (!GetNormalDeclIsUsed(decl))
                return true;
            return false;
        }

        private bool GetNormalDeclIsUsed(AstDeclaration decl)
        {
            // is decl used or
            // decl is propa field/func and propa is used
            bool isUsed = decl.IsDeclarationUsed || decl.IsDeclarationUsedOnlyDeclare ||
                          _compiler.CurrentProjectSettings.TargetFormat == HapetFrontend.TargetFormat.Library; // all used for library

            var attr = decl.Attributes.FirstOrDefault(x => (x.AttributeName.OutType as ClassType).Declaration.Name.Name == "System.DeclarationUsedAttribute");
            isUsed = isUsed || (attr != null);

            return isUsed;
        }
    }
}
