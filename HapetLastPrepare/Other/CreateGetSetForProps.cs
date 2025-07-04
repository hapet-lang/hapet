using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using System.Collections.Generic;

namespace HapetLastPrepare
{
    public partial class LastPrepare
    {
        private void CreateGetSetForProps(AstPropertyDecl propDecl)
        {
            if (GenericsHelper.ShouldTheDeclBeSkippedFromCodeGen(propDecl.ContainingParent))
                return;

            var newDecls = AddPropertyShiteToDecl(propDecl);
            propDecl.ContainingParent.GetDeclarations().AddRange(newDecls);
            foreach (var d in newDecls)
            {
                d.IsDeclarationUsed = true;

                _postPreparer.SetScopeAndParent(d, propDecl);
                _postPreparer.PostPrepareDeclScoping(d);
                _postPreparer.PostPrepareStatementUpToCurrentStep(d);

                // we need to add the funcs to arr
                if ((propDecl.SpecialKeys.ContainsAny(TokenType.KwAbstract, TokenType.KwVirtual, TokenType.KwOverride) ||
                    propDecl.ContainingParent.GetAllVirtualProps().Contains(propDecl)) &&
                    d is AstFuncDecl fncD)
                {
                    List<AstFuncDecl> virt = null;
                    if (propDecl.ContainingParent is AstClassDecl clsD)
                        virt = clsD.AllVirtualMethods;
                    else if (propDecl.ContainingParent is AstStructDecl strD)
                        virt = strD.AllVirtualMethods;

                    virt?.Add(fncD);
                }
            }
        }

        private List<AstDeclaration> AddPropertyShiteToDecl(AstPropertyDecl prop)
        {
            List<AstDeclaration> declarationsToAdd = new List<AstDeclaration>();
            bool isParentInterface = false;
            bool isParentStruct = false;
            AstDeclaration parent = prop.ContainingParent;

            if (parent is AstClassDecl clsDecl)
            {
                isParentInterface = clsDecl.IsInterface;
            }
            else if (parent is AstStructDecl strDecl)
            {
                isParentStruct = true;
            }

            AstPropertyDecl orig = prop.OriginalGenericDecl as AstPropertyDecl;

            if (prop.GetBlock == null && prop.SetBlock == null)
            {
                // need to create a field :(
                AstVarDecl propField = prop.GetField(parent, isParentStruct);
                // add abstract key to the field if it is an interface
                if (isParentInterface)
                    SpecialKeysHelper.AddSpecialKeyToDecl(propField, Lexer.CreateToken(TokenType.KwAbstract, prop.Location.Beginning),
                        _compiler.MessageHandler, _postPreparer._currentSourceFile);
                declarationsToAdd.Add(propField);
            }
            if (prop.HasGet)
            {
                // need to create a 'get' method
                AstFuncDecl getFunc = prop.GetGetFunction(parent);
                // add abstract key to the method if it is an interface
                if (isParentInterface)
                    SpecialKeysHelper.AddSpecialKeyToDecl(getFunc, Lexer.CreateToken(TokenType.KwAbstract, prop.Location.Beginning),
                        _compiler.MessageHandler, _postPreparer._currentSourceFile);
                declarationsToAdd.Add(getFunc);

                // add first param 
                _postPreparer.FuncPrepareAfterAll(getFunc, parent);
            }
            if (prop.HasSet)
            {
                // need to create a 'set' method
                AstFuncDecl setFunc = prop.GetSetFunction(parent);
                // add abstract key to the method if it is an interface
                if (isParentInterface)
                    SpecialKeysHelper.AddSpecialKeyToDecl(setFunc, Lexer.CreateToken(TokenType.KwAbstract, prop.Location.Beginning),
                        _compiler.MessageHandler, _postPreparer._currentSourceFile);
                declarationsToAdd.Add(setFunc);

                // add first param 
                _postPreparer.FuncPrepareAfterAll(setFunc, parent);
            }

            // abs has to not have impl
            if (prop.SpecialKeys.Contains(TokenType.KwAbstract) &&
                (prop.GetBlock != null || prop.SetBlock != null))
            {
                _compiler.MessageHandler.ReportMessage(_postPreparer._currentSourceFile.Text, prop.Name, [], ErrorCode.Get(CTEN.AbsPropertyWithBody));
            }
            return declarationsToAdd;
        }
    }
}
