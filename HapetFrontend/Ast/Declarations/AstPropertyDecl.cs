using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Extensions;
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

        public AstPropertyDecl(AstExpression type, AstIdExpr name, AstExpression ini = null, string doc = "", ILocation location = null) : base(type, name, ini, doc, location)
        {
        }

        public override AstStatement GetDeepCopy()
        {
            Dictionary<AstIdExpr, List<AstNestedExpr>> copiedConstrains = new Dictionary<AstIdExpr, List<AstNestedExpr>>();
            foreach (var cc in GenericConstrains)
            {
                copiedConstrains.Add(cc.Key.GetDeepCopy() as AstIdExpr, cc.Value.Select(x => x.GetDeepCopy() as AstNestedExpr).ToList());
            }

            var copy = new AstPropertyDecl(
                Type.GetDeepCopy() as AstNestedExpr,
                Name.GetDeepCopy() as AstIdExpr,
                Initializer?.GetDeepCopy() as AstExpression,
                Documentation, Location)
            {
                HasGet = HasGet,
                HasSet = HasSet,
                GetBlock = GetBlock?.GetDeepCopy() as AstBlockExpr,
                SetBlock = SetBlock?.GetDeepCopy() as AstBlockExpr,
                IsImported = IsImported,
                GenericNames = GenericNames?.Select(x => x.GetDeepCopy() as AstIdExpr).ToList(),
                GenericConstrains = copiedConstrains,
                HasGenericTypes = HasGenericTypes,
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
            };
            copy.Attributes.AddRange(Attributes);
            copy.SpecialKeys.AddRange(SpecialKeys);
            return copy;
        }

        #region Separating props into field and funcs
        public AstVarDecl GetField(AstDeclaration containingParent, bool forStruct)
        {
            var field = new AstVarDecl(Type, Name.GetCopy($"field_{Name.Name}"), Initializer, Documentation, Location)
            {
                ContainingParent = containingParent,
                Parent = Parent,
                Scope = Scope,
                SourceFile = SourceFile,
            };
            field.Attributes.AddRange(Attributes);
            field.IsPropertyField = true;

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

        public AstFuncDecl GetSetFunction(AstDeclaration containingParent, bool addFirstParam = false)
        {
            // add indexer param if it is an indexer
            var prs = new List<AstParamDecl>() { new AstParamDecl(Type, new AstIdExpr("value")) };
            if (this is AstIndexerDecl indDecl)
                prs.Insert(0, indDecl.IndexerParameter.GetCopy());

            var func = GetPropaFunc(addFirstParam, false, containingParent);
            func.Parameters.AddRange(prs);
            func.Returns = new AstNestedExpr(new AstIdExpr("void", Location), null, Location);

            if (SetBlock == null && !SpecialKeys.Contains(TokenType.KwAbstract))
            {
                // left part is null if it is a static propa
                AstNestedExpr leftPart = null;
                if (!SpecialKeys.Contains(TokenType.KwStatic))
                    leftPart = new AstNestedExpr(new AstIdExpr("this"), null);
                var setBlock = new AstBlockExpr(new List<AstStatement>()
                {
					// the stmt is - 'this.field_Prop = value'
					new AstAssignStmt(new AstNestedExpr(Name.GetCopy($"field_{Name.Name}"), leftPart), new AstIdExpr("value"), Location),
                }, Location);
                func.Body = setBlock;
            }
            else
            {
                func.Body = SetBlock;
            }
            return func;
        }

        public AstFuncDecl GetGetFunction(AstDeclaration containingParent, bool addFirstParam = false)
        {
            // add indexer param if it is an indexer
            var prs = new List<AstParamDecl>();
            if (this is AstIndexerDecl indDecl)
                prs.Add(indDecl.IndexerParameter.GetCopy());

            var func = GetPropaFunc(addFirstParam, true, containingParent);
            func.Parameters.AddRange(prs);
            func.Returns = Type;

            if (GetBlock == null && !SpecialKeys.Contains(TokenType.KwAbstract))
            {
                // left part is null if it is a static propa
                AstNestedExpr leftPart = null;
                if (!SpecialKeys.Contains(TokenType.KwStatic))
                    leftPart = new AstNestedExpr(new AstIdExpr("this"), null);
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
            return func;
        }

        private AstFuncDecl GetPropaFunc(bool addFirstParam, bool isGet, AstDeclaration containingParent)
        {
            AstFuncDecl func = new AstFuncDecl(
                new List<AstParamDecl>(),
                null,
                null,
                (isGet ? Name.GetCopy($"get_{Name.Name}") : Name.GetCopy($"set_{Name.Name}")),
                "",
                Location);
            func.SpecialKeys.AddRange(SpecialKeys);
            func.ContainingParent = containingParent; // it has to be
            func.IsPropertyFunction = true;
            func.SourceFile = SourceFile;

            // if we need to add 'this' param
            if (addFirstParam)
            {
                // for generic type - need to create an AstIdGenericExpr
                AstIdExpr thisParamType = containingParent.Name.GetCopy();
                // creating the class instance 'this' param
                AstExpression paramType = new AstPointerExpr(thisParamType, false);
                AstIdExpr paramName = new AstIdExpr("this");
                AstParamDecl thisParam = new AstParamDecl(new AstNestedExpr(paramType, null), paramName);
                // adding the param as the func first param
                func.Parameters.Insert(0, thisParam);
            }

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
