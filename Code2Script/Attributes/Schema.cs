using System;

namespace Code2Script.Attributes
{
    public class Schema : Attribute
    {
        public Schema(string Value)
        {
            this.Value = Value;
        }

        public string Value { get; }
    }
}
