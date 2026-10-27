using HapetFrontend.Entities;
using HapetFrontend.Syntax.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace HapetFrontend.Syntax.Green.Tokens
{
    internal class GreenSyntaxFactory
    {
        private const string _crLf = "\r\n";

        public static GreenSyntaxTrivia _carriageReturnLineFeed;
        public static GreenSyntaxTrivia CarriageReturnLineFeed
        {
            get
            {
                _carriageReturnLineFeed ??= EndOfLine(_crLf);
                return _carriageReturnLineFeed;
            }
        }
        public static GreenSyntaxTrivia _lineFeed;
        public static GreenSyntaxTrivia LineFeed
        {
            get
            {
                _lineFeed ??= EndOfLine("\n");
                return _lineFeed;
            }
        }
        public static GreenSyntaxTrivia _carriageReturn;
        public static GreenSyntaxTrivia CarriageReturn
        {
            get
            {
                _carriageReturn ??= EndOfLine("\r");
                return _carriageReturn;
            }
        }
        public static GreenSyntaxTrivia _space;
        public static GreenSyntaxTrivia Space
        {
            get
            {
                _space ??= Whitespace(" ");
                return _space;
            }
        }
        public static GreenSyntaxTrivia _tab;
        public static GreenSyntaxTrivia Tab
        {
            get
            {
                _tab ??= Whitespace("\t");
                return _tab;
            }
        }

        public static GreenSyntaxTrivia _elasticCarriageReturnLineFeed;
        public static GreenSyntaxTrivia ElasticCarriageReturnLineFeed
        {
            get
            {
                _elasticCarriageReturnLineFeed ??= EndOfLine(_crLf, elastic: true);
                return _elasticCarriageReturnLineFeed;
            }
        }
        public static GreenSyntaxTrivia _elasticLineFeed;
        public static GreenSyntaxTrivia ElasticLineFeed
        {
            get
            {
                _elasticLineFeed ??= EndOfLine("\n", elastic: true);
                return _elasticLineFeed;
            }
        }
        public static GreenSyntaxTrivia _elasticCarriageReturn;
        public static GreenSyntaxTrivia ElasticCarriageReturn
        {
            get
            {
                _elasticCarriageReturn ??= EndOfLine("\r", elastic: true);
                return _elasticCarriageReturn;
            }
        }
        public static GreenSyntaxTrivia _elasticSpace;
        public static GreenSyntaxTrivia ElasticSpace
        {
            get
            {
                _elasticSpace ??= Whitespace(" ", elastic: true);
                return _elasticSpace;
            }
        }
        public static GreenSyntaxTrivia _elasticTab;
        public static GreenSyntaxTrivia ElasticTab
        {
            get
            {
                _elasticTab ??= Whitespace("\t", elastic: true);
                return _elasticTab;
            }
        }

        public static GreenSyntaxTrivia _elasticZeroSpace;
        public static GreenSyntaxTrivia ElasticZeroSpace
        {
            get
            {
                _elasticZeroSpace ??= Whitespace(string.Empty, elastic: true);
                return _elasticZeroSpace;
            }
        }

        public static GreenSyntaxToken _endOfFile;
        public static GreenSyntaxToken EndOfFile 
        {
            get
            {
                _endOfFile ??= Token(null, SyntaxKind.EOF, null);
                return _endOfFile;
            }
        }

        public static GreenSyntaxTrivia EndOfLine(string text, bool elastic = false)
        {
            GreenSyntaxTrivia trivia = GreenSyntaxTrivia.Create(SyntaxKind.NewLine, text);
            if (!elastic)
            {
                return trivia;
            }
            return (GreenSyntaxTrivia)trivia.WithAnnotations([HapetSyntaxAnnotation.ElasticAnnotation]);
        }

        public static GreenSyntaxTrivia Whitespace(string text, bool elastic = false)
        {
            var trivia = GreenSyntaxTrivia.Create(SyntaxKind.Whitespace, text);
            if (!elastic)
            {
                return trivia;
            }

            return (GreenSyntaxTrivia)trivia.WithAnnotations([HapetSyntaxAnnotation.ElasticAnnotation]);
        }

        public static GreenSyntaxToken Token(SyntaxKind kind)
        {
            HapetAssert.Assert(kind != SyntaxKind.Identifier);
            HapetAssert.Assert(kind != SyntaxKind.CharLiteral);
            HapetAssert.Assert(kind != SyntaxKind.NumberLiteral);

            return GreenSyntaxToken.Create(kind);
        }

        public static GreenSyntaxToken TokenWithTrailingSpace(SyntaxKind kind)
        {
            HapetAssert.Assert(kind != SyntaxKind.Identifier);
            HapetAssert.Assert(kind != SyntaxKind.CharLiteral);
            HapetAssert.Assert(kind != SyntaxKind.NumberLiteral);

            return GreenSyntaxToken.CreateTokenWithTrailingSpace(kind);
        }

        public static GreenSyntaxToken Token(GreenNode leading, SyntaxKind kind, GreenNode trailing)
        {
            HapetAssert.Assert(kind != SyntaxKind.Identifier);
            HapetAssert.Assert(kind != SyntaxKind.CharLiteral);
            HapetAssert.Assert(kind != SyntaxKind.NumberLiteral);

            return GreenSyntaxToken.Create(kind, leading, trailing);
        }

        /// <summary>
        /// Creates a token whose Text and ValueText are the same.
        /// </summary>
        public static GreenSyntaxToken Token(GreenNode leading, SyntaxKind kind, string text, GreenNode trailing)
        {
            return Token(leading, kind, text, text, trailing);
        }

        public static GreenSyntaxToken Token(GreenNode leading, SyntaxKind kind, string text, string valueText, GreenNode trailing)
        {
            HapetAssert.Assert(kind != SyntaxKind.Identifier);
            HapetAssert.Assert(kind != SyntaxKind.CharLiteral);
            HapetAssert.Assert(kind != SyntaxKind.NumberLiteral);

            return kind >= GreenSyntaxToken.FirstTokenWithWellKnownText &&
                   kind <= GreenSyntaxToken.LastTokenWithWellKnownText
                ? Token(leading, kind, trailing)
                : GreenSyntaxToken.WithValue(kind, leading, text, valueText, trailing);
        }

        public static GreenSyntaxToken MissingToken(SyntaxKind kind)
        {
            return GreenSyntaxToken.CreateMissing(kind);
        }

        public static GreenSyntaxToken MissingToken(GreenNode leading, SyntaxKind kind, GreenNode trailing)
        {
            return GreenSyntaxToken.CreateMissing(kind, leading, trailing);
        }

        public static GreenSyntaxToken Identifier(string text)
        {
            return Identifier(SyntaxKind.Identifier, null, text, text, null);
        }

        public static GreenSyntaxToken Identifier(GreenNode leading, string text, GreenNode trailing)
        {
            return Identifier(SyntaxKind.Identifier, leading, text, text, trailing);
        }

        public static GreenSyntaxToken Identifier(
            SyntaxKind contextualKind,
            GreenNode leading,
            string text,
            string valueText,
            GreenNode trailing)
        {
            return GreenSyntaxToken.Identifier(contextualKind, leading, text, valueText, trailing);
        }

        public static GreenSyntaxToken Literal(GreenNode leading, string text, NumberData value, GreenNode trailing)
        {
            return GreenSyntaxToken.WithValue(SyntaxKind.NumberLiteral, leading, text, value, trailing);
        }

        public static GreenSyntaxToken Literal(GreenNode leading, string text, string value, GreenNode trailing)
        {
            return GreenSyntaxToken.WithValue(SyntaxKind.StringLiteral, leading, text, value, trailing);
        }

        public static GreenSyntaxToken Literal(GreenNode leading, string text, SyntaxKind kind, string value, GreenNode trailing)
        {
            return GreenSyntaxToken.WithValue(kind, leading, text, value, trailing);
        }

        public static GreenSyntaxToken Literal(GreenNode leading, string text, char value, GreenNode trailing)
        {
            return GreenSyntaxToken.WithValue(SyntaxKind.CharLiteral, leading, text, value, trailing);
        }

        public static GreenSyntaxToken BadToken(GreenNode leading, string text, GreenNode trailing)
        {
            return GreenSyntaxToken.WithValue(SyntaxKind.Unknown, leading, text, text, trailing);
        }
    }
}
