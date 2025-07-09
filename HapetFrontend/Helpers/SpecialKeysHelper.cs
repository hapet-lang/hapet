using HapetFrontend.Ast;
using HapetFrontend.Entities;
using HapetFrontend.Errors;
using HapetFrontend.Parsing;

namespace HapetFrontend.Helpers
{
    public static class SpecialKeysHelper
    {
        public static void CheckSpecialKeys(AstDeclaration decl, IMessageHandler messageHandler, ProgramFile sourceFile)
        {
            // not my problem
            if (decl == null)
                return;

            TokenType syncKey = default;          // async
            TokenType accessKey = default;        // public/protected/internal/private/unreflected
            TokenType instanceKey = default;      // abstract/static/sealed
            TokenType mutabilityKey = default;    // readonly/const
            TokenType shadowKey = default;        // new/virtual/override
            TokenType partialKey = default;       // partial
            TokenType externKey = default;        // extern
            TokenType inlineKey = default;        // inline
            TokenType noexceptKey = default;      // noexcept
            TokenType unsafeKey = default;        // unsafe

            for (int i = 0; i < decl.SpecialKeys.Count; ++i)
            {
                var currKey = decl.SpecialKeys[i];
                var keyType = GetSpecialKeyType(currKey.Type);
                switch (keyType)
                {
                    case 0:
                        {
                            TokenType[] asArr = [accessKey, instanceKey, mutabilityKey, shadowKey, partialKey, externKey, inlineKey, noexceptKey, unsafeKey];
                            Handler(asArr, currKey, ref syncKey);
                            break;
                        }
                    case 1:
                        {
                            TokenType[] asArr = [instanceKey, mutabilityKey, shadowKey, partialKey, externKey, inlineKey, noexceptKey, unsafeKey];
                            Handler(asArr, currKey, ref accessKey);
                            break;
                        }
                    case 2:
                        {
                            TokenType[] asArr = [mutabilityKey, shadowKey, partialKey, externKey, inlineKey, noexceptKey, unsafeKey];
                            Handler(asArr, currKey, ref instanceKey);
                            break;
                        }
                    case 3:
                        {
                            TokenType[] asArr = [shadowKey, partialKey, externKey, inlineKey, noexceptKey, unsafeKey];
                            Handler(asArr, currKey, ref mutabilityKey);
                            break;
                        }
                    case 4:
                        {
                            TokenType[] asArr = [partialKey, externKey, inlineKey, noexceptKey, unsafeKey];
                            Handler(asArr, currKey, ref shadowKey);
                            break;
                        }
                    case 5:
                        {
                            TokenType[] asArr = [externKey, inlineKey, noexceptKey, unsafeKey];
                            Handler(asArr, currKey, ref partialKey);
                            break;
                        }
                    case 6:
                        {
                            TokenType[] asArr = [inlineKey, noexceptKey, unsafeKey];
                            Handler(asArr, currKey, ref externKey);
                            break;
                        }
                    case 7:
                        {
                            TokenType[] asArr = [noexceptKey, unsafeKey];
                            Handler(asArr, currKey, ref inlineKey);
                            break;
                        }
                    case 8:
                        {
                            TokenType[] asArr = [unsafeKey];
                            Handler(asArr, currKey, ref noexceptKey);
                            break;
                        }
                    case 9:
                        {
                            TokenType[] asArr = [];
                            Handler(asArr, currKey, ref unsafeKey);
                            break;
                        }
                }
            }

            void Handler(TokenType[] forw, Token curr, ref TokenType check)
            {
                var nonDefault = forw.FirstOrDefault(x => x != default);
                if (nonDefault != default)
                {
                    messageHandler.ReportMessage(sourceFile.Text, curr.Location,
                        [Lexer.GetKeywordFromToken(curr.Type), Lexer.GetKeywordFromToken(nonDefault)],
                        ErrorCode.Get(CTEN.ShouldGoBeforeSpecialKey));
                    return;
                }

                if (check != default)
                {
                    messageHandler.ReportMessage(sourceFile.Text, curr.Location,
                        [Lexer.GetKeywordFromToken(curr.Type), Lexer.GetKeywordFromToken(check)],
                        ErrorCode.Get(CTEN.AlreadyDefinedSpecialKey));
                    return;
                }

                check = curr.Type;
            }
        }

        public static void AddSpecialKeyToDecl(AstDeclaration decl, Token specialKey, IMessageHandler messageHandler, ProgramFile sourceFile, bool doError = false)
        {
            TokenType syncKey = default;          // async
            TokenType accessKey = default;        // public/protected/internal/private/unreflected
            TokenType instanceKey = default;      // static/abstract/sealed
            TokenType mutabilityKey = default;    // readonly/const
            TokenType shadowKey = default;        // new/virtual/override
            TokenType partialKey = default;       // partial
            TokenType externKey = default;        // extern
            TokenType inlineKey = default;        // inline
            TokenType noexceptKey = default;      // noexcept
            TokenType unsafeKey = default;        // unsafe

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
                        instanceKey = currKey.Type;
                        break;
                    case 3:
                        mutabilityKey = currKey.Type;
                        break;
                    case 4:
                        shadowKey = currKey.Type;
                        break;
                    case 5:
                        partialKey = currKey.Type;
                        break;
                    case 6:
                        externKey = currKey.Type;
                        break;
                    case 7:
                        inlineKey = currKey.Type;
                        break;
                    case 8:
                        noexceptKey = currKey.Type;
                        break;
                    case 9:
                        unsafeKey = currKey.Type;
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
                                messageHandler.ReportMessage(sourceFile.Text, decl.Name,
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
                                messageHandler.ReportMessage(sourceFile.Text, decl.Name,
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
                        if (instanceKey != default)
                        {
                            if (doError)
                                messageHandler.ReportMessage(sourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey.Type), Lexer.GetKeywordFromToken(instanceKey)],
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
                        if (mutabilityKey != default)
                        {
                            if (doError)
                                messageHandler.ReportMessage(sourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey.Type), Lexer.GetKeywordFromToken(mutabilityKey)],
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
                        if (shadowKey != default)
                        {
                            if (doError)
                                messageHandler.ReportMessage(sourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey.Type), Lexer.GetKeywordFromToken(shadowKey)],
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
                        if (mutabilityKey != default)
                            index++;
                        decl.SpecialKeys.Insert(index, specialKey);
                        break;
                    }
                case 5:
                    {
                        if (partialKey != default)
                        {
                            if (doError)
                                messageHandler.ReportMessage(sourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey.Type), Lexer.GetKeywordFromToken(partialKey)],
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
                        if (mutabilityKey != default)
                            index++;
                        if (shadowKey != default)
                            index++;
                        decl.SpecialKeys.Insert(index, specialKey);
                        break;
                    }
                case 6:
                    {
                        if (externKey != default)
                        {
                            if (doError)
                                messageHandler.ReportMessage(sourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey.Type), Lexer.GetKeywordFromToken(externKey)],
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
                        if (mutabilityKey != default)
                            index++;
                        if (shadowKey != default)
                            index++;
                        if (partialKey != default)
                            index++;
                        decl.SpecialKeys.Insert(index, specialKey);
                        break;
                    }
                case 7:
                    {
                        if (inlineKey != default)
                        {
                            if (doError)
                                messageHandler.ReportMessage(sourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey.Type), Lexer.GetKeywordFromToken(inlineKey)],
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
                        if (mutabilityKey != default)
                            index++;
                        if (shadowKey != default)
                            index++;
                        if (partialKey != default)
                            index++;
                        if (externKey != default)
                            index++;
                        decl.SpecialKeys.Insert(index, specialKey);
                        break;
                    }
                case 8:
                    {
                        if (noexceptKey != default)
                        {
                            if (doError)
                                messageHandler.ReportMessage(sourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey.Type), Lexer.GetKeywordFromToken(noexceptKey)],
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
                        if (mutabilityKey != default)
                            index++;
                        if (shadowKey != default)
                            index++;
                        if (partialKey != default)
                            index++;
                        if (externKey != default)
                            index++;
                        if (inlineKey != default)
                            index++;
                        decl.SpecialKeys.Insert(index, specialKey);
                        break;
                    }
                case 9:
                    {
                        if (unsafeKey != default)
                        {
                            if (doError)
                                messageHandler.ReportMessage(sourceFile.Text, decl.Name,
                                    [Lexer.GetKeywordFromToken(specialKey.Type), Lexer.GetKeywordFromToken(unsafeKey)],
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
                        if (mutabilityKey != default)
                            index++;
                        if (shadowKey != default)
                            index++;
                        if (partialKey != default)
                            index++;
                        if (externKey != default)
                            index++;
                        if (inlineKey != default)
                            index++;
                        if (noexceptKey != default)
                            index++;
                        decl.SpecialKeys.Insert(index, specialKey);
                        break;
                    }
            }
        }

        public static int GetSpecialKeyType(TokenType specialKey)
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
                
                case TokenType.KwStatic:
                case TokenType.KwAbstract:
                case TokenType.KwSealed:
                    {
                        return 2;
                    }
                case TokenType.KwReadonly:
                case TokenType.KwConst:
                    {
                        return 3;
                    }
                case TokenType.KwNew:
                case TokenType.KwVirtual:
                case TokenType.KwOverride:
                    {
                        return 4;
                    }
                case TokenType.KwPartial:
                    {
                        return 5;
                    }
                case TokenType.KwExtern:
                    {
                        return 6;
                    }
                case TokenType.KwInline:
                    {
                        return 7;
                    }
                case TokenType.KwNoexcept:
                    {
                        return 8;
                    }
                case TokenType.KwUnsafe:
                    {
                        return 9;
                    }
            }
            return -1;
        }

        public static bool HasSpecialKeyType(AstDeclaration decl, int type, out int index)
        {
            for (int i = 0; i < decl.SpecialKeys.Count; ++i)
            {
                var currKey = decl.SpecialKeys[i];
                var keyType = GetSpecialKeyType(currKey.Type);
                if (keyType == type)
                {
                    index = i;
                    return true;
                }
            }
            index = -1;
            return false;
        }

        public static void ReplaceSpecialKeysByTypes(AstDeclaration decl, List<Token> newSpecialKeys)
        {
            foreach (Token sk in newSpecialKeys)
            {
                var keyType = GetSpecialKeyType(sk.Type);
                var has = HasSpecialKeyType(decl, keyType, out int index);
                if (has)
                {
                    decl.SpecialKeys[index] = sk;
                }
                else
                {
                    AddSpecialKeyToDecl(decl, sk, null, null, false);
                }
            }
        }
    }
}
