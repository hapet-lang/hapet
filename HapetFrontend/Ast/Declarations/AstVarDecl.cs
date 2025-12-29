using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Enums;
using HapetFrontend.Helpers;

namespace HapetFrontend.Ast.Declarations
{
    public class AstVarDecl : AstDeclaration
    {
        /// <summary>
        /// A value to init the var
        /// </summary>
        public AstExpression Initializer { get; set; }

        /// <summary>
        /// 'true' if the field is event field
        /// </summary>
        public bool IsEvent { get; set; }

        /// <summary>
        /// Used for easier infferencing. Mean that the field is for get/set func
        /// </summary>
        public bool IsPropertyField { get; set; }

        /// <summary>
        /// 'true' if used to handle static ctor condition
        /// </summary>
        public bool IsStaticCtorField { get; set; }

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
               Type?.GetDeepCopy() as AstNestedExpr,
               Name?.GetDeepCopy() as AstIdExpr,
               null,
               Documentation, Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                IsPropertyField = IsPropertyField,
                IsImported = IsImported,
                IsStaticCtorField = IsStaticCtorField,
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
                GenericConstrainLocations = GenericConstrainLocations,
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
                IsStaticCtorField = IsStaticCtorField,
                SourceFile = decl.SourceFile,
                ContainingParent = decl,
                GenericConstrainLocations = GenericConstrainLocations,
            };
            varDecl.Attributes.AddRange(Attributes);
            varDecl.SpecialKeys.AddRange(SpecialKeys);
            varDecl.IsPropertyField = IsPropertyField;
            return varDecl;
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            if (Type == oldChild)
                Type = newChild as AstExpression;
            else if (Name == oldChild)
                Name = newChild as AstIdExpr;
            else if (Initializer == oldChild)
                Initializer = newChild as AstExpression;
        }
    }
}
