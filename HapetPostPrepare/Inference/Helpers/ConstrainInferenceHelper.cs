using System.Runtime;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Enums;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        internal void PostPrepareGenericDeclConstrains(AstGenericDecl decl, InInfo inInfo, ref OutInfo outInfo)
        {
            // handle constrains
            foreach (var constrain in decl.Constrains)
            {
                switch (constrain.ConstrainType)
                {
                    case GenericConstrainType.CustomType:
                        HandleCustomConstrainType(decl, constrain, inInfo, ref outInfo);
                        break;
                    // ...
                }
            }
        }

        private void HandleCustomConstrainType(AstGenericDecl decl, AstConstrainStmt constrain, InInfo inInfo, ref OutInfo outInfo)
        {
            // inference the ident
            PostPrepareExprInference(constrain.Expr, inInfo, ref outInfo);
            // custom constains are always interface/class
            var theClass = (constrain.Expr.OutType as ClassType).Declaration;

            // we need to copy all the class decls to our genericDecl
            foreach (var d in theClass.Declarations)
            {
                // do not copy ini/ctor/stor/dtor funcs
                if (d is AstFuncDecl funcDecl && (
                    funcDecl.ClassFunctionType == ClassFunctionType.Initializer ||
                    funcDecl.ClassFunctionType == ClassFunctionType.Ctor ||
                    funcDecl.ClassFunctionType == ClassFunctionType.StaticCtor ||
                    funcDecl.ClassFunctionType == ClassFunctionType.Dtor))
                    continue;

                // create a copy of the decl 
                var copied = d.GetDeepCopy() as AstDeclaration;
                // clear bodies/initializers - there is no need for them
                if (copied is AstFuncDecl f && f.Body != null)
                    f.Body.Statements.Clear();
                else if (copied is AstVarDecl v)
                    v.Initializer = null;

                // add the copy
                decl.Declarations.Add(copied);
            }
        }
    }
}
