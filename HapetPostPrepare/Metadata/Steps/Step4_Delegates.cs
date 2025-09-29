using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;
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
                (del.Type.OutType as DelegateType).Declaration = HapetType.CurrentTypeContext.DelegateTypeInstance.Declaration;

                PostPrepareDelegateInference(del, inInfo, ref outInfo);

                // not yet created Invokes
                if (del.Functions.Count == 0)
                {
                    var structScope = new Scope($"{del.Name.Name}_scope", del.Scope) { GlobalScope = _compiler.GlobalScope };
                    del.SubScope = structScope;

                    AddInvokeDeclarationToDelegate(del);
                }
            }
        }
    }
}
