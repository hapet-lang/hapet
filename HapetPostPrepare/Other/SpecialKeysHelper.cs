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
            TokenType instanceKey = default;      // readonly/static/const
            TokenType abstractionKey = default;   // abstract/virtual/override
            TokenType otherKey = default;         // partial/extern/sealed/inline/noexcept/imported

            for (int i = 0; i < decl.SpecialKeys.Count; ++i)
            {
                var currKey = decl.SpecialKeys[i];
                var keyType = GetSpecialKeyType(currKey);
                switch (keyType)
                {
                    case 0:
                        {
                            TokenType[] asArr = [accessKey, instanceKey, abstractionKey, otherKey];
                            Handler(asArr, currKey, ref syncKey);
                            break;
                        }
                    case 1:
                        {
                            TokenType[] asArr = [instanceKey, abstractionKey, otherKey];
                            Handler(asArr, currKey, ref accessKey);
                            break;
                        }
                    case 2:
                        {
                            TokenType[] asArr = [abstractionKey, otherKey];
                            Handler(asArr, currKey, ref instanceKey);
                            break;
                        }
                    case 3:
                        {
                            TokenType[] asArr = [otherKey];
                            Handler(asArr, currKey, ref abstractionKey);
                            break;
                        }
                    case 4:
                        {
                            TokenType[] asArr = [];
                            Handler(asArr, currKey, ref otherKey);
                            break;
                        }
                }
            }

            void Handler(TokenType[] forw, TokenType curr, ref TokenType check)
            {
                var nonDefault = forw.FirstOrDefault(x => x != default);
                if (nonDefault != default)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name,
                        [Lexer.GetKeywordFromToken(curr), Lexer.GetKeywordFromToken(nonDefault)],
                        ErrorCode.Get(CTEN.ShouldGoBeforeSpecialKey));
                    return;
                }

                if (check != default)
                {
                    _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name,
                        [Lexer.GetKeywordFromToken(curr), Lexer.GetKeywordFromToken(check)],
                        ErrorCode.Get(CTEN.AlreadyDefinedSpecialKey));
                    return;
                }

                check = curr;
            }
        }

        private void AddSpecialKeyToDecl(AstDeclaration decl, TokenType specialKey, bool doError = false)
        {
            TokenType syncKey = default;          // async
            TokenType accessKey = default;        // public/protected/internal/private/unreflected
            TokenType instanceKey = default;      // readonly/static/const
            TokenType abstractionKey = default;   // abstract/virtual/override
            TokenType otherKey = default;         // partial/extern/sealed/inline/noexcept/imported

            for (int i = 0; i < decl.SpecialKeys.Count; ++i)
            {
                var currKey = decl.SpecialKeys[i];
                var keyType = GetSpecialKeyType(currKey);
                switch (keyType)
                {
                    case 0:
                        syncKey = currKey;
                        break;
                    case 1:
                        accessKey = currKey;
                        break;
                    case 2:
                        instanceKey = currKey;
                        break;
                    case 3:
                        abstractionKey = currKey;
                        break;
                    case 4:
                        otherKey = currKey;
                        break;
                }
            }

            var keyTypeToAdd = GetSpecialKeyType(specialKey);
            switch (keyTypeToAdd)
            {
                case 0:
                    {
                        if (syncKey != default)
                        {
                            if (doError)
                                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey), Lexer.GetKeywordFromToken(syncKey)],
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
                                    [Lexer.GetKeywordFromToken(specialKey), Lexer.GetKeywordFromToken(accessKey)],
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
                        if (instanceKey != default)
                        {
                            if (doError)
                                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey), Lexer.GetKeywordFromToken(instanceKey)],
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
                        if (abstractionKey != default)
                        {
                            if (doError)
                                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey), Lexer.GetKeywordFromToken(abstractionKey)],
                                    ErrorCode.Get(CTEN.AlreadyDefinedSpecialKey));
                            break;
                        }

                        int index = 0;
                        if (syncKey != default)
                            index++;
                        if (accessKey != default)
                            index++;
                        if (instanceKey != default)
                            index++;
                        decl.SpecialKeys.Insert(index, specialKey);
                        break;
                    }
                case 4:
                    {
                        if (otherKey != default)
                        {
                            if (doError)
                                _compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey), Lexer.GetKeywordFromToken(otherKey)],
                                    ErrorCode.Get(CTEN.AlreadyDefinedSpecialKey));
                            break;
                        }

                        int index = 0;
                        if (syncKey != default)
                            index++;
                        if (accessKey != default)
                            index++;
                        if (instanceKey != default)
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
                case TokenType.KwReadonly:
                case TokenType.KwStatic:
                case TokenType.KwConst:
                    {
                        return 2;
                    }
                case TokenType.KwAbstract:
                case TokenType.KwVirtual:
                case TokenType.KwOverride:
                    {
                        return 3;
                    }
                case TokenType.KwPartial:
                case TokenType.KwExtern:
                case TokenType.KwSealed:
                case TokenType.KwInline:
                case TokenType.KwNoexcept:
                    {
                        return 4;
                    }
            }
            return -1;
        }
    }
}
