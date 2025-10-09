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

        public string NameWithNs => $"{SourceFile.Namespace}.{Name.Name}";

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
        /// Used in LSP
        /// </summary>
        public List<(ILocation, ILocation)> GenericConstrainLocations { get; set; }

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
        /// 'true' if the decl is used in the assembly (codegen only for imported)
        /// </summary>
        public bool IsDeclarationUsed { get; set; }
        /// <summary>
        /// 'true' if the decl is used in the assembly BUT without body (only declaration) (codegen only for imported)
        /// </summary>
        public bool IsDeclarationUsedOnlyDeclare { get; set; }

        /// <summary>
        /// Getting symbol of itself
        /// </summary>
        [JsonIgnore]
        public ISymbol Symbol { get; set; }

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
        /// Helper function that returns inherited shite for class and struct decls
        /// </summary>
        /// <returns>Inhertied types</returns>
        public List<AstNestedExpr> GetInheritedTypes()
        {
            if (this is AstClassDecl clsDecl)
                return clsDecl.InheritedFrom;
            else if (this is AstStructDecl strDecl)
                return strDecl.InheritedFrom;
            else if (this is AstGenericDecl genDecl)
                return genDecl.InheritedFrom;
            return new List<AstNestedExpr>();
        }

        /// <summary>
        /// Helper function that returns declarations shite for class and struct decls
        /// </summary>
        /// <returns>Inhertied types</returns>
        public List<AstDeclaration> GetDeclarations()
        {
            if (this is AstClassDecl clsDecl)
                return clsDecl.Declarations;
            else if (this is AstStructDecl strDecl)
                return strDecl.Declarations;
            else if (this is AstGenericDecl genDecl)
                return genDecl.Declarations;
            return new List<AstDeclaration>();
        }

        public List<AstPropertyDecl> GetAllVirtualProps()
        {
            if (this is AstClassDecl clsDecl)
                return clsDecl.AllVirtualProps;
            else if (this is AstStructDecl strDecl)
                return strDecl.AllVirtualProps;
            return new List<AstPropertyDecl>();
        }

        public List<AstFuncDecl> GetAllVirtualMethods()
        {
            if (this is AstClassDecl clsDecl)
                return clsDecl.AllVirtualMethods;
            else if (this is AstStructDecl strDecl)
                return strDecl.AllVirtualMethods;
            return new List<AstFuncDecl>();
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

        public AstAttributeStmt GetAttribute(string name)
        {
            var attr = this.Attributes.FirstOrDefault(x =>
            {
                // could be if an attr was not infered properly
                if (x.AttributeName.OutType == null)
                    return false;
                return (x.AttributeName.OutType as ClassType).Declaration.NameWithNs == name;
            });
            return attr;
        }
    }
}
