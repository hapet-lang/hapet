using HapetFrontend.Ast;
using HapetFrontend.Errors;
using HapetFrontend.Parsing;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public void CheckSpecialKeys(AstDeclaration decl)
        {
            // not my problem
            if (decl == null)
                return;

            TokenType syncKey = default;          // async
            TokenType accessKey = default;        // public/protected/internal/private/unreflected
            TokenType shadowKey = default;        // new
            TokenType instanceKey = default;      // static
            TokenType mutabilityKey = default;    // readonly/const
            TokenType abstractionKey = default;   // abstract/virtual/override
            TokenType otherKey = default;         // partial/extern/sealed/inline/noexcept/imported

            for (int i = 0; i < decl.SpecialKeys.Count; ++i)
            {
                var currKey = decl.SpecialKeys[i];
                var keyType = GetSpecialKeyType(currKey.Type);
                switch (keyType)
                {
                    case 0:
                        {
                            TokenType[] asArr = [accessKey, shadowKey, instanceKey, mutabilityKey, abstractionKey, otherKey];
                            Handler(asArr, currKey, ref syncKey);
                            break;
                        }
                    case 1:
                        {
                            TokenType[] asArr = [shadowKey, instanceKey, mutabilityKey, abstractionKey, otherKey];
                            Handler(asArr, currKey, ref accessKey);
                            break;
                        }
                    case 2:
                        {
                            TokenType[] asArr = [instanceKey, mutabilityKey, abstractionKey, otherKey];
                            Handler(asArr, currKey, ref shadowKey);
                            break;
                        }
                    case 3:
                        {
                            TokenType[] asArr = [mutabilityKey, abstractionKey, otherKey];
                            Handler(asArr, currKey, ref instanceKey);
                            break;
                        }
                    case 4:
                        {
                            TokenType[] asArr = [abstractionKey, otherKey];
                            Handler(asArr, currKey, ref mutabilityKey);
                            break;
                        }
                    case 5:
                        {
                            TokenType[] asArr = [otherKey];
                            Handler(asArr, currKey, ref abstractionKey);
                            break;
                        }
                    case 6:
                        {
                            TokenType[] asArr = [];
                            Handler(asArr, currKey, ref otherKey);
                            break;
                        }
                }
            }

            void Handler(TokenType[] forw, Token curr, ref TokenType check)
            {
                var nonDefault = forw.FirstOrDefault(x => x != default);
                if (nonDefault != default)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, curr.Location,
                        [Lexer.GetKeywordFromToken(curr.Type), Lexer.GetKeywordFromToken(nonDefault)],
                        ErrorCode.Get(CTEN.ShouldGoBeforeSpecialKey));
                    return;
                }

                if (check != default)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, curr.Location,
                        [Lexer.GetKeywordFromToken(curr.Type), Lexer.GetKeywordFromToken(check)],
                        ErrorCode.Get(CTEN.AlreadyDefinedSpecialKey));
                    return;
                }

                check = curr.Type;
            }
        }

        private void AddSpecialKeyToDecl(AstDeclaration decl, Token specialKey, bool doError = false)
        {
            TokenType syncKey = default;          // async
            TokenType accessKey = default;        // public/protected/internal/private/unreflected
            TokenType shadowKey = default;        // new
            TokenType instanceKey = default;      // static
            TokenType mutabilityKey = default;    // readonly/const
            TokenType abstractionKey = default;   // abstract/virtual/override
            TokenType otherKey = default;         // partial/extern/sealed/inline/noexcept/imported

            for (int i = 0; i < decl.SpecialKeys.Count; ++i)
            {
                var currKey = decl.SpecialKeys[i];
                var keyType = GetSpecialKeyType(currKey.Type);
                switch (keyType)
                {
                    case 0:
                        syncKey = currKey.Type;
                        break;
                    case 1:
                        accessKey = currKey.Type;
                        break;
                    case 2:
                        shadowKey = currKey.Type;
                        break;
                    case 3:
                        instanceKey = currKey.Type;
                        break;
                    case 4:
                        mutabilityKey = currKey.Type;
                        break;
                    case 5:
                        abstractionKey = currKey.Type;
                        break;
                    case 6:
                        otherKey = currKey.Type;
                        break;
                }
            }

            var keyTypeToAdd = GetSpecialKeyType(specialKey.Type);
            switch (keyTypeToAdd)
            {
                case 0:
                    {
                        if (syncKey != default)
                        {
                            if (doError)
                                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey.Type), Lexer.GetKeywordFromToken(syncKey)],
                                    ErrorCode.Get(CTEN.AlreadyDefinedSpecialKey));
                            break;
                        }

                        decl.SpecialKeys.Insert(0, specialKey);
                        break;
                    }
                case 1:
                    {
                        if (accessKey != default)
                        {
                            if (doError)
                                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey.Type), Lexer.GetKeywordFromToken(accessKey)],
                                    ErrorCode.Get(CTEN.AlreadyDefinedSpecialKey));
                            break;
                        }

                        int index = 0;
                        if (syncKey != default)
                            index++;
                        decl.SpecialKeys.Insert(index, specialKey);
                        break;
                    }
                case 2:
                    {
                        if (shadowKey != default)
                        {
                            if (doError)
                                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey.Type), Lexer.GetKeywordFromToken(shadowKey)],
                                    ErrorCode.Get(CTEN.AlreadyDefinedSpecialKey));
                            break;
                        }

                        int index = 0;
                        if (syncKey != default)
                            index++;
                        if (accessKey != default)
                            index++;
                        decl.SpecialKeys.Insert(index, specialKey);
                        break;
                    }
                case 3:
                    {
                        if (instanceKey != default)
                        {
                            if (doError)
                                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey.Type), Lexer.GetKeywordFromToken(instanceKey)],
                                    ErrorCode.Get(CTEN.AlreadyDefinedSpecialKey));
                            break;
                        }

                        int index = 0;
                        if (syncKey != default)
                            index++;
                        if (accessKey != default)
                            index++;
                        if (shadowKey != default)
                            index++;
                        decl.SpecialKeys.Insert(index, specialKey);
                        break;
                    }
                case 4:
                    {
                        if (mutabilityKey != default)
                        {
                            if (doError)
                                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey.Type), Lexer.GetKeywordFromToken(mutabilityKey)],
                                    ErrorCode.Get(CTEN.AlreadyDefinedSpecialKey));
                            break;
                        }

                        int index = 0;
                        if (syncKey != default)
                            index++;
                        if (accessKey != default)
                            index++;
                        if (shadowKey != default)
                            index++;
                        if (instanceKey != default)
                            index++;
                        decl.SpecialKeys.Insert(index, specialKey);
                        break;
                    }
                case 5:
                    {
                        if (abstractionKey != default)
                        {
                            if (doError)
                                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey.Type), Lexer.GetKeywordFromToken(abstractionKey)],
                                    ErrorCode.Get(CTEN.AlreadyDefinedSpecialKey));
                            break;
                        }

                        int index = 0;
                        if (syncKey != default)
                            index++;
                        if (accessKey != default)
                            index++;
                        if (shadowKey != default)
                            index++;
                        if (instanceKey != default)
                            index++;
                        if (mutabilityKey != default)
                            index++;
                        decl.SpecialKeys.Insert(index, specialKey);
                        break;
                    }
                case 6:
                    {
                        if (otherKey != default)
                        {
                            if (doError)
                                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey.Type), Lexer.GetKeywordFromToken(otherKey)],
                                    ErrorCode.Get(CTEN.AlreadyDefinedSpecialKey));
                            break;
                        }

                        int index = 0;
                        if (syncKey != default)
                            index++;
                        if (accessKey != default)
                            index++;
                        if (shadowKey != default)
                            index++;
                        if (instanceKey != default)
                            index++;
                        if (mutabilityKey != default)
                            index++;
                        if (abstractionKey != default)
                            index++;
                        decl.SpecialKeys.Insert(index, specialKey);
                        break;
                    }
            }
        }

        private int GetSpecialKeyType(TokenType specialKey)
        {
            switch (specialKey)
            {
                case TokenType.KwAsync:
                    {
                        return 0;
                    }
                case TokenType.KwPublic:
                case TokenType.KwProtected:
                case TokenType.KwInternal:
                case TokenType.KwPrivate:
                case TokenType.KwUnreflected:
                    {
                        return 1;
                    }
                case TokenType.KwNew:
                    {
                        return 2;
                    }
                case TokenType.KwStatic:
                    {
                        return 3;
                    }
                case TokenType.KwReadonly:
                case TokenType.KwConst:
                    {
                        return 4;
                    }
                case TokenType.KwAbstract:
                case TokenType.KwVirtual:
                case TokenType.KwOverride:
                    {
                        return 5;
                    }
                case TokenType.KwPartial:
                case TokenType.KwExtern:
                case TokenType.KwSealed:
                case TokenType.KwInline:
                case TokenType.KwNoexcept:
                    {
                        return 6;
                    }
            }
            return -1;
        }

        private bool HasSpecialKeyType(AstDeclaration decl, int type)
        {
            for (int i = 0; i < decl.SpecialKeys.Count; ++i)
            {
                var currKey = decl.SpecialKeys[i];
                var keyType = GetSpecialKeyType(currKey.Type);
                if (keyType == type)
                    return true;
            }
            return false;
        }
    }
}
