using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Scoping;
using System.Drawing;
using System.Numerics;

namespace HapetFrontend.Types
{
    public class VoidType : StructType
    {
        public override string ToString() => "void";

        public override string TypeName => ToString();

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstNestedExpr(new AstIdExpr(ToString())
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            }, null)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        public VoidType(AstStructDecl astStructDecl) : base(astStructDecl) 
        {
            _size = 0;
            _alignment = 0;
        }

        public override int Match(HapetType concrete)
        {
            if (concrete is VoidType)
            {
                return 0;
            }
            return -1;
        }
    }

    public class BoolType : StructType
    {
        public override string ToString() => "bool";

        public override string TypeName => ToString();

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstNestedExpr(new AstIdExpr(ToString())
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            }, null)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        public BoolType(AstStructDecl astStructDecl) : base(astStructDecl) 
        {
            _size = 1;
            _alignment = 1;
        }

        public override int Match(HapetType concrete)
        {
            if (concrete is BoolType)
            {
                return 0;
            }
            return -1;
        }
    }

    public class IntType : StructType
    {
        public new static IntType LiteralType { get; } = new IntType(null, -1, false);
        public static IntType DefaultType => HapetType.CurrentTypeContext.GetIntType(4, true);

        public override string TypeName => ToString();

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstNestedExpr(new AstIdExpr(ToString())
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            }, null)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        public IntType(AstStructDecl astStructDecl, int size, bool sign) : base(astStructDecl)
        {
            Signed = sign;
            _size = size;
            _alignment = size;
        }

        public bool Signed { get; private set; }

        public override string ToString()
        {
            if (Signed)
            {
                switch (GetSize())
                {
                    case 1:
                        return "sbyte";
                    case 2:
                        return "short";
                    case 4:
                        return "int";
                    default:
                        return "long";
                }
            }
            else
            {
                switch (GetSize())
                {
                    case 1:
                        return "byte";
                    case 2:
                        return "ushort";
                    case 4:
                        return "uint";
                    default:
                        return "ulong";
                }
            }
        }

        public override int Match(HapetType concrete)
        {
            if (concrete is ReferenceType r)
                concrete = r.TargetType;

            if (concrete.IsExactly<IntType>())
            {
                if ((concrete as IntType).Signed != this.Signed)
                    return -1;

                if (concrete.GetSize() != this.GetSize())
                    return -1;
                return 0;
            }
            return -1;
        }

        public BigInteger MinValue => (Signed, GetSize()) switch
        {
            (true, 1) => sbyte.MinValue,
            (true, 2) => short.MinValue,
            (true, 4) => int.MinValue,
            (true, 8) => long.MinValue,
            (false, 1) => byte.MinValue,
            (false, 2) => ushort.MinValue,
            (false, 4) => uint.MinValue,
            (false, 8) => ulong.MinValue,
            _ => throw new NotImplementedException()
        };

        public BigInteger MaxValue => (Signed, GetSize()) switch
        {
            (true, 1) => sbyte.MaxValue,
            (true, 2) => short.MaxValue,
            (true, 4) => int.MaxValue,
            (true, 8) => long.MaxValue,
            (false, 1) => byte.MaxValue,
            (false, 2) => ushort.MaxValue,
            (false, 4) => uint.MaxValue,
            (false, 8) => ulong.MaxValue,
            _ => throw new NotImplementedException()
        };
    }

    public class FloatType : StructType
    {
        public new static FloatType LiteralType { get; } = new FloatType(null, -1);
        public static FloatType DefaultType => CurrentTypeContext.GetFloatType(4);

        public override string TypeName => ToString();

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstNestedExpr(new AstIdExpr(ToString())
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            }, null)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        public FloatType(AstStructDecl astStructDecl, int size) : base(astStructDecl) 
        {
            _size = size;
            _alignment = size;
        }

        public override string ToString()
        {
            switch (GetSize())
            {
                case 4:
                    return "float";
                case 8:
                    return "double";
            }
            return "float";
        }

        public override int Match(HapetType concrete)
        {
            if (concrete is ReferenceType r)
                concrete = r.TargetType;

            if (concrete is FloatType t)
            {
                if (concrete.GetSize() > this.GetSize())
                    return -1;
                if (concrete.GetSize() < this.GetSize())
                    return 1;
                return 0;
            }
            return -1;
        }

        public double MinValue => GetSize() switch
        {
            4 => float.MinValue,
            8 => double.MinValue,
            _ => throw new NotImplementedException()
        };

        public double MaxValue => GetSize() switch
        {
            4 => float.MaxValue,
            8 => double.MaxValue,
            _ => throw new NotImplementedException()
        };
        public double NaN => GetSize() switch
        {
            4 => float.NaN,
            8 => double.NaN,
            _ => throw new NotImplementedException()
        };

        public double PosInf => GetSize() switch
        {
            4 => float.PositiveInfinity,
            8 => double.PositiveInfinity,
            _ => throw new InvalidOperationException()
        };

        public double NegInf => GetSize() switch
        {
            4 => float.NegativeInfinity,
            8 => double.NegativeInfinity,
            _ => throw new InvalidOperationException()
        };
    }

    public class PointerType : HapetType
    {
        public static int PointerAlignment => CurrentTypeContext.PointerSize;

        private static Dictionary<HapetType, PointerType> _types = new Dictionary<HapetType, PointerType>();
        public static PointerType VoidLiteralType { get; } = new PointerType(HapetType.CurrentTypeContext.VoidTypeInstance);
        public static PointerType NullLiteralType { get; } = new PointerType(HapetType.CurrentTypeContext.VoidTypeInstance) { IsPointerToNull = true, };

        public override string TypeName => "ptr";
        /// <summary>
        /// Cringe kostyl to search for nullptr types
        /// </summary>
        public bool IsPointerToNull { get; set; }

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstPointerExpr(TargetType.GetAst() as AstExpression)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        private PointerType(HapetType target) : base(
            target switch
            {
                _ => CurrentTypeContext.PointerSize * 1,
            }, PointerAlignment)
        {
            TargetType = target;
        }

        public HapetType TargetType { get; set; }

        public static PointerType GetPointerType(HapetType targetType)
        {
            if (targetType == null)
                return null;

            if (_types.ContainsKey((targetType)))
            {
                return _types[(targetType)];
            }

            var type = new PointerType(targetType);

            _types[(targetType)] = type;
            return type;
        }

        public override string ToString()
        {
            return $"{(TargetType == null ? "null" : TargetType)}*";
        }

        public override bool Equals(object obj)
        {
            if (obj is PointerType p)
            {
                return TargetType == p.TargetType;
            }
            return false;
        }

        public override int Match(HapetType concrete)
        {
            if (concrete is ReferenceType r)
                concrete = r.TargetType;

            if (concrete is PointerType p)
            {
                if (TargetType == null && p.TargetType == null)
                    return 0;

                if (TargetType == null || p.TargetType == null)
                    return -1;

                var targetMatch = this.TargetType.Match(p.TargetType);
                if (targetMatch == -1)
                    return -1;
                return targetMatch;
            }
            return -1;
        }

        public override int GetHashCode()
        {
            var hashCode = -1663075914;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<HapetType>.Default.GetHashCode(TargetType);
            return hashCode;
        }
    }

    public class ReferenceType : HapetType
    {
        private static Dictionary<HapetType, ReferenceType> _types = new Dictionary<HapetType, ReferenceType>();

        public HapetType TargetType { get; set; }

        public override string TypeName => "ref";

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstAddressOfExpr(TargetType.GetAst() as AstExpression, null)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        private ReferenceType(HapetType target) : base(CurrentTypeContext.PointerSize, PointerType.PointerAlignment)
        {
            TargetType = target;
        }

        public static ReferenceType GetRefType(HapetType targetType)
        {
            if (targetType == null)
                return null;

            if (_types.ContainsKey((targetType)))
            {
                return _types[(targetType)];
            }

            var type = new ReferenceType(targetType);

            _types[(targetType)] = type;
            return type;
        }

        public override string ToString()
        {
            return $"ref {(TargetType == null ? "null" : TargetType)}";
        }

        public override int Match(HapetType concrete)
        {
            if (concrete is ReferenceType r)
            {
                var targetMatch = this.TargetType.Match(r.TargetType);
                if (targetMatch == -1)
                    return -1;
                return targetMatch;
            }

            return -1;
        }

        public override bool Equals(object obj)
        {
            if (obj is ReferenceType r)
            {
                return TargetType == r.TargetType;
            }
            return false;
        }

        public override int GetHashCode()
        {
            var hashCode = -1663075914;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<HapetType>.Default.GetHashCode(TargetType);
            return hashCode;
        }
    }

    public class ArrayType : StructType
    {
        public new static ArrayType LiteralType { get; } = new ArrayType(null, null);

        public HapetType TargetType { get; set; }

        public override string TypeName => $"array";

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstArrayExpr(TargetType.GetAst() as AstExpression)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        public ArrayType(HapetType target, AstStructDecl arrStructDecl) : base(arrStructDecl)
        {
            TargetType = target;
        }

        public override string ToString()
        {
            return $"{TargetType}[]";
        }

        public PointerType ToPointerType()
        {
            return PointerType.GetPointerType(TargetType);
        }

        public override int Match(HapetType concrete)
        {
            if (concrete is ArrayType p)
                return this.TargetType.Match(p.TargetType);
            return -1;
        }

        public override bool Equals(object obj)
        {
            if (obj is ArrayType r)
            {
                return TargetType == r.TargetType;
            }
            return false;
        }

        public override int GetHashCode()
        {
            var hashCode = -687864485;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<HapetType>.Default.GetHashCode(TargetType);
            return hashCode;
        }

        /// <summary>
        /// Returns 'true' is the types are the same AND 'true'
        /// when trying to assing smth like 'string[]' to pure
        /// 'Array' type
        /// </summary>
        /// <param name="curr"></param>
        /// <param name="req"></param>
        /// <returns></returns>
        public static bool IsCouldBeCastedIncludingArray(HapetType curr, HapetType req)
        {
            if (curr is ArrayType && req is StructType stT && stT.Declaration.Name.Name == "System.Array")
            {
                return true;
            }
            else if (curr is PointerType pt1 && req is PointerType pt2)
            {
                return IsCouldBeCastedIncludingArray(pt1.TargetType, pt2.TargetType);
            }
            else if (curr is ArrayType ar1 && req is ArrayType ar2)
            {
                return IsCouldBeCastedIncludingArray(ar1.TargetType, ar2.TargetType);
            }
            return curr == req;
        }
    }

    public class CharType : StructType
    {
        public override string TypeName => "char";

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstNestedExpr(new AstIdExpr(ToString())
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            }, null)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        public CharType(AstStructDecl astStructDecl, int size) : base(astStructDecl)
        {
            _size = size;
            _alignment = size;
        }

        public override string ToString()
        {
            return "char";
        }

        public override int Match(HapetType concrete)
        {
            if (concrete is ReferenceType r)
                concrete = r.TargetType;

            if (concrete is CharType)
            {
                if (concrete.GetSize() != this.GetSize())
                    return -1;
                return 0;
            }
            return -1;
        }

        public BigInteger MinValue => GetSize() switch
        {
            2 => ushort.MinValue,
            _ => throw new NotImplementedException()
        };

        public BigInteger MaxValue => GetSize() switch
        {
            2 => ushort.MaxValue,
            _ => throw new NotImplementedException()
        };
    }

    public class StringType : StructType
    {
        public override string TypeName => "string";

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstNestedExpr(new AstIdExpr("System.String")
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            }, null)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        public StringType(AstStructDecl astStructDecl) : base(astStructDecl) { }

        public override int Match(HapetType concrete)
        {
            if (concrete is StringType)
            {
                return 0;
            }
            return -1;
        }
    }

    public class IntPtrType : IntType
    {
        public override string TypeName => ToString();

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstNestedExpr(new AstIdExpr(TypeName)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            }, null)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        public IntPtrType(AstStructDecl astStructDecl) : base(astStructDecl, -1, false) 
        { 

        }

        public override string ToString() => "uintptr";

        public override int Match(HapetType concrete)
        {
            if (concrete is ReferenceType r)
                concrete = r.TargetType;

            if (concrete is IntPtrType t)
            {
                if (t.Signed != this.Signed)
                    return -1;

                if (concrete.GetSize() != this.GetSize())
                    return -1;
                return 0;
            }
            return -1;
        }
    }

    public class PtrDiffType : IntType
    {
        public override string TypeName => "ptrdiff";

        public override AstExpression GetAst(AstExpression iniExpr = null)
        {
            return new AstNestedExpr(new AstIdExpr(TypeName)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            }, null)
            {
                Scope = iniExpr?.Scope,
                SourceFile = iniExpr?.SourceFile,
                Location = iniExpr?.Location,
            };
        }

        public PtrDiffType(AstStructDecl astStructDecl) : base(astStructDecl, -1, true) 
        { 
        
        }
        public override string ToString() => "ptrdiff";

        public override int Match(HapetType concrete)
        {
            if (concrete is ReferenceType r)
                concrete = r.TargetType;

            if (concrete is PtrDiffType t)
            {
                if (t.Signed != this.Signed)
                    return -1;

                if (concrete.GetSize() != this.GetSize())
                    return -1;
                return 0;
            }
            return -1;
        }
    }
}
