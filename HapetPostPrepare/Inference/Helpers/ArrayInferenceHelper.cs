using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using HapetFrontend.Types;
using System.Runtime.InteropServices;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareFullArray(AstArrayCreateExpr arrayExpr)
        {
            // getting the current size
            AstExpression currentSizeExpr = arrayExpr.SizeExprs.Last();

            // all the dimetions should be checked for this shite!
            if (arrayExpr.Elements.Count > 0 && currentSizeExpr.OutValue == null)
            {
                // expected a const value to be used when creating an array with elements
                // byte[] a2 = new byte[b] {1, b, 2, 4}; - would error in C#
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, arrayExpr, [], ErrorCode.Get(CTEN.ArrayVarSizeAndVals));
            }
            else if (arrayExpr.Elements.Count > 0 && currentSizeExpr.OutValue is NumberData numData && numData.IntValue != arrayExpr.Elements.Count)
            {
                //  byte[] a2 = new byte[3] {1, 1, 2, 4}; - would error in C#
                _compiler.MessageHandler.ReportMessage(_currentSourceFile, arrayExpr,
                    [numData.ToString(), arrayExpr.Elements.Count.ToString()], ErrorCode.Get(CTEN.ArraySizeAndValsDiffer));
            }

            // if it is ndim array
            if (arrayExpr.SizeExprs.Count > 1)
            {
                // if there are ini elements
                if (arrayExpr.Elements.Count > 0)
                {
                    // just foreach on elements and validate them
                    foreach (var element in arrayExpr.Elements)
                    {
                        // the elements of ndim array have to be array typed
                        if (element is not AstArrayCreateExpr elementArrayExpr)
                        {
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile, element, [], ErrorCode.Get(CTEN.ArrayTypeAsElement));
                            continue;
                        }
                        // it should be already prepared so just create new array type over it
                        arrayExpr.OutType = GetArrayType(arrayExpr.TypeName, arrayExpr);
                    }
                }
                else
                {
                    // if there are no ini elements - just post prepare what you have
                    // and set current arrayExpr OutType as ArrayType of out
                    var cloned = arrayExpr.Clone() as AstArrayCreateExpr;
                    cloned.SizeExprs.RemoveAt(cloned.SizeExprs.Count - 1);
                    PostPrepareFullArray(cloned);
                    arrayExpr.OutType = GetArrayType(arrayExpr.TypeName, arrayExpr);
                }
            }
            else
            {
                // if it is just 1-d array - create the arrayType with just outType of Name
                arrayExpr.OutType = GetArrayType(arrayExpr.TypeName, arrayExpr);
            }
        }
    }
}
