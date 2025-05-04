using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareMetadataTypeFieldInits(AstStatement stmt)
        {
            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            if (stmt is AstClassDecl cls)
            {
                // infer fields and props at first
                foreach (var decl in cls.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
                {
                    // do not infer generic props - it is for stage 12
                    if (decl is AstPropertyDecl && decl.HasGenericTypes)
                        continue;

                    // this kostyl is done to skip double error on uninferred type
                    var savedIsPropF = decl.IsPropertyField;
                    decl.IsPropertyField = true;

                    // field or property
                    inInfo.AllowSpecialKeys = true;
                    PostPrepareVarInference(decl, inInfo, ref outInfo);
                    inInfo.AllowSpecialKeys = false;

                    decl.IsPropertyField = savedIsPropF;
                }
            }
            else if (stmt is AstStructDecl str)
            {
                // infer fields at first
                foreach (var decl in str.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
                {
                    // do not infer generic props - it is for stage 12
                    if (decl is AstPropertyDecl && decl.HasGenericTypes)
                        continue;

                    // this kostyl is done to skip double error on uninferred type
                    var savedIsPropF = decl.IsPropertyField;
                    decl.IsPropertyField = true;

                    // field or property
                    inInfo.AllowSpecialKeys = true;
                    PostPrepareVarInference(decl, inInfo, ref outInfo);
                    inInfo.AllowSpecialKeys = false;

                    decl.IsPropertyField = savedIsPropF;
                }
            }
            else if (stmt is AstEnumDecl enm)
            {
                // generating all the values of fields
                int currentValue = 0;
                List<int> allValues = new List<int>(enm.Declarations.Count);

                // infer fields at first
                foreach (var decl in enm.Declarations)
                {
                    // field 
                    PostPrepareVarInference(decl, inInfo, ref outInfo);
                    // this shite is to generate values for enum fields
                    if (decl.Initializer == null)
                    {
                        decl.Initializer = PostPrepareExpressionWithType(GetPreparedAst(decl.Type.OutType, decl.Type), new AstNumberExpr(NumberData.FromInt(currentValue)));
                        // warn if the value already exists in enum
                        if (allValues.Contains(currentValue))
                        {
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl, [], ErrorCode.Get(CTWN.EnumHasSameValue), null, ReportType.Warning);
                        }
                        allValues.Add(currentValue);
                        currentValue++;
                    }
                    else
                    {
                        if (decl.Initializer.OutValue == null)
                        {
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Initializer, [], ErrorCode.Get(CTEN.EnumIniNotComptime));
                            continue;
                        }
                        else if (decl.Initializer.OutValue is not NumberData)
                        {
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Initializer, [], ErrorCode.Get(CTEN.EnumIniNotNumber));
                            continue;
                        }
                        var userDefinedValue = (int)((NumberData)decl.Initializer.OutValue).IntValue;
                        // warn if the value already exists in enum
                        if (allValues.Contains(userDefinedValue))
                        {
                            _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl, [], ErrorCode.Get(CTWN.EnumHasSameValue), null, ReportType.Warning);
                        }
                        allValues.Add(userDefinedValue);
                        currentValue = userDefinedValue + 1; // getting value for the next field
                    }
                }
            }
        }
    }
}
