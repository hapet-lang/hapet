using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Enums;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using Newtonsoft.Json;

namespace HapetFrontend.Ast.Declarations
{
    public class AstVarDecl : AstDeclaration
    {
        /// <summary>
        /// A value to init the var
        /// </summary>
        public AstExpression Initializer { get; set; }

        /// <summary>
        /// Used for easier infferencing. Mean that the field is for get/set func
        /// </summary>
        public bool IsPropertyField { get; set; }

        /// <summary>
        /// Handles get/set methods to access the static/const field (used only for static/const !!!)
        /// </summary>
        public (AstFuncDecl, AstFuncDecl)? GetSetMethodsForStatic { get; set; }

        public override string AAAName => nameof(AstVarDecl);

        public AstVarDecl(AstExpression type, AstIdExpr name, AstExpression ini = null, string doc = "", ILocation location = null) : base(name, doc, location)
        {
            Type = type;
            Initializer = ini;
        }

        public override string ToString()
        {
            return $"var:{GenericsHelper.GetNameFromAst(Name, null)}";
        }

        public override AstDeclaration GetOnlyDeclareCopy()
        {
            var copy = new AstVarDecl(
               Type.GetDeepCopy() as AstNestedExpr,
               Name.GetDeepCopy() as AstIdExpr,
               null,
               Documentation, Location)
            {
                IsPropertyField = IsPropertyField,
                IsImported = IsImported,
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
            };
            copy.Attributes.AddRange(Attributes);
            copy.SpecialKeys.AddRange(SpecialKeys);
            return copy;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = GetOnlyDeclareCopy() as AstVarDecl;
            copy.Initializer = Initializer?.GetDeepCopy() as AstExpression;
            return copy;
        }

        public virtual AstVarDecl GetCopyForAnotherType(AstDeclaration decl)
        {
            var varDecl = new AstVarDecl(Type, Name, Initializer, Documentation, Location)
            {
                Parent = decl,
                Scope = decl.SubScope,
                SourceFile = decl.SourceFile,
                ContainingParent = decl
            };
            varDecl.Attributes.AddRange(Attributes);
            varDecl.SpecialKeys.AddRange(SpecialKeys);
            varDecl.IsPropertyField = IsPropertyField;
            return varDecl;
        }
    }
}
