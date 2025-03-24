namespace UnityChess
{
    /// <summary>
    /// Representation of a square on a chessboard.
    /// </summary>
    public readonly struct Square
    {
        public static readonly Square Invalid = new Square(-1, -1);

        public readonly int File;
        public readonly int Rank;

        /// <summary>
        /// Creates a new Square instance with specified file and rank.
        /// </summary>
        public Square(int file, int rank)
        {
            File = file;
            Rank = rank;
        }

        /// <summary>
        /// Creates a new Square instance from a string like "e4".
        /// </summary>
        public Square(string squareString)
        {
            this = string.IsNullOrEmpty(squareString)
                ? Invalid
                : SquareUtil.StringToSquare(squareString);
        }

        /// <summary>
        /// Creates a square offset from another square.
        /// </summary>
        internal Square(Square start, int fileOffset, int rankOffset)
        {
            File = start.File + fileOffset;
            Rank = start.Rank + rankOffset;
        }

        /// <summary>
        /// Returns true if the square is on the board (1-8, 1-8).
        /// </summary>
        public bool IsValid()
        {
            return File is >= 1 and <= 8 && Rank is >= 1 and <= 8;
        }

        public override string ToString()
        {
            return SquareUtil.SquareToString(this);
        }

        public bool Equals(Square other)
        {
            return File == other.File && Rank == other.Rank;
        }

        public override bool Equals(object obj)
        {
            return obj is Square other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (File * 397) ^ Rank;
            }
        }

        public static bool operator ==(Square lhs, Square rhs) => lhs.File == rhs.File && lhs.Rank == rhs.Rank;
        public static bool operator !=(Square lhs, Square rhs) => !(lhs == rhs);
        public static Square operator +(Square lhs, Square rhs) => new Square(lhs.File + rhs.File, lhs.Rank + rhs.Rank);
    }
}
