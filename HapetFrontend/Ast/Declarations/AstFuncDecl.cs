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
        public AstExpression Returns { get; set; }

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

        public AstFuncDecl(List<AstParamDecl> parameters, AstExpression returns, AstBlockExpr body, AstIdExpr name, string doc = "", ILocation location = null) : base(name, doc, location)
        {
            Type = new AstIdExpr("func", location);
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

        public string GenerateHashForGenericType(string genTypeName)
        {
            return Funcad.CreateMD5($"{SourceFile}{Name.Name}{ContainingParent?.Name.Name}{string.Join('_', Parameters.Select(x => HapetType.AsString(x.Type.OutType)))}{HapetType.AsString(Returns.OutType)}{genTypeName}");
        }
    }
}
