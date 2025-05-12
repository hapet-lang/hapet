using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Helpers;
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
        public AstClassDecl Declaration { get; }

        public override string TypeName => "class";

        public static ClassType LiteralType { get; } = new ClassType(null);

        private Guid Guid { get; set; } // just for debug

        public override AstExpression GetAst()
        {
            return new AstNestedExpr(Declaration.Name.GetCopy(), null);
        }

        public ClassType(AstClassDecl decl)
            : base()
        {
            Declaration = decl;

            Guid = Guid.NewGuid();
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

    /// <summary>
    /// Doesn't have Ast shite in it but being created every time like a new instance
    /// </summary>
    public class TupleType : HapetType
    {
        public static readonly TupleType LiteralType = GetTuple(Array.Empty<(HapetType, string)>());

        public (HapetType type, string name)[] Members { get; }

        public override string TypeName => "tuple";

        public override AstExpression GetAst()
        {
            return new AstNestedExpr(new AstIdExpr(ToString()), null);
        }

        private TupleType((HapetType type, string name)[] members) : base()
        {
            Members = members;
        }

        public static TupleType GetTuple((HapetType type, string name)[] members)
        {
            return new TupleType(members);
        }

        public override string ToString()
        {
            var members = string.Join(", ", Members.Select(m =>
            {
                if (m.name != null) return $"{m.type} {m.name}";
                return HapetType.AsString(m.type);
            }));
            return $"({members})";
        }

        public override bool Equals(object obj)
        {
            if (obj is TupleType t)
            {
                if (Members.Length != t.Members.Length) return false;
                for (int i = 0; i < Members.Length; i++)
                    if (Members[i].type != t.Members[i].type) return false;

                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var m in Members)
            {
                hash.Add(m.type.GetHashCode());
            }
            return hash.ToHashCode();
        }
    }

    public class StructType : HapetType
    {
        [JsonIgnore]
        public AstStructDecl Declaration { get; }

        public static StructType LiteralType { get; } = new StructType(null);

        public override string TypeName => "struct";

        /// <summary>
        /// The property is set to 'true' in code gen when StructLayoutAttribute found
        /// </summary>
        public bool IsUserDefinedAlignment { get; set; } = false;

        public override AstExpression GetAst()
        {
            return new AstNestedExpr(Declaration.Name.GetCopy(), null);
        }

        public StructType(AstStructDecl decl)
            : base()
        {
            Declaration = decl;
        }

        public void ChangeSize(int size)
        {
            _size = size;
        }

        public void ChangeAlignment(int alignment)
        {
            _alignment = alignment;
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

        public override AstExpression GetAst()
        {
            return new AstNestedExpr(Declaration.Name.GetCopy(), null);
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

        public override AstExpression GetAst()
        {
            return new AstNestedExpr(Declaration.Name.GetCopy(), null);
        }

        public FunctionType(AstFuncDecl decl)
            : base(CurrentTypeContext.PointerSize, PointerType.PointerAlignment)
        {
            Declaration = decl;
        }

        public override string ToString()
        {
            if (Declaration.Returns.OutType != VoidType.Instance)
                return $"({Declaration.Returns.OutType}:{Declaration.Name.Name})";
            else
                return $"(void:{Declaration.Name.Name})";
        }

        /// <summary>
        /// Returns string with return type and args types but without name of func
        /// </summary>
        /// <returns></returns>
        public string ToCringeString()
        {
            string args;

            if (IsStaticFunction())
            {
                // the func is static...
                args = string.Join(":", Declaration.Parameters.Select(p =>
                {
                    return p.Type.OutType.ToString();
                }));
            }
            else
            {
                // the func is non-static...
                // skip the first param with class object ptr
                args = string.Join(":", Declaration.Parameters.Skip(1).Select(p =>
                {
                    return p.Type.OutType.ToString();
                }));
            }

            if (Declaration.Returns.OutType != VoidType.Instance)
                return $"({Declaration.Returns.OutType}:({args}))";
            else
                return $"(void:({args}))";
        }

        public bool IsStaticFunction()
        {
            return (Declaration.Parameters.FirstOrDefault() == null ||
                (Declaration.Parameters.FirstOrDefault().Type.OutType is not PointerType && Declaration.Parameters.FirstOrDefault().Name.Name != "this"));
        }
    }

    public class DelegateType : ClassType
    {
        public static DelegateType GetDelegateType(AstDelegateDecl targetDecl, Scope scope)
        {
            return GetDelegateType(targetDecl, AstDelegateDecl.GetDelegateClass(scope));
        }

        public static DelegateType GetDelegateType(AstDelegateDecl targetDecl, AstClassDecl delClassDecl)
        {
            if (targetDecl == null)
                return null;

            var existing = CurrentTypeContext.DelegateTypeInstances.FirstOrDefault(t => t.Value.TargetDeclaration == targetDecl).Value;
            if (existing != null)
                return existing;

            var type = new DelegateType(targetDecl, delClassDecl);

            CurrentTypeContext.DelegateTypeInstances[targetDecl] = type;
            return type;
        }

        public override string TypeName => "delegate";

        public AstDelegateDecl TargetDeclaration { get; set; }

        public override AstExpression GetAst()
        {
            return new AstNestedExpr(Declaration.Name.GetCopy(), null);
        }

        private DelegateType(AstDelegateDecl targetDecl, AstClassDecl decl)
            : base(decl)
        {
            TargetDeclaration = targetDecl;
        }

        public override string ToString()
        {
            return $"{Declaration.Name.Name}";
        }
    }
}
