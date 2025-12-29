using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using System;

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
        /// Special keys of 'get'.
        /// Like 'Anime { internal get; set; }'
        /// </summary>
        public List<Token> GetSpecialKeys { get; private set; } = new List<Token>();
        /// <summary>
        /// Special keys of 'set'.
        /// Like 'Anime { get; private set; }'
        /// </summary>
        public List<Token> SetSpecialKeys { get; private set; } = new List<Token>();

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
        /// The function that was generated for 'get'
        /// </summary>
        public AstFuncDecl GetFunction { get; set; }
        /// <summary>
        /// Block for 'set'. Could be null
        /// </summary>
        public AstBlockExpr SetBlock { get; set; }
        /// <summary>
        /// The function that was generated for 'set'
        /// </summary>
        public AstFuncDecl SetFunction { get; set; }

        /// <summary>
        /// Position of 'get' token used for LSP
        /// </summary>
        public ILocation GetTokenPosition { get; set; }
        /// <summary>
        /// Position of 'set' token used for LSP
        /// </summary>
        public ILocation SetTokenPosition { get; set; }

        public override string AAAName => nameof(AstPropertyDecl);

        public AstPropertyDecl(AstExpression type, AstIdExpr name, AstExpression ini = null, string doc = "", ILocation location = null) : base(type, name, ini, doc, location)
        {
        }

        public override string ToString()
        {
            return $"prop:{GenericsHelper.GetNameFromAst(Name, null)}";
        }

        public override AstDeclaration GetOnlyDeclareCopy()
        {
            var copy = new AstPropertyDecl(
                Type?.GetDeepCopy() as AstNestedExpr,
                Name?.GetDeepCopy() as AstIdExpr,
                null,
                Documentation, Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                HasGet = HasGet,
                HasSet = HasSet,
                IsImported = IsImported,
                HasGenericTypes = HasGenericTypes,
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
                GetTokenPosition = GetTokenPosition,
                SetTokenPosition = SetTokenPosition,
                GenericConstrainLocations = GenericConstrainLocations,
            };
            copy.Attributes.AddRange(Attributes);
            copy.SpecialKeys.AddRange(SpecialKeys);
            return copy;
        }

        public override AstStatement GetDeepCopy()
        {
            Dictionary<AstIdExpr, List<AstConstrainStmt>> copiedConstrains = new Dictionary<AstIdExpr, List<AstConstrainStmt>>();
            foreach (var cc in GenericConstrains)
            {
                copiedConstrains.Add(cc.Key.GetDeepCopy() as AstIdExpr, cc.Value.Select(x => x.GetDeepCopy() as AstConstrainStmt).ToList());
            }

            var copy = GetOnlyDeclareCopy() as AstPropertyDecl;
            copy.GenericConstrains = copiedConstrains;
            copy.Initializer = Initializer?.GetDeepCopy() as AstExpression;
            copy.GetBlock = GetBlock?.GetDeepCopy() as AstBlockExpr;
            copy.SetBlock = SetBlock?.GetDeepCopy() as AstBlockExpr;
            
            return copy;
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            if (Type == oldChild)
                Type = newChild as AstExpression;
            else if (Name == oldChild)
                Name = newChild as AstIdExpr;
            else if (GetBlock == oldChild)
                GetBlock = newChild as AstBlockExpr;
            else if (SetBlock == oldChild)
                SetBlock = newChild as AstBlockExpr;
        }

        #region Separating props into field and funcs
        public AstVarDecl GetField(AstDeclaration containingParent, bool forStruct)
        {
            var field = new AstVarDecl(
                Type.GetDeepCopy() as AstExpression, 
                Name.GetCopy($"field_{Name.Name}"), 
                Initializer?.GetDeepCopy() as AstExpression, Documentation, Location)
            {
                ContainingParent = containingParent,
                Parent = Parent,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            field.Attributes.AddRange(Attributes);
            field.IsPropertyField = true;
            field.IsImported = IsImported;
            field.IsSyntheticStatement = true;

            // no special keys for struct
            if (!forStruct)
            {
                field.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwPrivate, field.Type.Location.Beginning));
                // if the propa is static - make the field also static
                if (SpecialKeys.Contains(TokenType.KwStatic))
                    field.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwStatic, field.Type.Location.Beginning));
                // if the propa is shadowing - make the field also shadowing
                if (SpecialKeys.Contains(TokenType.KwNew))
                    field.SpecialKeys.Add(Lexer.CreateToken(TokenType.KwNew, field.Type.Location.Beginning));
            }
            
            return field;
        }

        public AstFuncDecl GetSetFunction(AstDeclaration containingParent)
        {
            // add indexer param if it is an indexer
            var prs = new List<AstParamDecl>() { new AstParamDecl(Type.GetDeepCopy() as AstExpression, new AstIdExpr("value")) };
            if (this is AstIndexerDecl indDecl)
                prs.Insert(0, indDecl.IndexerParameter.GetDeepCopy() as AstParamDecl);

            var func = GetPropaFunc(false, containingParent);
            func.Parameters.AddRange(prs);
            func.Returns = new AstNestedExpr(new AstIdExpr("void", Location), null, Location);

            bool hasNoFieldAttribute = this.GetAttribute("System.NoFieldAttribute") != null;
            if (SetBlock == null && !SpecialKeys.Contains(TokenType.KwAbstract) && !hasNoFieldAttribute)
            {
                // left part is null if it is a static propa
                AstNestedExpr leftPart = null;
                if (!SpecialKeys.Contains(TokenType.KwStatic))
                    leftPart = new AstNestedExpr(new AstIdExpr("this"), null);
                var setBlock = new AstBlockExpr(new List<AstStatement>()
                {
					// the stmt is - 'this.field_Prop = value'
					new AstAssignStmt(new AstNestedExpr(Name.GetCopy($"field_{Name.Name}"), leftPart), new AstIdExpr("value", Location), Location),
                }, Location);
                func.Body = setBlock;
            }
            else
            {
                func.Body = SetBlock;
            }

            SetFunction = func;
            return func;
        }

        public AstFuncDecl GetGetFunction(AstDeclaration containingParent)
        {
            // add indexer param if it is an indexer
            var prs = new List<AstParamDecl>();
            if (this is AstIndexerDecl indDecl)
                prs.Add(indDecl.IndexerParameter.GetDeepCopy() as AstParamDecl);

            var func = GetPropaFunc(true, containingParent);
            func.Parameters.AddRange(prs);
            func.Returns = Type.GetDeepCopy() as AstExpression;

            bool hasNoFieldAttribute = this.GetAttribute("System.NoFieldAttribute") != null;
            if (GetBlock == null && !SpecialKeys.Contains(TokenType.KwAbstract) && !hasNoFieldAttribute)
            {
                // left part is null if it is a static propa
                AstNestedExpr leftPart = null;
                if (!SpecialKeys.Contains(TokenType.KwStatic))
                    leftPart = new AstNestedExpr(new AstIdExpr("this", Location), null, Location);
                var getBlock = new AstBlockExpr(new List<AstStatement>()
                {
					// the stmt is - 'return this.field_Prop'
					new AstReturnStmt(new AstNestedExpr(Name.GetCopy($"field_{Name.Name}"), leftPart), Location),
                }, Location);
                func.Body = getBlock;
            }
            else
            {
                func.Body = GetBlock;
            }

            GetFunction = func;
            return func;
        }

        private AstFuncDecl GetPropaFunc(bool isGet, AstDeclaration containingParent)
        {
            AstFuncDecl func = new AstFuncDecl(
                new List<AstParamDecl>(),
                null,
                null,
                (isGet ? Name.GetCopy($"get_{Name.Name}") : Name.GetCopy($"set_{Name.Name}")),
                "",
                Location);
            func.SpecialKeys.AddRange(SpecialKeys);
            func.Attributes.AddRange(Attributes);
            func.ContainingParent = containingParent; // it has to be
            func.IsPropertyFunction = true;
            func.SourceFile = SourceFile;
            func.IsImported = IsImported;
            func.IsSyntheticStatement = true;

            // add specific special keys
            if (isGet)
                SpecialKeysHelper.ReplaceSpecialKeysByTypes(func, GetSpecialKeys);
            else
                SpecialKeysHelper.ReplaceSpecialKeysByTypes(func, SetSpecialKeys);

            return func;
        }
        #endregion

        public override AstVarDecl GetCopyForAnotherType(AstDeclaration decl)
        {
            var varDecl = GetDeepCopy() as AstPropertyDecl;
            varDecl.Parent = decl;
            varDecl.Scope = decl.SubScope;
            varDecl.SourceFile = decl.SourceFile;
            varDecl.ContainingParent = decl;
            return varDecl;
        }
    }
}
