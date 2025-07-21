using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Enums;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using Newtonsoft.Json;
using System.Text;
using System.Xml.Linq;

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

        public override AstDeclaration GetOnlyDeclareCopy()
        {
            var copy = new AstFuncDecl(
                Parameters.Select(x => x.GetDeepCopy() as AstParamDecl).ToList(),
                Returns.GetDeepCopy() as AstNestedExpr,
                null,
                Name.GetDeepCopy() as AstIdExpr,
                Documentation, Location)
            {
                IsPropertyFunction = IsPropertyFunction,
                BaseCtorCall = BaseCtorCall?.GetDeepCopy() as AstBaseCtorStmt,
                CallingConvention = CallingConvention,
                ClassFunctionType = ClassFunctionType,
                HasGenericTypes = HasGenericTypes,
                IsNestedDecl = IsNestedDecl,
                ParentDecl = ParentDecl,
                IsImported = IsImported,
                Scope = Scope,
                SourceFile = SourceFile,
                SubScope = SubScope,
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

            var copy = GetOnlyDeclareCopy() as AstFuncDecl;
            copy.GenericConstrains = copiedConstrains;
            copy.Body = Body?.GetDeepCopy() as AstBlockExpr;

            // handle containing parent shite
            if (copy.Body != null)
                foreach (var stmt in copy.Body.Statements)
                {
                    if (stmt is not AstDeclaration decl)
                        continue;

                    if (decl.IsNestedDecl)
                        decl.ParentDecl = copy;
                }
            return copy;
        }

        public override string ToString()
        {
            return $"func:{Name}";
        }

        /// <summary>
        /// Returns string with return type and args types but without name of func
        /// USED FOR FUNC and LAMBDA
        /// </summary>
        /// <returns></returns>
        public string ToCringeString()
        {
            string args;

            if ((Type.OutType as FunctionType).IsStaticFunction())
            {
                // the func is static...
                args = string.Join(":", Parameters.Select(p =>
                {
                    return p.Type.OutType.ToString();
                }));
            }
            else
            {
                // the func is non-static...
                // skip the first param with class object ptr
                args = string.Join(":", Parameters.Skip(1).Select(p =>
                {
                    return p.Type.OutType.ToString();
                }));
            }

            if (Returns.OutType != HapetType.CurrentTypeContext.VoidTypeInstance)
                return $"({Returns.OutType}:({args}))";
            else
                return $"(void:({args}))";
        }
    }
}
