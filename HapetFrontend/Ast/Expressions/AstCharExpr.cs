using HapetFrontend.Ast.Declarations;
using HapetFrontend.Types;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstCharExpr : AstExpression
    {
        public char CharValue => (char)OutValue;
        public string RawValue { get; set; }

        public override string AAAName => nameof(AstCharExpr);

        public AstCharExpr(string rawValue, ILocation location = null) : base(location)
        {
            this.RawValue = rawValue;
            OutValue = rawValue.FirstOrDefault();
            OutType = HapetType.CurrentTypeContext.CharTypeInstance; // TODO: check prefixes for the size of char
        }

        public override AstStatement GetDeepCopy()
        {
            var copy = new AstCharExpr(
                RawValue,
                Location)
            {
                IsSyntheticStatement = IsSyntheticStatement,
                IsCompileTimeValue = IsCompileTimeValue,
                OutType = OutType,
                OutValue = OutValue,
                Scope = Scope,
                SourceFile = SourceFile,
                TupleNameList = TupleNameList,
            };
            return copy;
        }

        public override void ReplaceChild(AstStatement oldChild, AstStatement newChild)
        {
            throw new NotImplementedException();
        }
    }
}
