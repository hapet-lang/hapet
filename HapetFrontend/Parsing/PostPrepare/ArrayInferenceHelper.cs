using HapetFrontend.Ast;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;
using System.Runtime.InteropServices;

namespace HapetFrontend.Parsing.PostPrepare
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
                _compiler.ErrorHandler.ReportError(_currentSourceFile.Text, arrayExpr, $"Array cannot has initialization values when its size is not a const");
            }
            else if (arrayExpr.Elements.Count > 0 && currentSizeExpr.OutValue is NumberData numData && numData != arrayExpr.Elements.Count)
            {
                //  byte[] a2 = new byte[3] {1, 1, 2, 4}; - would error in C#
                _compiler.ErrorHandler.ReportError(_currentSourceFile.Text, arrayExpr, $"Array initialization values amount and its size different but they haму to be the same");
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
                            _compiler.ErrorHandler.ReportError(_currentSourceFile.Text, element, $"The element has to be an array type");
                            continue;
                        }
                        // it should be already prepared so just create new array type over it
                        arrayExpr.OutType = ArrayType.GetArrayType(elementArrayExpr.OutType);
                    }
                }
                else
                {
                    // if there are no ini elements - just post prepare what you have
                    // and set current arrayExpr OutType as ArrayType of out
                    var cloned = arrayExpr.Clone() as AstArrayCreateExpr;
                    cloned.SizeExprs.RemoveAt(cloned.SizeExprs.Count - 1);
                    PostPrepareFullArray(cloned);
                    arrayExpr.OutType = ArrayType.GetArrayType(cloned.OutType);
                }
            }
            else
            {
                // if it is just 1-d array - create the arrayType with just outType of Name
                arrayExpr.OutType = ArrayType.GetArrayType(arrayExpr.TypeName.OutType);
            }
        }
    }
}
