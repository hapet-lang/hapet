using System.Runtime;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Enums;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
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

            // single post prepare
            PostPrepareStatementUpToCurrentStep(decl);
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
                if (copied is AstFuncDecl f)
                    f.Body = null;
                else if (copied is AstVarDecl v)
                    v.Initializer = null;

                // change the parent
                copied.ContainingParent = decl;
                // reset name
                if (copied is AstFuncDecl func1)
                    func1.Name = func1.Name.GetCopy(func1.Name.Name.GetPureFuncName());

                // we should make all the decls abstract
                SpecialKeysHelper.ReplaceSpecialKeysByTypes(copied, new List<Token>() { Lexer.CreateToken(TokenType.KwAbstract, copied.Location.Beginning) });

                // we need to change first param in non static funcs
                if (copied is AstFuncDecl func && !func.SpecialKeys.Contains(TokenType.KwStatic))
                    ReplaceFirstParamOnNonStaticFunc(func, decl);

                // add the copy
                decl.Declarations.Add(copied);
            }
        }

        private void ReplaceFirstParamOnNonStaticFunc(AstFuncDecl func, AstGenericDecl decl)
        {
            /// almost the same as in <see cref="FuncPrepareAfterAll"/>
            var thisParamType = decl.Name.GetCopy();
            // creating the class instance 'this' param
            AstExpression paramType = new AstPointerExpr(thisParamType, false);
            AstIdExpr paramName = new AstIdExpr("this");
            AstParamDecl thisParam = new AstParamDecl(new AstNestedExpr(paramType, null), paramName);
            // replacing
            func.Parameters[0] = thisParam;
        }
    }
}
