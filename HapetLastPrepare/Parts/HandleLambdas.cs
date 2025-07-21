using HapetFrontend.Ast.Declarations;

namespace HapetLastPrepare
{
    public partial class LastPrepare
    {
        private void HandleLambdas()
        {
            // TODO: handle here non-static nested/lambdas
            foreach (var d in _compiler.LambdasAndNested)
            {
                if (d is AstFuncDecl)
                {

                }
                else if (d is AstLambdaDecl)
                {

                }
            }
        }
    }
}
