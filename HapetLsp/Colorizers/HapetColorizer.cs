using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
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

        public HapetColorizer(ProgramFile file, SemanticTokenType[] tokenTypes, SemanticTokenModifier[] tokenModifiers) 
        {
            _tokenTypes = tokenTypes;
            _tokenModifiers = tokenModifiers;
            _programFile = file;
        }

        public void Colorize()
        {
            ColorizeUsings(_programFile);

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
        }

        private void ColorizeVarDecl(AstVarDecl decl)
        {
            ColorizeExpr(decl.Type);
        }

        private void ColorizePropertyDecl(AstPropertyDecl decl)
        {
            ColorizeExpr(decl.Type);

            if (decl is AstIndexerDecl indD)
            {
                ColorizeParamDecl(indD.IndexerParameter);
            }
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
                case AstDefaultGenericExpr _: // no need to scope anything
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
            }
        }

        private void ColorizeBlockExpr(AstBlockExpr expr)
        {
            foreach (var s in expr.Statements)
            {
                ColorizeExpr(s);
            }
        }

        private void ColorizeUnaryExpr(AstUnaryExpr expr)
        {
            ColorizeExpr(expr.SubExpr);
        }

        private void ColorizeBinaryExpr(AstBinaryExpr expr)
        {
            ColorizeExpr(expr.Left);
            ColorizeExpr(expr.Right);
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

            if (declSymbol.Decl is AstParamDecl parD)
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
                ColorizeExpr(s);
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
            ColorizeExpr(expr.Condition);
            ColorizeExpr(expr.TrueExpr);
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

        private void AddSemanticToken(ILocation location, SemanticTokenType type, SemanticTokenModifier modifier)
        {
            CurrentSemanticTokens.Add(new SemanticToken(location.Beginning.Line - 1, location.Beginning.Column - 1,
                    location.Ending.End - location.Beginning.Index, type, modifier));
        }
    }
}
