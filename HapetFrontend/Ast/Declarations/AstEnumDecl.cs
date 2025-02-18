using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using System.Numerics;

namespace HapetFrontend.Ast.Declarations
{
    public class AstEnumDecl : AstDeclaration
    {
        /// <summary>
        /// Declarations that are in the enum (their type should be inherited from the enum type)
        /// </summary>
        public List<AstVarDecl> Declarations { get; } = new List<AstVarDecl>();

        /// <summary>
        /// The numeric type from which the enum inherits. Default is 'int'
        /// </summary>
        public AstNestedExpr InheritedType { get; set; }

        public AstEnumDecl(AstIdExpr name, List<AstVarDecl> declarations, string doc = "", ILocation Location = null) : base(name, doc, Location)
        {
            Type = new AstIdExpr("enum", Location);
            Type.OutType = new EnumType(this);

            Declarations = declarations;
        }

        public EnumDeclJson GetJson()
        {
            var fields = Declarations.Select(x => x.Name.Name).ToList();
            var values = Declarations.Select(x => ((NumberData)(x.Initializer.OutValue)).IntValue).ToList();
            var attributes = Attributes.Select(x => x.GetJson()).ToList();
            return new EnumDeclJson()
            {
                Fields = fields,
                Values = values,
                Name = Name.Name,
                SpecialKeys = SpecialKeys,
                Attributes = attributes,
                InheritedType = HapetType.AsString(InheritedType.OutType),
                DocString = Documentation
            };
        }
    }

    public class EnumDeclJson
    {
        public List<string> Fields { get; set; }
        public List<BigInteger> Values { get; set; }
        public string Name { get; set; }

        public List<TokenType> SpecialKeys { get; set; }
        public List<AttributeJson> Attributes { get; set; }
        public string InheritedType { get; set; }

        public string DocString { get; set; }

        public AstEnumDecl GetAst(Compiler compiler)
        {
            var fields = new List<AstVarDecl>();
            for (int i = 0; i < Fields.Count; i++)
            {
                var v = new AstVarDecl(Parser.ParseType(InheritedType, compiler), new AstIdExpr(Fields[i]), new AstNumberExpr(NumberData.FromInt(Values[i])));
                fields.Add(v);
            }
            var decl = new AstEnumDecl(new AstIdExpr(Name), fields, DocString);
            decl.SpecialKeys.AddRange(SpecialKeys);
            decl.Attributes.AddRange(Attributes.Select(x => x.GetAst(compiler)));
            decl.InheritedType = Parser.ParseType(InheritedType, compiler) as AstNestedExpr;
            return decl;
        }
    }
}
