using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using HapetLsp.Entities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace HapetLsp.Colorizers
{
    internal class HapetColorizer
    {
        public List<SemanticToken> CurrentSemanticTokens { get; } = new List<SemanticToken>();

        private readonly SemanticTokenType[] _tokenTypes;
        private readonly SemanticTokenModifier[] _tokenModifiers;
        private readonly ProgramFile _programFile;
        private readonly Compiler _compiler;

        public HapetColorizer(ProgramFile file, Compiler compiler, SemanticTokenType[] tokenTypes, SemanticTokenModifier[] tokenModifiers) 
        {
            _tokenTypes = tokenTypes;
            _tokenModifiers = tokenModifiers;
            _programFile = file;
            _compiler = compiler;
        }

        public void Colorize()
        {
            ColorizeUsings(_programFile);
            ColorizeComments(_programFile);

            // making colorize of declarations
            foreach (var d in _programFile.Statements)
            {
                if (d is not AstDeclaration decl)
                    continue;
                ColorizeDeclaration(decl);
            }
        }

        private void ColorizeUsings(ProgramFile file)
        {
            foreach (var u in file.Usings)
            {
                AddSemanticToken(u.Location, _tokenTypes[1], _tokenModifiers[0]);
            }
        }

        private void ColorizeComments(ProgramFile file)
        {
            foreach (var c in file.CommentLocations)
            {
                AddSemanticToken(c, _tokenTypes[12], _tokenModifiers[0]);
            }
        }

        private void ColorizeSpecialKeys(AstDeclaration decl)
        {
            // colorize special keys
            foreach (var k in decl.SpecialKeys)
            {
                AddSemanticToken(k.Location, _tokenTypes[2], _tokenModifiers[0]);
            }
        }

        private void ColorizeDeclaration(AstDeclaration decl)
        {
            // skip synthetic
            if (decl.IsSyntheticStatement)
                return;

            // colorize special keys
            ColorizeSpecialKeys(decl);

            // colorize attirbutes
            foreach (var c in decl.Attributes)
                ColorizeAttributeStmt(c);

            switch (decl)
            {
                case AstClassDecl clsD:
                    ColorizeClassDecl(clsD);
                    break;
                case AstStructDecl strD:
                    ColorizeStructDecl(strD);
                    break;
                case AstEnumDecl enmD:
                    ColorizeEnumDecl(enmD);
                    break;
                case AstDelegateDecl delD:
                    ColorizeDelegateDecl(delD);
                    break;
                case AstFuncDecl funcD:
                    ColorizeFuncDecl(funcD);
                    break;
                case AstPropertyDecl propD:
                    ColorizePropertyDecl(propD);
                    break;
                case AstVarDecl varD:
                    ColorizeVarDecl(varD);
                    break;
            }
        }

        private void ColorizeClassDecl(AstClassDecl decl)
        {
            // class token
            AddSemanticToken(decl.Location.Beginning, _tokenTypes[2], _tokenModifiers[0]);
            // class name
            ColorizeExpr(decl.Name);

            foreach (var i in decl.InheritedFrom)
            {
                // skip synthetic
                if (i.IsSyntheticStatement)
                    continue;
                ColorizeExpr(i);
            }

            foreach (var d in decl.Declarations)
            {
                ColorizeDeclaration(d);
            }
        }

        private void ColorizeStructDecl(AstStructDecl decl)
        {
            // struct token
            AddSemanticToken(decl.Location.Beginning, _tokenTypes[2], _tokenModifiers[0]);
            // struct name
            ColorizeExpr(decl.Name);

            foreach (var i in decl.InheritedFrom)
            {
                // skip synthetic
                if (i.IsSyntheticStatement)
                    continue;
                ColorizeExpr(i);
            }

            foreach (var d in decl.Declarations)
            {
                ColorizeDeclaration(d);
            }
        }

        private void ColorizeEnumDecl(AstEnumDecl decl)
        {
            // enum token
            AddSemanticToken(decl.Location.Beginning, _tokenTypes[2], _tokenModifiers[0]);
            // enum name
            ColorizeExpr(decl.Name);

            // skip synthetic
            if (!decl.InheritedType.IsSyntheticStatement)
                ColorizeExpr(decl.InheritedType);

            foreach (var d in decl.Declarations)
            {
                ColorizeDeclaration(d);
            }
        }

        private void ColorizeDelegateDecl(AstDelegateDecl decl)
        {
            // enum token
            AddSemanticToken(decl.Location.Beginning, _tokenTypes[2], _tokenModifiers[0]);
            ColorizeExpr(decl.Returns);
            // func name
            ColorizeExpr(decl.Name);

            // params
            foreach (var p in decl.Parameters)
            {
                if (p.IsSyntheticStatement)
                    continue;
                ColorizeParamDecl(p);
            }
        }

        private void ColorizeFuncDecl(AstFuncDecl decl)
        {
            if (!decl.Returns.IsSyntheticStatement)
                ColorizeExpr(decl.Returns);
            // func name
            ColorizeExpr(decl.Name);

            // params
            foreach (var p in decl.Parameters)
            {
                if (p.IsSyntheticStatement)
                    continue;
                ColorizeParamDecl(p);
            }

            if (decl.Body != null)
                ColorizeExpr(decl.Body);
        }

        private void ColorizeVarDecl(AstVarDecl decl)
        {
            ColorizeExpr(decl.Type);
            ColorizeExpr(decl.Name);

            if (decl.Initializer != null)
                ColorizeExpr(decl.Initializer);
        }

        private void ColorizePropertyDecl(AstPropertyDecl decl)
        {
            ColorizeExpr(decl.Type);
            ColorizeExpr(decl.Name);

            if (decl.Initializer != null)
                ColorizeExpr(decl.Initializer);

            if (decl is AstIndexerDecl indD)
            {
                ColorizeParamDecl(indD.IndexerParameter);
            }

            if (decl.GetTokenPosition != null)
                AddSemanticToken(decl.GetTokenPosition, _tokenTypes[2], _tokenModifiers[0]);
            if (decl.SetTokenPosition != null)
                AddSemanticToken(decl.SetTokenPosition, _tokenTypes[2], _tokenModifiers[0]);

            if (decl.GetBlock != null)
                ColorizeBlockExpr(decl.GetBlock);
            if (decl.SetBlock != null)
                ColorizeBlockExpr(decl.SetBlock);
        }

        private void ColorizeParamDecl(AstParamDecl decl)
        {
            ColorizeExpr(decl.Type);
            // param name
            AddSemanticToken(decl.Name.Location, _tokenTypes[6], _tokenModifiers[0]);
        }

        public void ColorizeExpr(AstStatement expr)
        {
            switch (expr)
            {
                // special case at least for 'for' loop
                // when 'for (int i = 0;...)' where 'int i' 
                // would not be handled by blockExpr
                case AstVarDecl varDecl:
                    ColorizeVarDecl(varDecl);
                    break;
                case AstBlockExpr blockExpr:
                    ColorizeBlockExpr(blockExpr);
                    break;
                case AstUnaryExpr unExpr:
                    ColorizeUnaryExpr(unExpr);
                    break;
                case AstBinaryExpr binExpr:
                    ColorizeBinaryExpr(binExpr);
                    break;
                case AstPointerExpr pointerExpr:
                    ColorizePointerExpr(pointerExpr);
                    break;
                case AstAddressOfExpr addrExpr:
                    ColorizeAddressOfExpr(addrExpr);
                    break;
                case AstNewExpr newExpr:
                    ColorizeNewExpr(newExpr);
                    break;
                case AstArgumentExpr argumentExpr:
                    ColorizeArgumentExpr(argumentExpr);
                    break;
                case AstIdGenericExpr genExpr:
                    ColorizeIdGenericExpr(genExpr);
                    break;
                case AstIdExpr idExpr:
                    ColorizeIdExpr(idExpr);
                    break;
                case AstCallExpr callExpr:
                    ColorizeCallExpr(callExpr);
                    break;
                case AstCastExpr castExpr:
                    ColorizeCastExpr(castExpr);
                    break;
                case AstNestedExpr nestExpr:
                    ColorizeNestedExpr(nestExpr);
                    break;
                case AstDefaultExpr defaultExpr:
                    ColorizeDefaultExpr(defaultExpr);
                    break;
                case AstDefaultGenericExpr defaultExpr:
                    ColorizeDefaultGenericExpr(defaultExpr);
                    break;
                case AstEmptyStructExpr _: // no need
                    break;
                case AstArrayExpr arrayExpr:
                    ColorizeArrayExpr(arrayExpr);
                    break;
                case AstArrayCreateExpr arrayCreateExpr:
                    ColorizeArrayCreateExpr(arrayCreateExpr);
                    break;
                case AstArrayAccessExpr arrayAccExpr:
                    ColorizeArrayAccessExpr(arrayAccExpr);
                    break;
                case AstTernaryExpr ternaryExpr:
                    ColorizeTernaryExpr(ternaryExpr);
                    break;
                case AstCheckedExpr checkedExpr:
                    ColorizeCheckedExpr(checkedExpr);
                    break;
                case AstSATOfExpr satExpr:
                    ColorizeSATExpr(satExpr);
                    break;
                case AstEmptyExpr:
                    break;
                case AstLambdaExpr lambdaExpr:
                    ColorizeLambdaExpr(lambdaExpr);
                    break;
                case AstNullableExpr nullableExpr:
                    ColorizeNullableExpr(nullableExpr);
                    break;

                // statements
                case AstAssignStmt assignStmt:
                    ColorizeAssignStmt(assignStmt);
                    break;
                case AstForStmt forStmt:
                    ColorizeForStmt(forStmt);
                    break;
                case AstWhileStmt whileStmt:
                    ColorizeWhileStmt(whileStmt);
                    break;
                case AstDoWhileStmt doWhileStmt:
                    ColorizeDoWhileStmt(doWhileStmt);
                    break;
                case AstIfStmt ifStmt:
                    ColorizeIfStmt(ifStmt);
                    break;
                case AstSwitchStmt switchStmt:
                    ColorizeSwitchStmt(switchStmt);
                    break;
                case AstCaseStmt caseStmt:
                    ColorizeCaseStmt(caseStmt);
                    break;
                case AstBreakContStmt breakContStmt:
                    ColorizeBreakContStmt(breakContStmt);
                    break;
                case AstReturnStmt returnStmt:
                    ColorizeReturnStmt(returnStmt);
                    break;
                case AstAttributeStmt attrStmt:
                    ColorizeAttributeStmt(attrStmt);
                    break;
                case AstBaseCtorStmt baseStmt:
                    ColorizeBaseCtorStmt(baseStmt);
                    break;
                case AstConstrainStmt constrainStmt:
                    ColorizeConstrainStmt(constrainStmt);
                    break;
                case AstThrowStmt throwStmt:
                    ColorizeThrowStmt(throwStmt);
                    break;
                case AstTryCatchStmt tryCatchStmt:
                    ColorizeTryCatchStmt(tryCatchStmt);
                    break;
                case AstCatchStmt сatchStmt:
                    ColorizeCatchStmt(сatchStmt);
                    break;
                case AstGotoStmt gotoStmt:
                    ColorizeGotoStmt(gotoStmt);
                    break;

                // skip literals
                case AstNumberExpr numberExpr:
                    AddSemanticToken(numberExpr.Location, _tokenTypes[9], _tokenModifiers[0]);
                    break;
                case AstStringExpr stringExpr:
                    AddSemanticToken(stringExpr.Location, _tokenTypes[10], _tokenModifiers[0]);
                    break;
                case AstBoolExpr boolExpr:
                    AddSemanticToken(boolExpr.Location, _tokenTypes[2], _tokenModifiers[0]);
                    break;
                case AstCharExpr charExpr:
                    AddSemanticToken(charExpr.Location, _tokenTypes[11], _tokenModifiers[0]);
                    break;
                case AstNullExpr nullExpr:
                    AddSemanticToken(nullExpr.Location, _tokenTypes[2], _tokenModifiers[0]);
                    break;

                default:
                    {
                        _compiler.MessageHandler.ReportMessage(_programFile, expr, [expr.AAAName], ErrorCode.Get(CTEN.StmtNotImplemented));
                        break;
                    }
            }
        }

        private void ColorizeBlockExpr(AstBlockExpr expr)
        {
            foreach (var s in expr.Statements)
            {
                if (s is AstFuncDecl fncD)
                {
                    ColorizeDeclaration(fncD);
                    continue;
                }
                ColorizeExpr(s);
            }
        }

        private void ColorizeUnaryExpr(AstUnaryExpr expr)
        {
            ColorizeExpr(expr.SubExpr);
        }

        private void ColorizeBinaryExpr(AstBinaryExpr expr)
        {
            if (!expr.Left.IsSyntheticStatement)
                ColorizeExpr(expr.Left);
            if (!expr.Right.IsSyntheticStatement)
                ColorizeExpr(expr.Right);

            // special cases for 'as', 'is', 'in'
            if (expr.Operator == "is" ||  expr.Operator == "as" || expr.Operator == "in")
            {
                AddSemanticToken(expr.OperatorTokenLocation, _tokenTypes[2], _tokenModifiers[0]);
            }
        }

        private void ColorizePointerExpr(AstPointerExpr expr)
        {
            ColorizeExpr(expr.SubExpression);
        }

        private void ColorizeAddressOfExpr(AstAddressOfExpr expr)
        {
            ColorizeExpr(expr.SubExpression);
        }

        private void ColorizeNewExpr(AstNewExpr expr)
        {
            // colorize 'new' word
            AddSemanticToken(expr.Location.Beginning, _tokenTypes[2], _tokenModifiers[0]);
            // colorize type
            ColorizeExpr(expr.TypeName);

            // colorize args
            foreach (var a in expr.Arguments)
                ColorizeArgumentExpr(a);
        }

        private void ColorizeArgumentExpr(AstArgumentExpr expr)
        {
            ColorizeExpr(expr.Expr);
        }

        private void ColorizeIdGenericExpr(AstIdGenericExpr expr)
        {
            ColorizeIdExpr(expr);

            // go over generic types
            foreach (var g in expr.GenericRealTypes)
                ColorizeExpr(g);
        }

        private void ColorizeIdExpr(AstIdExpr expr)
        {
            // skip synthetic
            if (expr.IsSyntheticStatement)
                return;
            if (expr.FindSymbol is not DeclSymbol declSymbol)
                return;

            // special case for 'this' and 'indexer__'
            if (expr.Name == "this" || expr.Name == "indexer__")
            {
                AddSemanticToken(expr.Location, _tokenTypes[2], _tokenModifiers[0]);
                return;
            }

            // colorize additional data
            if (expr.AdditionalData != null)
            {
                ColorizeExpr(expr.AdditionalData.RightPart);
            }

            if (declSymbol.Decl is AstParamDecl)
            {
                // param name
                AddSemanticToken(expr.Location, _tokenTypes[6], _tokenModifiers[0]);
            }
            else if (declSymbol.Decl is AstVarDecl vD)
            {
                switch (vD.ContainingParent)
                {
                    case AstClassDecl:
                    case AstStructDecl:
                    case AstEnumDecl:
                        // no need to colorize props and fields
                        break;
                    default:
                        AddSemanticToken(expr.Location, _tokenTypes[6], _tokenModifiers[0]);
                        break;
                }
            }
            else if (declSymbol.Decl is AstFuncDecl)
            {
                // func name colorizing
                AddSemanticToken(expr.Location, _tokenTypes[5], _tokenModifiers[0]);
            }
            else
            {
                // static/decl colorizing
                if (expr.OutType is ClassType clsTT)
                    AddSemanticToken(expr.Location, clsTT.Declaration.IsInterface ? _tokenTypes[3] : _tokenTypes[0], _tokenModifiers[0]);
                else
                    AddSemanticToken(expr.Location, _tokenTypes[4], _tokenModifiers[0]);
            }
        }

        private void ColorizeCallExpr(AstCallExpr expr)
        {
            if (expr.IsSyntheticStatement)
                return;

            if (expr.TypeOrObjectName != null)
                ColorizeExpr(expr.TypeOrObjectName);

            ColorizeExpr(expr.FuncName);

            // args
            foreach (var a in expr.Arguments)
                ColorizeArgumentExpr(a);
        }

        private void ColorizeCastExpr(AstCastExpr expr)
        {
            ColorizeExpr(expr.TypeExpr);
            ColorizeExpr(expr.SubExpression);
        }

        private void ColorizeNestedExpr(AstNestedExpr expr)
        {
            if (expr.LeftPart != null)
                ColorizeExpr(expr.LeftPart);
            ColorizeExpr(expr.RightPart);
        }

        private void ColorizeDefaultExpr(AstDefaultExpr expr)
        {
            // skip synthetic
            if (expr.IsSyntheticStatement)
                return;
            // colorize 'default' word
            AddSemanticToken(expr.Location.Beginning, _tokenTypes[2], _tokenModifiers[0]);
            if (expr.TypeForDefault != null)
                ColorizeExpr(expr.TypeForDefault);
        }

        private void ColorizeDefaultGenericExpr(AstDefaultGenericExpr expr)
        {
            // skip synthetic
            if (expr.IsSyntheticStatement)
                return;
            // colorize 'default' word
            AddSemanticToken(expr.Location.Beginning, _tokenTypes[2], _tokenModifiers[0]);
        }

        private void ColorizeArrayExpr(AstArrayExpr expr)
        {
            ColorizeExpr(expr.SubExpression);
        }

        private void ColorizeArrayCreateExpr(AstArrayCreateExpr expr)
        {
            // colorize 'new' word
            AddSemanticToken(expr.Location.Beginning, _tokenTypes[2], _tokenModifiers[0]);

            ColorizeExpr(expr.TypeName);
            foreach (var s in expr.SizeExprs)
            {
                if (s.IsSyntheticStatement)
                    continue;
                ColorizeExpr(s);
            }
            foreach (var e in expr.Elements)
                ColorizeExpr(e);
        }

        private void ColorizeArrayAccessExpr(AstArrayAccessExpr expr)
        {
            ColorizeExpr(expr.ObjectName);
            ColorizeExpr(expr.ParameterExpr);
        }

        private void ColorizeTernaryExpr(AstTernaryExpr expr)
        {
            if (!expr.Condition.IsSyntheticStatement)
                ColorizeExpr(expr.Condition);
            if (!expr.TrueExpr.IsSyntheticStatement)
                ColorizeExpr(expr.TrueExpr);
            if (!expr.FalseExpr.IsSyntheticStatement)
                ColorizeExpr(expr.FalseExpr);
        }

        private void ColorizeCheckedExpr(AstCheckedExpr expr)
        {
            // colorize 'checked' word
            AddSemanticToken(expr.Location.Beginning, _tokenTypes[2], _tokenModifiers[0]);

            ColorizeExpr(expr.SubExpression);
        }
        
        private void ColorizeSATExpr(AstSATOfExpr expr)
        {
            // colorize 'sat' word
            AddSemanticToken(expr.Location.Beginning, _tokenTypes[2], _tokenModifiers[0]);

            ColorizeExpr(expr.TargetType);
        }

        private void ColorizeLambdaExpr(AstLambdaExpr expr)
        {
            foreach (var p in expr.Parameters)
                ColorizeParamDecl(p);
            ColorizeBlockExpr(expr.Body);
        }

        private void ColorizeNullableExpr(AstNullableExpr expr)
        {
            ColorizeExpr(expr.SubExpression);
        }


        private void ColorizeAssignStmt(AstAssignStmt stmt)
        {
            ColorizeExpr(stmt.Target);
            ColorizeExpr(stmt.Value);
        }

        private void ColorizeForStmt(AstForStmt stmt)
        {
            // colorize 'for' word
            AddSemanticToken(stmt.Location.Beginning, _tokenTypes[8], _tokenModifiers[0]);

            ColorizeExpr(stmt.FirstArgument);
            ColorizeExpr(stmt.SecondArgument);
            ColorizeExpr(stmt.ThirdArgument);
            ColorizeBlockExpr(stmt.Body);
        }

        private void ColorizeWhileStmt(AstWhileStmt stmt)
        {
            // colorize 'while' word
            AddSemanticToken(stmt.Location.Beginning, _tokenTypes[8], _tokenModifiers[0]);

            ColorizeExpr(stmt.Condition);
            ColorizeBlockExpr(stmt.Body);
        }

        private void ColorizeDoWhileStmt(AstDoWhileStmt stmt)
        {
            // colorize 'do' word
            AddSemanticToken(stmt.Location.Beginning, _tokenTypes[8], _tokenModifiers[0]);
            // colorize 'while' word
            AddSemanticToken(stmt.WhileTokenLocation, _tokenTypes[8], _tokenModifiers[0]);

            ColorizeExpr(stmt.Condition);
            ColorizeBlockExpr(stmt.Body);
        }

        private void ColorizeIfStmt(AstIfStmt stmt)
        {
            // colorize 'if' word
            AddSemanticToken(stmt.Location.Beginning, _tokenTypes[8], _tokenModifiers[0]);

            ColorizeExpr(stmt.Condition);
            ColorizeBlockExpr(stmt.BodyTrue);

            if (stmt.BodyFalse != null)
            {
                // colorize 'else' word
                AddSemanticToken(stmt.ElseTokenLocation, _tokenTypes[8], _tokenModifiers[0]);
                ColorizeBlockExpr(stmt.BodyFalse);
            }
        }

        private void ColorizeSwitchStmt(AstSwitchStmt stmt)
        {
            // colorize 'switch' word
            AddSemanticToken(stmt.Location.Beginning, _tokenTypes[8], _tokenModifiers[0]);

            ColorizeExpr(stmt.SubExpression);

            foreach (var c in stmt.Cases)
                ColorizeCaseStmt(c);
        }

        private void ColorizeCaseStmt(AstCaseStmt stmt)
        {
            // colorize 'case' word
            AddSemanticToken(stmt.Location.Beginning, _tokenTypes[8], _tokenModifiers[0]);

            if (stmt.Pattern != null)
                ColorizeExpr(stmt.Pattern);

            ColorizeBlockExpr(stmt.Body);
        }

        private void ColorizeBreakContStmt(AstBreakContStmt stmt)
        {
            // colorize 'break/continue' word
            AddSemanticToken(stmt.Location, _tokenTypes[8], _tokenModifiers[0]);
        }

        private void ColorizeReturnStmt(AstReturnStmt stmt)
        {
            // colorize 'return' word
            AddSemanticToken(stmt.Location.Beginning, _tokenTypes[8], _tokenModifiers[0]);

            if (stmt.ReturnExpression != null)
                ColorizeExpr(stmt.ReturnExpression);
        }

        private void ColorizeAttributeStmt(AstAttributeStmt stmt)
        {
            if (stmt.IsSyntheticStatement)
                return;

            ColorizeExpr(stmt.AttributeName);

            foreach (var a in stmt.Arguments)
                ColorizeArgumentExpr(a);
        }

        private void ColorizeBaseCtorStmt(AstBaseCtorStmt stmt)
        {
            if (stmt.IsSyntheticStatement)
                return;

            // colorize 'base' word
            AddSemanticToken(stmt.Location.Beginning, _tokenTypes[2], _tokenModifiers[0]);

            foreach (var a in stmt.Arguments)
                ColorizeArgumentExpr(a);
        }

        private void ColorizeConstrainStmt(AstConstrainStmt stmt)
        {
            if (stmt.IsSyntheticStatement)
                return;

            // TODO:
        }

        private void ColorizeThrowStmt(AstThrowStmt stmt)
        {
            // colorize 'throw' word
            AddSemanticToken(stmt.Location.Beginning, _tokenTypes[8], _tokenModifiers[0]);

            if (stmt.ThrowExpression != null)
                ColorizeExpr(stmt.ThrowExpression);
        }

        private void ColorizeTryCatchStmt(AstTryCatchStmt stmt)
        {
            // colorize 'try' word
            AddSemanticToken(stmt.Location.Beginning, _tokenTypes[8], _tokenModifiers[0]);
            ColorizeBlockExpr(stmt.TryBlock);

            foreach (var c in stmt.CatchBlocks)
                ColorizeCatchStmt(c);

            if (stmt.FinallyBlock != null)
            {
                // colorize 'finally' word
                AddSemanticToken(stmt.FinallyTokenLocation, _tokenTypes[8], _tokenModifiers[0]);
                ColorizeBlockExpr(stmt.FinallyBlock);
            }
        }

        private void ColorizeCatchStmt(AstCatchStmt stmt)
        {
            // colorize 'catch' word
            AddSemanticToken(stmt.Location.Beginning, _tokenTypes[8], _tokenModifiers[0]);
            ColorizeBlockExpr(stmt.CatchBlock);

            if (stmt.CatchParam != null)
                ColorizeParamDecl(stmt.CatchParam);
        }

        private void ColorizeGotoStmt(AstGotoStmt stmt)
        {
            // colorize 'goto' word
            AddSemanticToken(stmt.Location.Beginning, _tokenTypes[8], _tokenModifiers[0]);
        }

        private void AddSemanticToken(ILocation location, SemanticTokenType type, SemanticTokenModifier modifier)
        {
            CurrentSemanticTokens.Add(new SemanticToken(location.Beginning.Line - 1, location.Beginning.Column - 1,
                    location.Ending.End - location.Beginning.Index, type, modifier));
        }
    }
}
