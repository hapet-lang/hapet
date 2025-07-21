using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json;
using System.Drawing;
using System.Reflection;

namespace HapetFrontend.Types
{
    public class ClassType : HapetType
    {
        [JsonIgnore]
        public AstClassDecl Declaration { get; set; }

        public override string TypeName => "class";

        public static ClassType LiteralType { get; } = new ClassType(null);

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstNestedExpr(Declaration.Name.GetCopy(), null)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        public ClassType(AstClassDecl decl)
            : base()
        {
            Declaration = decl;
        }

        public override string ToString()
        {
            return $"{(Declaration != null ? Declaration.Name.Name : "null")}";
        }

        public override int Match(HapetType concrete)
        {
            if (concrete is ClassType cls && cls == this)
            {
                int score = 0;
                return score;
            }
            return -1;
        }
    }

    public class StructType : HapetType
    {
        [JsonIgnore]
        public AstStructDecl Declaration { get; set; }

        public static StructType LiteralType { get; } = new StructType(null);

        public override string TypeName => "struct";

        /// <summary>
        /// The property is set to 'true' in code gen when StructLayoutAttribute found
        /// </summary>
        public bool IsUserDefinedAlignment { get; set; } = false;

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstNestedExpr(Declaration.Name.GetCopy(), null)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        public StructType(AstStructDecl decl)
            : base()
        {
            Declaration = decl;
        }

        public override string ToString()
        {
            return $"{Declaration.Name}";
        }

        public int GetIndexOfMember(string member)
        {
            return Declaration.Declarations.FindIndex(m => m.Name.Name == member);
        }

        public override bool Equals(object obj)
        {
            if (obj is StructType s)
            {
                if (Declaration != s.Declaration)
                    return false;
                return true;
            }
            return false;
        }

        public override int Match(HapetType concrete)
        {
            if (concrete is StructType)
            {
                int score = 0;
                return score;
            }
            return -1;
        }

        public override int GetHashCode()
        {
            var hashCode = 1624555593;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            return hashCode;
        }
    }

    public class EnumType : HapetType
    {
        [JsonIgnore]
        public AstEnumDecl Declaration { get; set; }

        public override string TypeName => "enum";

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstNestedExpr(Declaration.Name.GetCopy(), null)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        public EnumType(AstEnumDecl decl) : base()
        {
            Declaration = decl;
        }

        public override string ToString()
        {
            return $"{Declaration.Name.Name}";
        }

        public override int Match(HapetType concrete)
        {
            if (concrete is EnumType str)
            {
                int score = 0;
                return score;
            }
            return -1;
        }
    }

    public class FunctionType : HapetType
    {
        [JsonIgnore]
        public AstFuncDecl Declaration { get; set; }

        public override string TypeName => "func";

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstNestedExpr(Declaration.Name.GetCopy(), null)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        public FunctionType(AstFuncDecl decl)
            : base(CurrentTypeContext.PointerSize, PointerType.PointerAlignment)
        {
            Declaration = decl;
        }

        public override string ToString()
        {
            if (Declaration.Returns.OutType != CurrentTypeContext.VoidTypeInstance)
                return $"({Declaration.Returns.OutType}:{Declaration.Name.Name})";
            else
                return $"(void:{Declaration.Name.Name})";
        }

        public bool IsStaticFunction()
        {
            return Declaration.SpecialKeys.Contains(TokenType.KwStatic);
        }
    }

    public class DelegateType : ClassType
    {
        public override string TypeName => "delegate";

        public AstDelegateDecl TargetDeclaration { get; set; }

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstNestedExpr(Declaration.Name.GetCopy(), null)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        public DelegateType(AstDelegateDecl targetDecl)
            : base(null)
        {
            TargetDeclaration = targetDecl;
        }

        public override string ToString()
        {
            return $"{Declaration.Name.Name}";
        }
    }

    public class LambdaType : HapetType
    {
        [JsonIgnore]
        public AstLambdaExpr Declaration { get; set; }

        public override string TypeName => "lambda";

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            throw new NotSupportedException("GetAst not supported for LambdaType");
        }

        public LambdaType(AstLambdaExpr decl)
            : base(CurrentTypeContext.PointerSize, PointerType.PointerAlignment)
        {
            Declaration = decl;
        }

        public override string ToString()
        {
            if (Declaration.Returns.OutType != CurrentTypeContext.VoidTypeInstance)
                return $"({Declaration.Returns.OutType}:_lambda)";
            else
                return $"(void:_lambda)";
        }
    }
}
