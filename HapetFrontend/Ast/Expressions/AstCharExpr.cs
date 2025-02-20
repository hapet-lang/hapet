using HapetFrontend.Types;
using System.Diagnostics;

namespace HapetFrontend.Ast.Expressions
{
    public class AstCharExpr : AstExpression
    {
        public char CharValue => (char)OutValue;
        public string RawValue { get; set; }

        public override string AAAName => nameof(AstCharExpr);

        public AstCharExpr(string rawValue, ILocation Location = null) : base(Location)
        {
            this.RawValue = rawValue;
            OutValue = rawValue.FirstOrDefault();
            OutType = CharType.DefaultType; // TODO: check prefixes for the size of char
        }
    }
}
