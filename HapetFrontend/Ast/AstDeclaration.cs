using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Enums;
using HapetFrontend.Extensions;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using Newtonsoft.Json;

namespace HapetFrontend.Ast
{
    public abstract class AstDeclaration : AstStatement
    {
        /// <summary>
        /// Could be anything - nested, id, genId, tuple
        /// </summary>
        public AstExpression Type { get; set; }
        public AstIdExpr Name { get; set; }

        public string Documentation { get; set; }

        /// <summary>
        /// Keys like public/static/virtual and other
        /// </summary>
        public List<Token> SpecialKeys { get; private set; } = new List<Token>();
        /// <summary>
        /// Attributes that are applied to the decl
        /// </summary>
        public List<AstAttributeStmt> Attributes { get; } = new List<AstAttributeStmt>();

        /// <summary>
        /// The inner scope of the decl. Used to get access to it's content
		/// Not for every decl!!!
        /// </summary>
        public Scope SubScope { get; set; }

        /// <summary>
        /// The class/struct/interface/other_shite that contains the decl
        /// </summary>
        [JsonIgnore]
        public AstDeclaration ContainingParent { get; set; }

        /// <summary>
        /// 'true' if the declaration is a generic decl like 'List-T-'
        /// </summary>
        public bool HasGenericTypes { get; set; }

        /// <summary>
        /// 'true' if smth like List-T- or List-int-, 'false' on real pure generic type
        /// </summary>
        public bool IsImplOfGeneric { get; set; }

        /// <summary>
        /// Generic parameter constrains like:
        /// ...-T- where T: struct, enum, class { ... }
        /// </summary>
        public Dictionary<AstIdExpr, List<AstConstrainStmt>> GenericConstrains { get; set; } = new Dictionary<AstIdExpr, List<AstConstrainStmt>>();

        /// <summary>
        /// Contains the original generic decl from which the current one is created 
        /// also if the current one is <see cref="IsImplOfGeneric"/>
        /// </summary>
        public AstDeclaration OriginalGenericDecl { get; set; }

        /// <summary>
        /// 'true' if the decl is nested decl
        /// </summary>
        public bool IsNestedDecl { get; set; }
        /// <summary>
        /// Contains parent decl if the <see cref="IsNestedDecl"/> is 'true'
        /// </summary>
        public AstDeclaration ParentDecl { get; set; }

        /// <summary>
        /// Getting symbol of itself
        /// </summary>
        [JsonIgnore]
        public virtual ISymbol GetSymbol
        {
            get
            {
                return Scope.GetSymbol(Name);
            }
        }

        /// <summary>
        /// Is the declaration imported from another assembly
        /// </summary>
        public bool IsImported { get; set; }

        public override string AAAName => nameof(AstDeclaration);

        public AstDeclaration(AstIdExpr name, string doc, ILocation location = null) : base(location)
        {
            this.Name = name;
            this.Documentation = doc;
        }

        public virtual AstDeclaration GetOnlyDeclareCopy()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the real struct size for allocation
        /// Could be used only after <see cref="GenerateTypeInfoConst"/> from backend
        /// </summary>
        /// <returns>The size</returns>
        public static int GetSizeForAlloc(List<AstVarDecl> fields, bool withTypeInfo = true)
        {
            if (withTypeInfo)
            {
                var tp = new AstIdExpr("uintptr") { OutType = PointerType.VoidLiteralType };
                fields.Insert(0, new AstVarDecl(new AstNestedExpr(tp, null) { OutType = tp.OutType }, new AstIdExpr("typeinfo")));
            }
            int totalSize = 0;
            // go all over the fields and calc the size
            for (int i = 0; i < fields.Count; ++i)
            {
                var field = fields[i];
                var fieldType = field.Type.OutType;

                int fieldAlignment = fieldType.GetAlignment() == 0 ? fieldType.GetSize() : fieldType.GetAlignment();
                int padding = (fieldAlignment - (totalSize % fieldAlignment)) % fieldAlignment;  // Alignment
                totalSize += padding;  // Add padding for the alignment
                totalSize += fieldType.GetSize();  // Add field size
            }
            return totalSize;
        }

        /// <summary>
        /// Helper function that returns inherited shite for class and struct decls
        /// </summary>
        /// <returns>Inhertied types</returns>
        public List<AstNestedExpr> GetInheritedTypes()
        {
            if (this is AstClassDecl clsDecl)
                return clsDecl.InheritedFrom;
            else if (this is AstStructDecl strDecl)
                return strDecl.InheritedFrom;
            return new List<AstNestedExpr>();
        }

        /// <summary>
        /// Returns all the fields (including inherited) 
        /// to properly calculate size to alloc and other cringe
        /// </summary>
        /// <returns></returns>
        public List<AstVarDecl> GetAllRawFields()
        {
            List<AstNestedExpr> inheritedFrom;
            List<AstDeclaration> declarations;
            if (this is AstStructDecl strDecl)
            {
                // return cached
                if (strDecl.AllRawFields != null)
                    return strDecl.AllRawFields.ToList();
                inheritedFrom = strDecl.InheritedFrom;
                declarations = strDecl.Declarations;
            }
            else if (this is AstClassDecl clsDecl)
            {
                // return cached
                if (clsDecl.AllRawFields != null)
                    return clsDecl.AllRawFields.ToList();
                inheritedFrom = clsDecl.InheritedFrom;
                declarations = clsDecl.Declarations;
            }
            else
            {
                return new List<AstVarDecl>();
            }

            // to handle all the fields
            List<AstVarDecl> allFields = new List<AstVarDecl>();

            List<AstVarDecl> parentFields = new List<AstVarDecl>();
            // we can take only inherited class - because it has all the 
            // fields that we want. if it is not - it probably errored 
            // previously in PP (step 8-9 probably)
            if (inheritedFrom != null &&
                inheritedFrom.Count > 0 &&
                inheritedFrom[0].OutType is ClassType inhClsType &&
                !inhClsType.Declaration.IsInterface)
            {
                parentFields = inhClsType.Declaration.GetAllRawFields();
            }

            List<AstVarDecl> currentFields = declarations.GetStructFields();
            foreach (var parentField in parentFields)
            {
                var currentField = currentFields.GetSameDeclByTypeAndName(parentField, out int _);
                if (currentField != null)
                {
                    // do not remove fields with 'new' kw - they also has to be stored :(
                    if (!currentField.SpecialKeys.Contains(TokenType.KwNew))
                    {
                        // we need to remove - because we would add remainings at the end
                        currentFields.Remove(currentField);
                    }
                    allFields.Add(currentField);
                }
                else
                {
                    allFields.Add(parentField);
                }
            }
            // add remainings
            allFields.AddRange(currentFields);

            if (this is AstStructDecl strDecl2)
            {
                // cache
                strDecl2.AllRawFields = allFields;
            }
            else if (this is AstClassDecl clsDecl2)
            {
                // cache
                clsDecl2.AllRawFields = allFields;
            }

            return allFields.ToList();
        }
    }
}
