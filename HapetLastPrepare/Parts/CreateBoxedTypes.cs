using HapetFrontend.Ast.Declarations;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using HapetFrontend.Extensions;
using System.Linq;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Enums;
using HapetFrontend.Types;

namespace HapetLastPrepare
{
    public partial class LastPrepare
    {
        public void CreateBoxedTypes()
        {
            foreach (var str in _postPreparer.AllStructsMetadata.ToList())
            {
                if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(str))
                    continue;
                // reg type info if non static
                if (str.SpecialKeys.Contains(TokenType.KwStatic))
                    continue;

                _postPreparer._currentSourceFile = str.SourceFile;
                CreateBoxedType(str);
            }
        }

        private void CreateBoxedType(AstStructDecl str)
        {
            var deepCopy = str.GetDeepCopy() as AstStructDecl;
            deepCopy.OriginalOfBoxedType = str;
            deepCopy.Name = str.Name.GetCopy($"boxed.{GenericsHelper.GetCodegenGenericName(str.Name, _compiler.MessageHandler)}");

            // remove stor/ctor/dtor/ini and other shite
            foreach (var d in deepCopy.Declarations.ToList())
            {
                if ((d is AstVarDecl vd && vd.IsStaticCtorField) || (d is AstFuncDecl fd && fd.ClassFunctionType != ClassFunctionType.Default))
                    deepCopy.Declarations.Remove(d);
            }

            deepCopy.Scope = str.SourceFile.NamespaceScope;
            _postPreparer.PostPrepareDeclScoping(deepCopy);
            _postPreparer.PostPrepareStatementUpToCurrentStep(deepCopy);

            // add typeinfo 
            var tp = new AstIdExpr("uintptr") { OutType = HapetType.CurrentTypeContext.PtrToVoidType };
            deepCopy.Declarations.Insert(0, new AstVarDecl(new AstNestedExpr(tp, null) { OutType = tp.OutType }, new AstIdExpr("typeinfo")));

            _postPreparer.AllStructsMetadata.Add(str);
        }
    }
}
