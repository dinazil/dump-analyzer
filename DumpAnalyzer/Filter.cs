using System;

namespace DumpAnalyzer
{
    public class Filter : IEquatable<Filter>
    {
        public string ModuleName { get; set; }

        public string MethodName { get; set; }

        public bool Equals(Filter other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(ModuleName, other.ModuleName) && string.Equals(MethodName, other.MethodName);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Filter) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ModuleName != null ? ModuleName.GetHashCode() : 0)*397) ^
                       (MethodName != null ? MethodName.GetHashCode() : 0);
            }
        }

        public static bool operator ==(Filter left, Filter right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Filter left, Filter right)
        {
            return !Equals(left, right);
        }
    }
}