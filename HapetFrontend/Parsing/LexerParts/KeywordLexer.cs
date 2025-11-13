namespace HapetFrontend.Parsing
{
    public partial class Lexer
    {
        private static void CheckKeywords(ref Token token)
        {
            switch (token.Data as string)
            {
                case "interface": token.Type = TokenType.KwInterface; break;
                case "class": token.Type = TokenType.KwClass; break;
                case "struct": token.Type = TokenType.KwStruct; break;
                case "enum": token.Type = TokenType.KwEnum; break;
                case "delegate": token.Type = TokenType.KwDelegate; break;

                case "true": token.Type = TokenType.KwTrue; break;
                case "false": token.Type = TokenType.KwFalse; break;
                case "null": token.Type = TokenType.KwNull; break;

                case "using": token.Type = TokenType.KwUsing; break;
                case "namespace": token.Type = TokenType.KwNamespace; break;

                case "if": token.Type = TokenType.KwIf; break;
                case "else": token.Type = TokenType.KwElse; break;
                case "switch": token.Type = TokenType.KwSwitch; break;
                case "case": token.Type = TokenType.KwCase; break;
                case "for": token.Type = TokenType.KwFor; break;
                case "foreach": token.Type = TokenType.KwForeach; break;
                case "while": token.Type = TokenType.KwWhile; break;
                case "do": token.Type = TokenType.KwDo; break;
                case "goto": token.Type = TokenType.KwGoto; break;

                // exceptions
                case "try": token.Type = TokenType.KwTry; break;
                case "catch": token.Type = TokenType.KwCatch; break;
                case "finally": token.Type = TokenType.KwFinally; break;
                case "throw": token.Type = TokenType.KwThrow; break;

                case "break": token.Type = TokenType.KwBreak; break;
                case "continue": token.Type = TokenType.KwContinue; break;
                case "return": token.Type = TokenType.KwReturn; break;
                case "yield": token.Type = TokenType.KwYield; break;

                case "const": token.Type = TokenType.KwConst; break;
                case "readonly": token.Type = TokenType.KwReadonly; break;
                case "unsafe": token.Type = TokenType.KwUnsafe; break;
                case "volatile": token.Type = TokenType.KwVolatile; break;
                case "global": token.Type = TokenType.KwGlobal; break;

                case "default": token.Type = TokenType.KwDefault; break;
                case "new": token.Type = TokenType.KwNew; break;
                case "stackalloc": token.Type = TokenType.KwStackAlloc; break;
                case "base": token.Type = TokenType.KwBase; break;
                case "sizeof": token.Type = TokenType.KwSizeof; break;
                case "alignof": token.Type = TokenType.KwAlignof; break;
                case "typeof": token.Type = TokenType.KwTypeof; break;
                case "nameof": token.Type = TokenType.KwNameof; break;
                case "checked": token.Type = TokenType.KwChecked; break;
                case "unchecked": token.Type = TokenType.KwUnchecked; break;

                case "get": token.Type = TokenType.KwGet; break;
                case "set": token.Type = TokenType.KwSet; break;

                case "in": token.Type = TokenType.KwIn; break;
                case "is": token.Type = TokenType.KwIs; break;
                case "as": token.Type = TokenType.KwAs; break;
                case "ref": token.Type = TokenType.KwRef; break;
                case "out": token.Type = TokenType.KwOut; break;
                case "params": token.Type = TokenType.KwParams; break;
                case "arglist": token.Type = TokenType.KwArglist; break;
                case "where": token.Type = TokenType.KwWhere; break;

                case "public": token.Type = TokenType.KwPublic; break;
                case "internal": token.Type = TokenType.KwInternal; break;
                case "protected": token.Type = TokenType.KwProtected; break;
                case "private": token.Type = TokenType.KwPrivate; break;
                case "unreflected": token.Type = TokenType.KwUnreflected; break;

                case "async": token.Type = TokenType.KwAsync; break;
                case "await": token.Type = TokenType.KwAwait; break;

                case "static": token.Type = TokenType.KwStatic; break;
                case "abstract": token.Type = TokenType.KwAbstract; break;
                case "virtual": token.Type = TokenType.KwVirtual; break;
                case "override": token.Type = TokenType.KwOverride; break;
                case "partial": token.Type = TokenType.KwPartial; break;
                case "extern": token.Type = TokenType.KwExtern; break;
                case "sealed": token.Type = TokenType.KwSealed; break;
                case "inline": token.Type = TokenType.KwInline; break;

                // for events
                case "event": token.Type = TokenType.KwEvent; break;
                case "add": token.Type = TokenType.KwAdd; break;
                case "remove": token.Type = TokenType.KwRemove; break;

                // for overriding casts
                case "explicit": token.Type = TokenType.KwExplicit; break;
                case "implicit": token.Type = TokenType.KwImplicit; break;
                // for overriding operators
                case "operator": token.Type = TokenType.KwOperator; break;
            }
        }

        public static string GetKeywordFromToken(TokenType token)
        {
            switch (token)
            {
                case TokenType.KwInterface: return "interface";
                case TokenType.KwClass: return "class";
                case TokenType.KwStruct: return "struct";
                case TokenType.KwEnum: return "enum";
                case TokenType.KwDelegate: return "delegate";

                case TokenType.KwTrue: return "true";
                case TokenType.KwFalse: return "false";
                case TokenType.KwNull: return "null";

                case TokenType.KwUsing: return "using";
                case TokenType.KwNamespace: return "namespace";

                case TokenType.KwIf: return "if";
                case TokenType.KwElse: return "else";
                case TokenType.KwSwitch: return "switch";
                case TokenType.KwCase: return "case";
                case TokenType.KwFor: return "for";
                case TokenType.KwForeach: return "foreach";
                case TokenType.KwWhile: return "while";
                case TokenType.KwDo: return "do";
                case TokenType.KwGoto: return "goto";

                // exceptions
                case TokenType.KwTry: return "try";
                case TokenType.KwCatch: return "catch";
                case TokenType.KwFinally: return "finally";
                case TokenType.KwThrow: return "throw";

                case TokenType.KwBreak: return "break";
                case TokenType.KwContinue: return "continue";
                case TokenType.KwReturn: return "return";
                case TokenType.KwYield: return "yield";

                case TokenType.KwConst: return "const";
                case TokenType.KwReadonly: return "readonly";
                case TokenType.KwUnsafe: return "unsafe";
                case TokenType.KwVolatile: return "volatile";
                case TokenType.KwGlobal: return "global";
                case TokenType.KwDefault: return "default";
                case TokenType.KwNew: return "new";
                case TokenType.KwBase: return "base";
                case TokenType.KwSizeof: return "sizeof";
                case TokenType.KwAlignof: return "alignof";
                case TokenType.KwTypeof: return "typeof";
                case TokenType.KwNameof: return "nameof";

                case TokenType.KwGet: return "get";
                case TokenType.KwSet: return "set";

                case TokenType.KwIn: return "in";
                case TokenType.KwIs: return "is";
                case TokenType.KwAs: return "as";
                case TokenType.KwRef: return "ref";
                case TokenType.KwOut: return "out";
                case TokenType.KwParams: return "params";
                case TokenType.KwArglist: return "arglist";
                case TokenType.KwWhere: return "where";

                case TokenType.KwPublic: return "public";
                case TokenType.KwInternal: return "internal";
                case TokenType.KwProtected: return "protected";
                case TokenType.KwPrivate: return "private";
                case TokenType.KwUnreflected: return "unreflected";

                case TokenType.KwAsync: return "async";
                case TokenType.KwAwait: return "await";

                case TokenType.KwStatic: return "static";
                case TokenType.KwAbstract: return "abstract";
                case TokenType.KwVirtual: return "virtual";
                case TokenType.KwOverride: return "override";
                case TokenType.KwPartial: return "partial";
                case TokenType.KwExtern: return "extern";
                case TokenType.KwSealed: return "sealed";
                case TokenType.KwInline: return "inline";

                // for events
                case TokenType.KwEvent: return "event";
                case TokenType.KwAdd: return "add";
                case TokenType.KwRemove: return "remove";

                // for overriding casts
                case TokenType.KwExplicit: return "explicit";
                case TokenType.KwImplicit: return "implicit";
                // for overriding operators
                case TokenType.KwOperator: return "operator";
                default: return "!unexpected!";
            }
        }
    }
}
