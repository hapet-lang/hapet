using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Xml.Linq;
using HapetFrontend.Scoping;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Parsing;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareMetadataDelegates(AstStatement stmt)
        {
            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            if (stmt is AstDelegateDecl del)
            {
                // have to do it here!!!
                del.Type.OutType = DelegateType.GetDelegateType(del, del.Scope);

                PostPrepareDelegateInference(del, inInfo, ref outInfo);

                // not yet created Invokes
                if (del.Functions.Count == 0)
                {
                    var structScope = new Scope($"{del.Name.Name}_scope", del.Scope);
                    del.SubScope = structScope;

                    AddInvokeDeclarationToDelegate(del);
                }
            }
        }
    }
}
