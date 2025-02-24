using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;
using HapetFrontend.Types;

namespace HapetFrontend.Ast.Declarations
{
    /// <summary>
    /// Ast for properties: <br />
    /// Prop { get; set; }				=> (field_Prop, get_Prop(), set_Prop(...)) <br />
    /// Prop { get; }					=> (field_Prop, get_Prop()) <br />
    /// Prop { set; }					=> could not be, error <br />
    /// Prop { get {...} set {...} }	=> (get_Prop(), set_Prop(...)) <br />
    /// Prop { get {...} }				=> (get_Prop()) <br />
    /// Prop { set {...} }				=> (set_Prop(...)) <br />
    /// Prop { get {...} set; }			=> could not be, error <br />
    /// Prop { get; set {...} }			=> could not be, error <br />
    /// </summary>
    public class AstPropertyDecl : AstVarDecl
    {
        /// <summary>
        /// True if 'get' is declared
        /// </summary>
        public bool HasGet { get; set; }
        /// <summary>
        /// True if 'set' is declared
        /// </summary>
        public bool HasSet { get; set; }

        /// <summary>
        /// Block for 'get'. Could be null
        /// </summary>
        public AstBlockExpr GetBlock { get; set; }
        /// <summary>
        /// Block for 'set'. Could be null
        /// </summary>
        public AstBlockExpr SetBlock { get; set; }

        public override string AAAName => nameof(AstPropertyDecl);

        public AstPropertyDecl(AstExpression type, AstIdExpr name, AstExpression ini = null, string doc = "", ILocation Location = null) : base(type, name, ini, doc, Location)
        {
        }

        internal PropertyDeclJson GetJsonPropa()
        {
            var attributes = Attributes.Select(x => x.GetJson()).ToList();
            return new PropertyDeclJson()
            {
                Type = HapetType.AsString(Type.OutType),
                Name = Name.Name,
                HasGet = HasGet,
                HasSet = HasSet,
                SpecialKeys = SpecialKeys,
                Attributes = attributes,
                DocString = Documentation
            };
        }

        public AstVarDecl GetField(bool forStruct)
        {
            var field = new AstVarDecl(Type, Name.GetCopy($"field_{Name.Name}"), Initializer, Documentation, Location)
            {
                ContainingParent = ContainingParent,
                Parent = Parent,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            field.Attributes.AddRange(Attributes);
            field.IsPropertyField = true;

            // no special keys for struct
            if (!forStruct)
            {
                field.SpecialKeys.Add(TokenType.KwPrivate);
                // if the propa is static - make the field also static
                if (SpecialKeys.Contains(TokenType.KwStatic))
                    field.SpecialKeys.Add(TokenType.KwStatic);
            }
            
            return field;
        }

        public AstFuncDecl GetSetFunction()
        {
            // add indexer param if it is an indexer
            var prs = new List<AstParamDecl>() { new AstParamDecl(Type, new AstIdExpr("value")) };
            if (this is AstIndexerDecl indDecl)
                prs.Insert(0, indDecl.IndexerParameter.GetCopy());

            // the func is - 'void set_Prop(PropType value)'
            AstFuncDecl func = new AstFuncDecl(
                prs,
                new AstIdExpr("void"),
                null,
                new AstIdExpr($"set_{Name.Name}"),
                "",
                Location);
            func.SpecialKeys.AddRange(SpecialKeys);
            func.ContainingParent = ContainingParent; // it has to be
            func.IsPropertyFunction = true;

            if (SetBlock == null)
            {
                // left part is null if it is a static propa
                AstNestedExpr leftPart = null;
                if (!SpecialKeys.Contains(TokenType.KwStatic))
                    leftPart = new AstNestedExpr(new AstIdExpr("this"), null);
                var setBlock = new AstBlockExpr(new List<AstStatement>()
                {
					// the stmt is - 'this.field_Prop = value'
					new AstAssignStmt(new AstNestedExpr(new AstIdExpr($"field_{Name.Name}"), leftPart), new AstIdExpr("value"), Location),
                }, Location);
                func.Body = setBlock;
            }
            else
            {
                func.Body = SetBlock;
            }
            return func;
        }

        public AstFuncDecl GetGetFunction()
        {
            // add indexer param if it is an indexer
            var prs = new List<AstParamDecl>();
            if (this is AstIndexerDecl indDecl)
                prs.Add(indDecl.IndexerParameter.GetCopy());

            // the func is - 'PropType get_Prop()'
            AstFuncDecl func = new AstFuncDecl(
                prs,
                Type,
                null,
                new AstIdExpr($"get_{Name.Name}"),
                "",
                Location);
            func.SpecialKeys.AddRange(SpecialKeys);
            func.ContainingParent = ContainingParent; // it has to be
            func.IsPropertyFunction = true;

            if (GetBlock == null)
            {
                // left part is null if it is a static propa
                AstNestedExpr leftPart = null;
                if (!SpecialKeys.Contains(TokenType.KwStatic))
                    leftPart = new AstNestedExpr(new AstIdExpr("this"), null);
                var getBlock = new AstBlockExpr(new List<AstStatement>()
                {
					// the stmt is - 'return this.field_Prop'
					new AstReturnStmt(new AstNestedExpr(new AstIdExpr($"field_{Name.Name}"), leftPart), Location),
                }, Location);
                func.Body = getBlock;
            }
            else
            {
                func.Body = GetBlock;
            }
            return func;
        }

        public override AstVarDecl GetCopyForAnotherType(AstDeclaration decl)
        {
            var varDecl = new AstPropertyDecl(Type, Name, Initializer, Documentation, Location)
            {
                Parent = decl,
                Scope = decl.SubScope,
                SourceFile = decl.SourceFile,
                ContainingParent = decl
            };
            varDecl.Attributes.AddRange(Attributes);
            varDecl.SpecialKeys.AddRange(SpecialKeys);

            varDecl.HasGet = HasGet;
            varDecl.HasSet = HasSet;
            varDecl.GetBlock = GetBlock;
            varDecl.SetBlock = SetBlock;

            return varDecl;
        }
    }

    public class PropertyDeclJson
    {
        public string Type { get; set; }
        public string Name { get; set; }

        public bool HasGet { get; set; }
        public bool HasSet { get; set; }

        public List<TokenType> SpecialKeys { get; set; }
        public List<AttributeJson> Attributes { get; set; }

        public string DocString { get; set; }

        public AstPropertyDecl GetAst(Compiler compiler)
        {
            var decl = new AstPropertyDecl(Parser.ParseType(Type, compiler), new AstIdExpr(Name), null, DocString);
            decl.HasGet = HasGet;
            decl.HasSet = HasSet;
            decl.SpecialKeys.AddRange(SpecialKeys);
            decl.Attributes.AddRange(Attributes.Select(x => x.GetAst(compiler)));
            return decl;
        }
    }
}
