using System;

namespace iLand.Tool
{
    /// <summary>
    /// ReadOnlySpan<char> version of String.Split().
    /// </summary>
    public class SplitString
    {
        private readonly double fileLength;
        private long positionInFile;
        private readonly char separator;
        private string sourceString;
        private readonly int[] tokenLengths;
        private readonly int[] tokenStartIndices;

        public int Count { get; private set; }
        public int Capacity { get; private init; }

        public SplitString(char separator, int tokenCapacity, long fileLength)
        {
            this.fileLength = (double)fileLength;
            this.positionInFile = 0;
            this.separator = separator;
            this.sourceString = String.Empty;
            this.tokenLengths = new int[tokenCapacity];
            this.tokenStartIndices = new int[tokenCapacity];

            this.Capacity = tokenCapacity;
        }

        public ReadOnlySpan<char> this[int tokenIndex]
        {
            get { return this.sourceString.AsSpan().Slice(this.tokenStartIndices[tokenIndex], this.tokenLengths[tokenIndex]); }
        }

        public double GetPositionInFile()
        {
            return (double)this.positionInFile / this.fileLength;
        }

        public void Parse(string sourceString, long positionInFile)
        {
            this.sourceString = sourceString;
            this.positionInFile = positionInFile;

            // locate tokens within string
            // For now, use of quotations or other secondary delimiters to allow separators to occur within tokens is not
            // supported.
            this.Count = 0;
            int tokenStartIndex = 0;
            for (int index = 0; index < sourceString.Length; ++index)
            {
                if (sourceString[index] == this.separator)
                {
                    this.tokenLengths[this.Count] = index - tokenStartIndex;
                    ++this.Count;

                    // first token always has start index 0
                    tokenStartIndex = index + 1;
                    this.tokenStartIndices[this.Count] = tokenStartIndex;
                }
            }

            // set length of last token (the last token may also be the only token)
            this.tokenLengths[this.Count] = sourceString.Length - this.tokenStartIndices[this.Count];
            ++this.Count;
        }
    }
}
