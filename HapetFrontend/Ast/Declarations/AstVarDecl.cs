using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
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

        public override string AAAName => nameof(AstVarDecl);

        public AstVarDecl(AstNestedExpr type, AstIdExpr name, AstExpression ini = null, string doc = "", ILocation Location = null) : base(name, doc, Location)
        {
            Type = type;
            Initializer = ini;
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstVarDecl(
                Type.GetDeepCopy() as AstNestedExpr,
                Name.GetDeepCopy() as AstIdExpr,
                Initializer?.GetDeepCopy() as AstExpression,
                Documentation, Location)
            {
                IsPropertyField = IsPropertyField,
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
            };
            copy.Attributes.AddRange(Attributes);
            copy.SpecialKeys.AddRange(SpecialKeys);
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

        internal VarDeclJson GetJson()
        {
            var attributes = Attributes.Select(x => x.GetJson()).ToList();
            return new VarDeclJson()
            {
                Type = HapetType.AsString(Type.OutType, true),
                Name = Name.Name,
                SpecialKeys = SpecialKeys,
                Attributes = attributes,
                DocString = Documentation
            };
        }

        public override string ToString()
        {
            return $"{HapetType.AsString(Type.OutType)} {Name.Name}";
        }
    }

    public class VarDeclJson
    {
        public string Type { get; set; }
        public string Name { get; set; }

        public List<TokenType> SpecialKeys { get; set; }
        public List<AttributeJson> Attributes { get; set; }

        public string DocString { get; set; }

        public AstVarDecl GetAst(Compiler compiler)
        {
            var decl = new AstVarDecl(Parser.ParseType(Type, compiler) as AstNestedExpr, new AstIdExpr(Name), null, DocString);
            decl.SpecialKeys.AddRange(SpecialKeys);
            decl.Attributes.AddRange(Attributes.Select(x => x.GetAst(compiler)));
            return decl;
        }
    }
}
