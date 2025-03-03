using HapetFrontend.Ast;
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
                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, stmt, [], ErrorCode.Get(CTEN.FieldAlreadyDefined));
                return;
            }

            if (_currentPreparationStep >= PreparationStep.Types)
        }
    }
}
