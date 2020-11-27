namespace SVFileMapper.Models
{
    public struct ParserProgress
    {
        public ParserProgress(int current, int max)
        {
            Current = current;
            Max = max;
        }

        public int Current { get; set; }
        public int Max { get; set; }

        public void Deconstruct(out int current, out int max)
        {
            current = Current;
            max = Max;
        }
    }
}