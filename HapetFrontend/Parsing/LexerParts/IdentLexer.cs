using HapetFrontend.Ast;
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using HapetFrontend.Types;
using System.Text;

namespace HapetFrontend.Parsing
{
    public partial class Lexer
    {
        private void ParseIdentifier(TokenLocation location, ref Token token, TokenType idtype)
        {
            token.Type = idtype;

            int start = location.Index;

            switch (idtype)
            {
                case TokenType.AtSignIdentifier:
                case TokenType.DollarIdentifier:
                case TokenType.SharpIdentifier:
                    location.Index++;
                    start++;
                    break;
            }

            while (location.Index < _text.Length && IsIdent(Current(location)))
            {
                location.Index++;
            }

            token.Data = _text.Substring(start, location.Index - start);
        }

        private void ParseStringLiteral(TokenLocation location, ref Token token, char end)
        {
            token.Type = TokenType.StringLiteral;
            int start = location.Index++;
            StringBuilder sb = new StringBuilder();

            bool foundEnd = false;
            while (location.Index < _text.Length)
            {
                char c = Current(location);
                location.Index++;
                if (c == end)
                {
                    foundEnd = true;
                    break;
                }
                else if (c == '\\')
                {
                    if (location.Index >= _text.Length)
                    {
                        _messageHandler.ReportMessage(_programFile, new Location(location), [], ErrorCode.Get(CTEN.UnexpectedEndOfStringLit));
                        token.Data = sb.ToString();
                        return;
                    }
                    switch (Current(location))
                    {
                        case '0': sb.Append('\0'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(Current(location)); break;
                    }
                    location.Index++;
                    continue;
                }

                if (c == '\n')
                {
                    location.Line++;
                    location.LineStartIndex = location.Index;
                }

                sb.Append(c);
            }

            if (!foundEnd)
            {
                _messageHandler.ReportMessage(_programFile, new Location(location), [], ErrorCode.Get(CTEN.UnexpectedEndOfStringLit));
            }

            token.Data = sb.ToString();
        }

        private void ParseNumberLiteral(TokenLocation location, ref Token token)
        {
            token.Type = TokenType.NumberLiteral;
            var dataIntBase = 10;
            StringBuilder dataStringValue = new StringBuilder();
            var dataType = NumberType.Int;

            const int StateError = -1;
            const int StateInit = 0;
            const int State0 = 1;
            const int StateX = 2;
            const int StateB = 3;
            const int StateDecimalDigit = 5;
            const int StateBinaryDigit = 6;
            const int StateHexDigit = 7;
            const int StateDone = 9;
            const int StateFloatPoint = 10;
            const int StateFloatDigit = 11;
            const int StateDecimal_ = 12;
            const int StateHex_ = 13;
            const int StateBinary_ = 14;
            const int StateFloat_ = 15;
            int state = StateInit;
            string error = null;


            while (location.Index < _text.Length && state != -1 && state != StateDone)
            {
                char c = Current(location);

                switch (state)
                {
                    case StateInit:
                        {
                            if (c == '0')
                            {
                                dataStringValue.Append('0');
                                state = State0;
                            }
                            else if (IsDigit(c))
                            {
                                dataStringValue.Append(c);
                                state = StateDecimalDigit;
                            }
                            else
                            {
                                state = StateError;
                                error = "THIS SHOULD NOT HAPPEN!";
                            }
                            break;
                        }



                    case State0:
                        {
                            if (c == 'x')
                            {
                                dataIntBase = 16;
                                dataStringValue.Clear();
                                state = StateX;
                            }
                            else if (c == 'b')
                            {
                                dataIntBase = 2;
                                dataStringValue.Clear();
                                state = StateB;
                            }
                            else if (IsDigit(c))
                            {
                                dataStringValue.Append(c);
                                state = StateDecimalDigit;
                            }
                            else if (c == '.' && Next(location) != '.')
                            {
                                dataStringValue.Append(c);
                                state = StateFloatPoint;
                                dataType = NumberType.Float;
                            }
                            else
                            {
                                state = StateDone;
                            }
                            break;
                        }

                    case StateDecimalDigit:
                        {
                            if (IsDigit(c))
                                dataStringValue.Append(c);
                            else if (c == '.' && Next(location) != '.')
                            {
                                dataStringValue.Append(c);
                                state = StateFloatPoint;
                                dataType = NumberType.Float;

                            }
                            else if (c == '_')
                            {
                                state = StateDecimal_;
                            }
                            else
                            {
                                state = StateDone;
                            }
                            break;
                        }

                    case StateFloatPoint:
                        {
                            if (IsDigit(c))
                            {
                                dataStringValue.Append(c);
                                state = StateFloatDigit;
                            }
                            else
                            {
                                error = "Invalid character, expected digit";
                                state = -1;
                            }
                            break;
                        }

                    case StateFloatDigit:
                        {
                            if (IsDigit(c))
                            {
                                dataStringValue.Append(c);
                            }
                            else if (c == '_')
                            {
                                state = StateFloat_;
                            }
                            else if (c == 'E' || c == 'e')
                            {
                                // just append the exponent
                                dataStringValue.Append(c);
                                if (Next(location) == '+' || Next(location) == '-')
                                {
                                    // just append the sign
                                    dataStringValue.Append(Next(location));
                                    location.Index++;
                                }
                            }
                            else
                            {
                                state = StateDone;
                            }
                            break;
                        }

                    case StateX:
                        {
                            if (IsHexDigit(c))
                            {
                                dataStringValue.Append(c);
                                state = StateHexDigit;
                            }
                            else
                            {
                                error = "Invalid character, expected hex digit";
                                state = -1;
                            }
                            break;
                        }

                    case StateHexDigit:
                        {
                            if (IsHexDigit(c))
                                dataStringValue.Append(c);
                            else if (c == '_')
                            {
                                state = StateHex_;
                            }
                            else
                            {
                                state = StateDone;
                            }
                            break;
                        }

                    case StateB:
                        {
                            if (IsBinaryDigit(c))
                            {
                                dataStringValue.Append(c);
                                state = StateBinaryDigit;
                            }
                            else if (IsDigit(c))
                            {
                                error = "Invalid character, expected binary digit";
                                state = -1;
                            }
                            else
                            {
                                state = StateDone;
                            }
                            break;
                        }

                    case StateBinaryDigit:
                        {
                            if (IsBinaryDigit(c))
                                dataStringValue.Append(c);
                            else if (c == '_')
                            {
                                state = StateBinary_;
                            }
                            else if (IsDigit(c))
                            {
                                error = "Invalid character, expected binary digit";
                                state = -1;
                            }
                            else
                            {
                                state = StateDone;
                            }
                            break;
                        }

                    case StateDecimal_:
                        if (IsDigit(c))
                        {
                            dataStringValue.Append(c);
                            state = StateDecimalDigit;
                        }
                        else
                        {
                            error = $"Unexpected character '{c}'. Expected digit";
                            state = StateError;
                        }
                        break;

                    case StateHex_:
                        if (IsHexDigit(c))
                        {
                            dataStringValue.Append(c);
                            state = StateHexDigit;
                        }
                        else
                        {
                            error = $"Unexpected character '{c}'. Expected hex digit";
                            state = StateError;
                        }
                        break;

                    case StateBinary_:
                        if (IsDigit(c))
                        {
                            dataStringValue.Append(c);
                            state = StateBinaryDigit;
                        }
                        else
                        {
                            error = $"Unexpected character '{c}'. Expected binary digit";
                            state = StateError;
                        }
                        break;

                    case StateFloat_:
                        if (IsDigit(c))
                        {
                            dataStringValue.Append(c);
                            state = StateFloatDigit;
                        }
                        else
                        {
                            error = $"Unexpected character '{c}'. Expected digit";
                            state = StateError;
                        }
                        break;
                }

                if (state != StateDone)
                {
                    location.Index++;
                }
            }

            if (state == -1)
            {
                token.Type = TokenType.Unknown;
                token.Data = error;
                return;
            }

            token.Data = new NumberData(dataType, dataStringValue.ToString(), dataIntBase);
        }
    }
}
