using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Code2Script.Attributes
{
    public class MapToType: Attribute
    {
        public MapToType(string type)
        {
            Type = type;
        }

        public string Type { get; }
    }
}
