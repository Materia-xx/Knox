namespace Knox
{
    public class SecretTag
    {
        public SecretTag()
        {
        }

        public SecretTag(string name, string value)
        {
            this.Name = name;
            this.Value = value;
        }

        public string Name { get; set; }

        public string Value { get; set; }
    }
}
