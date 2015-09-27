namespace DumpAnalyzer
{
    internal enum StackFrameType
    {
        Native,
        Managed
    }

    internal class StackFrame
    {
        public StackFrameType Type { get; set; }
        public ulong StackPointer { get; set; }
        public ulong InstructionPointer { get; set; }
        public string ModuleName { get; set; }
        public string MethodName { get; set; }
        public ulong MethodOffset { get; set; }

        public override string ToString()
        {
            return string.Format("{4:x} {0}: {1}!{2}+{3}", Type, ModuleName, MethodName, MethodOffset,
                                 InstructionPointer);
        }

        public bool Match(Filter filter)
        {
            return MatchModuleName(filter.ModuleName) && MatchMethodName(filter.MethodName);
        }

        private bool MatchMethodName(string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return true;
            }
            return MethodName.ToLower().Equals(filter.ToLower());
        }

        private bool MatchModuleName(string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return true;
            }
            return ModuleName.ToLower().Equals(filter.ToLower());
        }
    }
}