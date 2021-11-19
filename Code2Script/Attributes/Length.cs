using System;
namespace Code2Script.Attributes
{
    public class Length : Attribute 
    {
        public Length(int length)
        {
            Value = length;
        }
        public int Value { get; }
    }
}
