using HapetFrontend.Ast;
using HapetFrontend.Enums;
using HapetFrontend.Types;
using System.Text;

namespace HapetFrontend.Parsing
{
    public partial class Lexer
    {
        private void ParseIdentifier(ref Token token, TokenType idtype)
        {
            token.Type = idtype;

            int start = _location.Index;

            switch (idtype)
            {
                case TokenType.AtSignIdentifier:
                case TokenType.DollarIdentifier:
                case TokenType.SharpIdentifier:
                    _location.Index++;
                    start++;
                    break;
            }

            while (_location.Index < _text.Length && IsIdent(Current))
            {
                _location.Index++;
            }

            token.Data = _text.Substring(start, _location.Index - start);
        }

        private void ParseStringLiteral(ref Token token, char end)
        {
            token.Type = TokenType.StringLiteral;
            int start = _location.Index++;
            StringBuilder sb = new StringBuilder();

            bool foundEnd = false;
            while (_location.Index < _text.Length)
            {
                char c = Current;
                _location.Index++;
                if (c == end)
                {
                    foundEnd = true;
                    break;
                }
                else if (c == '\\')
                {
                    if (_location.Index >= _text.Length)
                    {
                        _messageHandler.ReportMessage(_text, new Location(_location), $"Unexpected end of file while parsing string literal");
                        token.Data = sb.ToString();
                        return;
                    }
                    switch (Current)
                    {
                        case '0': sb.Append('\0'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(Current); break;
                    }
                    _location.Index++;
                    continue;
                }

                if (c == '\n')
                {
                    _location.Line++;
                    _location.LineStartIndex = _location.Index;
                }

                sb.Append(c);
            }

            if (!foundEnd)
            {
                _messageHandler.ReportMessage(_text, new Location(_location), $"Unexpected end of string literal");
            }

            token.Data = sb.ToString();
        }

        private void ParseNumberLiteral(ref Token token)
        {
            token.Type = TokenType.NumberLiteral;
            var dataIntBase = 10;
            var dataStringValue = "";
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


            while (_location.Index < _text.Length && state != -1 && state != StateDone)
            {
                char c = Current;

                switch (state)
                {
                    case StateInit:
                        {
                            if (c == '0')
                            {
                                dataStringValue += '0';
                                state = State0;
                            }
                            else if (IsDigit(c))
                            {
                                dataStringValue += c;
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
                                dataStringValue = "";
                                state = StateX;
                            }
                            else if (c == 'b')
                            {
                                dataIntBase = 2;
                                dataStringValue = "";
                                state = StateB;
                            }
                            else if (IsDigit(c))
                            {
                                dataStringValue += c;
                                state = StateDecimalDigit;
                            }
                            else if (c == '.' && Next != '.')
                            {
                                dataStringValue += c;
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
                                dataStringValue += c;
                            else if (c == '.' && Next != '.')
                            {
                                dataStringValue += c;
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
                                dataStringValue += c;
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
                                dataStringValue += c;
                            }
                            else if (c == '_')
                            {
                                state = StateFloat_;
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
                                dataStringValue += c;
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
                                dataStringValue += c;
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
                                dataStringValue += c;
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
                                dataStringValue += c;
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
                            dataStringValue += c;
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
                            dataStringValue += c;
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
                            dataStringValue += c;
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
                            dataStringValue += c;
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
                    _location.Index++;
                }
            }

            if (state == -1)
            {
                token.Type = TokenType.Unknown;
                token.Data = error;
                return;
            }

            token.Data = new NumberData(dataType, dataStringValue, dataIntBase);
        }
    }
}
