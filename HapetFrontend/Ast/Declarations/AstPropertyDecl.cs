using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;

namespace HapetFrontend.Ast.Declarations
{
	/// <summary>
	/// Ast for properties:
	/// Prop { get; set; }				=> (field_Prop, get_Prop(), set_Prop(...))
	/// Prop { get; }					=> (field_Prop, get_Prop())
	/// Prop { set; }					=> could not be, error
	/// Prop { get {...} set {...} }	=> (get_Prop(), set_Prop(...))
	/// Prop { get {...} }				=> (get_Prop())
	/// Prop { set {...} }				=> (set_Prop(...))
	/// Prop { get {...} set; }			=> could not be, error
	/// Prop { get; set {...} }			=> could not be, error
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

		public AstPropertyDecl(AstExpression type, AstIdExpr name, AstExpression ini = null, string doc = "", ILocation Location = null) : base(type, name, ini, doc, Location)
		{
		}

		internal PropertyDeclJson GetJsonPropa()
		{
			var attributes = Attributes.Select(x => x.GetJson()).ToList();
			return new PropertyDeclJson()
			{
				Type = Type.OutType.ToString(),
				Name = Name.Name,
				HasGet = HasGet,
				HasSet = HasSet,
				SpecialKeys = SpecialKeys,
				Attributes = attributes,
				DocString = Documentation
			};
		}

		public AstVarDecl GetField()
		{
			var field = new AstVarDecl(Type, Name.GetCopy($"field_{Name.Name}"), Initializer, Documentation, Location)
			{
				ContainingParent = ContainingParent,
				Parent = Parent,
				Scope = Scope,
				SourceFile = SourceFile,
			};
			field.Attributes.AddRange(Attributes);
			field.SpecialKeys.Add(TokenType.KwPrivate);
			return field;
		}

		public AstFuncDecl GetSetFunction()
		{
			// the func is - 'void set_Prop(PropType value)'
			AstFuncDecl func = new AstFuncDecl(
				new List<AstParamDecl>() 
				{ 
					new AstParamDecl(Type, new AstIdExpr("value")) 
				}, 
				new AstIdExpr("void"), 
				null, 
				new AstIdExpr($"set_{Name.Name}"),
				"",
				Location);
			func.SpecialKeys.AddRange(SpecialKeys);
			func.ContainingClass = ContainingParent as AstClassDecl; // it has to be

			if (SetBlock == null)
			{
				var setBlock = new AstBlockExpr(new List<AstStatement>()
				{
					// the stmt is - 'this.field_Prop = value'
					new AstAssignStmt(new AstNestedExpr(new AstIdExpr($"field_{Name.Name}"), new AstNestedExpr(new AstIdExpr("this"), null)), new AstIdExpr("value"), Location),
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
			// the func is - 'PropType get_Prop()'
			AstFuncDecl func = new AstFuncDecl(
				new List<AstParamDecl>(),
				Type,
				null,
				new AstIdExpr($"get_{Name.Name}"),
				"",
				Location);
			func.SpecialKeys.AddRange(SpecialKeys);
			func.ContainingClass = ContainingParent as AstClassDecl; // it has to be

			if (GetBlock == null)
			{
				var getBlock = new AstBlockExpr(new List<AstStatement>()
				{
					// the stmt is - 'return this.field_Prop'
					new AstReturnStmt(new AstNestedExpr(new AstIdExpr($"field_{Name.Name}"), new AstNestedExpr(new AstIdExpr("this"), null)), Location),
				}, Location);
				func.Body = getBlock;
			}
			else
			{
				func.Body = GetBlock;
			}
			return func;
		}
	}

	internal class PropertyDeclJson
	{
		public string Type { get; set; }
		public string Name { get; set; }

		public bool HasGet { get; set; }
		public bool HasSet { get; set; }

		public List<TokenType> SpecialKeys { get; set; }
		public List<AttributeJson> Attributes { get; set; }

		public string DocString { get; set; }
	}
}
