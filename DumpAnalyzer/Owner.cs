namespace DumpAnalyzer
{
    public class Owner
    {
        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Name
        {
            get { return FirstName + " " + LastName; }
        }
    }
}