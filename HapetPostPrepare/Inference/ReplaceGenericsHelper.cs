using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private Dictionary<string, AstNestedExpr> _currentGenericMapping = new Dictionary<string, AstNestedExpr>();

        public void ReplaceAllGenericTypesInClass(AstClassDecl clsDecl, List<AstNestedExpr> normalTypes)
        {
            // ini the dict
            _currentGenericMapping = new Dictionary<string, AstNestedExpr>();
            for (int i = 0; i < clsDecl.GenericNames.Count; ++i)
            {
                _currentGenericMapping.Add(clsDecl.GenericNames[i].Name, normalTypes[i]);
            }

            // replacing inheritance
            foreach (var inh in clsDecl.InheritedFrom)
            {
                ReplaceAllGenericTypesInExpr(inh);
            }

            // go all over the decls
            foreach (var decl in clsDecl.Declarations)
            {
                if (decl is AstFuncDecl funcDecl)
                {
                    ReplaceAllGenericTypesInFunction(funcDecl);
                }
                else if (decl is AstPropertyDecl propDecl)
                {
                    if (propDecl.GetBlock != null)
                    {
                        ReplaceAllGenericTypesInExpr(propDecl.GetBlock);
                    }
                    if (propDecl.SetBlock != null)
                    {
                        ReplaceAllGenericTypesInExpr(propDecl.SetBlock);
                    }

                    // replacing indexer parameter
                    if (propDecl is AstIndexerDecl indDecl)
                    {
                        ReplaceAllGenericTypesInExpr(indDecl.IndexerParameter);
                    }

                    ReplaceAllGenericTypesInVar(propDecl);
                }
                else if (decl is AstVarDecl fieldDecl) // field 
                {
                    ReplaceAllGenericTypesInVar(fieldDecl);
                }
            }
        }

        private void ReplaceAllGenericTypesInFunction(AstFuncDecl funcDecl)
        {

        }

        private void ReplaceAllGenericTypesInVar(AstVarDecl varDecl)
        {
            // replacing var attrs
            foreach (var a in varDecl.Attributes)
            {
                SetScopeAndParent(a, varDecl);
                PostPrepareExprScoping(a);
            }

            ReplaceAllGenericTypesInExpr(varDecl.Type);
            if (varDecl.Initializer != null)
            {
                ReplaceAllGenericTypesInExpr(varDecl.Initializer);
            }
        }

        private void ReplaceAllGenericTypesInParam(AstParamDecl paramDecl)
        {
            ReplaceAllGenericTypesInExpr(paramDecl.Type);
            if (paramDecl.DefaultValue != null)
            {
                ReplaceAllGenericTypesInExpr(paramDecl.DefaultValue);
            }
        }
    }
}
