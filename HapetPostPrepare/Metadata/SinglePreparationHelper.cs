using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Errors;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;
using System;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public void PostPrepareStatementUpToCurrentStep(AstStatement stmt)
        {
            if (_currentPreparationStep == PreparationStep.None)
            {
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, stmt, [], ErrorCode.Get(CTEN.StmtInvalidPreparation));
                return;
            }

            // setting up anime shite
            var cachedSourceFile = _currentSourceFile;
            var cachedCurrentClass = _currentClass;
            _currentSourceFile = stmt.SourceFile;
            if (stmt is AstClassDecl cls)
                _currentClass = cls;

            // go all over the steps down
            if (_currentPreparationStep >= PreparationStep.Types)
            {
                PostPrepareMetadataTypes(stmt, false);
            }
            if (_currentPreparationStep >= PreparationStep.Generics)
            {
                PostPrepareMetadataGenerics(stmt);
            }
        }
    }
}
