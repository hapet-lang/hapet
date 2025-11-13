namespace HapetFrontend.Ast
{
    /// <summary>
    /// Atomic location used to locale tokens
    /// </summary>
    public class TokenLocation : ILocation
    {
        public string File { get; set; }
        public int Line { get; set; }
        public int Index { get; set; }
        public int End { get; set; }
        public int LineStartIndex { get; set; }

        public TokenLocation Beginning => this;
        public TokenLocation Ending => this;

        public int Column => Index - LineStartIndex + 1;

        public TokenLocation()
        {
        }

        public TokenLocation Clone()
        {
            return new TokenLocation
            {
                File = File,
                Line = Line,
                Index = Index,
                End = End,
                LineStartIndex = LineStartIndex
            };
        }

        public override string ToString()
        {
            return $"{File}:{Line}:{Column}";
        }
    }

    public interface ILocation
    {
        TokenLocation Beginning { get; }
        TokenLocation Ending { get; }
    }

    public class Location : ILocation
    {
        public TokenLocation Beginning { get; }
        public TokenLocation Ending { get; }

        public Location(TokenLocation beg)
        {
            this.Beginning = beg;
            this.Ending = beg;
        }

        public Location(TokenLocation beg, TokenLocation end)
        {
            if (beg == null || end == null) throw new System.Exception("Arguments can't be null");
            this.Beginning = beg;
            this.Ending = end;
        }

        public Location(IEnumerable<ILocation> locations)
        {
            this.Beginning = locations.First().Beginning;
            this.Ending = locations.Last().Ending;
        }

        public static Location FromLocations<T>(IEnumerable<T> expressions)
            where T : ILocation
        {
            return new Location(expressions.First().Beginning, expressions.Last().Ending);
        }

        public override string ToString()
        {
            return Beginning.ToString();
        }
    }
}
