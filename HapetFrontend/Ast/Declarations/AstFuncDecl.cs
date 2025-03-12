using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Enums;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using Newtonsoft.Json;
using System.Text;

namespace HapetFrontend.Ast.Declarations
{
    public class AstFuncDecl : AstDeclaration
    {
        public CallingConvention CallingConvention { get; set; } = CallingConvention.Default;
        public ClassFunctionType ClassFunctionType { get; set; } = ClassFunctionType.Default;

        public List<AstParamDecl> Parameters { get; set; }
        public AstNestedExpr Returns { get; set; }

        [JsonIgnore]
        public AstBlockExpr Body { get; set; }

        /// <summary>
        /// Statement of calling base ctor. Used only for ctors!!!!
        /// </summary>
        public AstBaseCtorStmt BaseCtorCall { get; set; }

        /// <summary>
        /// Used for easier infferencing. Mean that the func is a get/set func
        /// </summary>
        public bool IsPropertyFunction { get; set; }

        public override string AAAName => nameof(AstFuncDecl);

        public AstFuncDecl(List<AstParamDecl> parameters, AstNestedExpr returns, AstBlockExpr body, AstIdExpr name, string doc = "", ILocation location = null) : base(name, doc, location)
        {
            Type = new AstNestedExpr(new AstIdExpr("func", location), null, location);
            Type.OutType = new FunctionType(this);

            Body = body;
            Parameters = parameters;
            Returns = returns;
        }

        public override AstStatement GetDeepCopy()
        {
            Dictionary<AstIdExpr, List<AstNestedExpr>> copiedConstrains = new Dictionary<AstIdExpr, List<AstNestedExpr>>();
            foreach (var cc in GenericConstrains)
            {
                copiedConstrains.Add(cc.Key.GetDeepCopy() as AstIdExpr, cc.Value.Select(x => x.GetDeepCopy() as AstNestedExpr).ToList());
            }

            var copy = new AstFuncDecl(
                Parameters.Select(x => x.GetDeepCopy() as AstParamDecl).ToList(),
                Returns.GetDeepCopy() as AstNestedExpr,
                Body?.GetDeepCopy() as AstBlockExpr,
                Name.GetDeepCopy() as AstIdExpr,
                Documentation, Location)
            {
                IsPropertyFunction = IsPropertyFunction,
                BaseCtorCall = BaseCtorCall?.GetDeepCopy() as AstBaseCtorStmt,
                CallingConvention = CallingConvention,
                ClassFunctionType = ClassFunctionType,
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

        public FuncDeclJson GetJson()
        {
            var parameters = Parameters.Select(x => x.GetJson()).ToList();
            var attributes = Attributes.Select(x => x.GetJson()).ToList();

            return new FuncDeclJson()
            {
                Parameters = parameters,
                ReturnType = HapetType.AsString(Returns.OutType, true),
                Name = GenericsHelper.GetPrettyGenericFuncName(Name.Name),
                SpecialKeys = SpecialKeys,
                IsGenericDecl = HasGenericTypes,
                Attributes = attributes,
                CallingConvention = CallingConvention,
                DocString = Documentation
            };
        }

        public string GenerateHashForGenericType(string genTypeName)
        {
            return Funcad.CreateMD5($"{SourceFile}{Name.Name}{ContainingParent?.Name.Name}{string.Join('_', Parameters.Select(x => x.Type.TryFlatten(null, null)))}{Returns.TryFlatten(null, null)}{genTypeName}");
        }
    }

    public class FuncDeclJson
    {
        public List<ParamDeclJson> Parameters { get; set; }
        public string ReturnType { get; set; }
        public string Name { get; set; }

        public List<TokenType> SpecialKeys { get; set; }
        public List<AttributeJson> Attributes { get; set; }

        public CallingConvention CallingConvention { get; set; }
        public bool IsGenericDecl { get; set; }

        public string DocString { get; set; }

        public AstFuncDecl GetAst(Compiler compiler)
        {
            var decl = new AstFuncDecl(Parameters.Select(x => x.GetAst(compiler)).ToList(), Parser.ParseType(ReturnType, compiler) as AstNestedExpr, null, new AstIdExpr(Name), DocString);
            decl.SpecialKeys.AddRange(SpecialKeys);
            decl.Attributes.AddRange(Attributes.Select(x => x.GetAst(compiler)));
            decl.CallingConvention = CallingConvention;
            return decl;
        }
    }
}
