using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Errors;
using HapetFrontend.Extensions;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using HapetPostPrepare.Entities;
using System;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private void PostPrepareMetadataInheritance(AstStatement stmt)
        {
            // just handlers
            InInfo inInfo = InInfo.Default;
            OutInfo outInfo = OutInfo.Default;

            if (stmt is AstClassDecl cls)
            {
                foreach (var inh in cls.InheritedFrom)
                {
                    PostPrepareExprInference(inh, inInfo, ref outInfo);

                    // was not infered properly - probably errored somewhere before
                    if (inh.OutType == null)
                        continue;

                    if (inh.OutType is not ClassType)
                    {
                        // error - cannot inherit from non class types
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, inh, [HapetType.AsString(cls.Type.OutType), HapetType.AsString(inh.OutType)], ErrorCode.Get(CTEN.CannotDeriveFromStruct));
                        continue;
                    }

                    // check for sealed type
                    if ((inh.OutType as ClassType).Declaration.SpecialKeys.Contains(TokenType.KwSealed))
                    {
                        // error - cannot inherit from sealed
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, inh, [], ErrorCode.Get(CTEN.DerivedFromSealed));
                    }
                }

                // set System.Object inheritance if there is nothing
                if ((cls.InheritedFrom.Count <= 0 ||
                    (cls.InheritedFrom[0].OutType is ClassType &&
                    (cls.InheritedFrom[0].OutType as ClassType).Declaration.IsInterface)) &&
                    cls.Name.Name != "System.Object") // skip itself
                {
                    // set it only if there are not inheritances or only interfaces
                    var nst = new AstNestedExpr(new AstIdExpr("System.Object", cls), null, cls);
                    cls.InheritedFrom.Insert(0, nst);
                    SetScopeAndParent(nst, cls);
                    PostPrepareExprScoping(nst);
                    PostPrepareExprInference(nst, inInfo, ref outInfo);
                }
            }
            else if (stmt is AstStructDecl str)
            {
                foreach (var inh in str.InheritedFrom)
                {
                    PostPrepareExprInference(inh, inInfo, ref outInfo);

                    // was not infered properly - probably errored somewhere before
                    if (inh.OutType == null)
                        continue;

                    if (inh.OutType is not ClassType ||
                        (!(inh.OutType as ClassType).Declaration.IsInterface &&
                        (inh.OutType as ClassType).Declaration.Name.Name != "System.ValueType"))
                    {
                        // error - cannot inherit from non interfaces
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, inh, [HapetType.AsString(inh.OutType)], ErrorCode.Get(CTEN.NonInterfaceInhInStruct));
                        continue;
                    }

                    // check for sealed type
                    if ((inh.OutType as ClassType).Declaration.SpecialKeys.Contains(TokenType.KwSealed))
                    {
                        // error - cannot inherit from sealed
                        _compiler.MessageHandler.ReportMessage(_currentSourceFile, inh, [], ErrorCode.Get(CTEN.DerivedFromSealed));
                    }
                }

                // set System.Object inheritance if there is nothing
                if ((str.InheritedFrom.Count <= 0 ||
                    (str.InheritedFrom[0].OutType is ClassType &&
                    (str.InheritedFrom[0].OutType as ClassType).Declaration.IsInterface)))
                {
                    // set it only if there are not inheritances or only interfaces
                    var nst = new AstNestedExpr(new AstIdExpr("System.ValueType", str), null, str);
                    str.InheritedFrom.Insert(0, nst);
                    SetScopeAndParent(nst, str);
                    PostPrepareExprScoping(nst);
                    PostPrepareExprInference(nst, inInfo, ref outInfo);
                }
            }
            else if (stmt is AstEnumDecl enm)
            {
                if (enm.InheritedType == null)
                    return;
                PostPrepareExprInference(enm.InheritedType, inInfo, ref outInfo);
                // only int type inheritance allowed for enums
                if (enm.InheritedType.OutType is not IntType)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile, enm.InheritedType, [], ErrorCode.Get(CTEN.EnumTypeNotInt));
                }
            }
        }
    }
}
