using System;
using System.Text;

namespace SpawnDev.EBML
{
    public class UTF8StringElement : StringElement
    {
        public static explicit operator string?(UTF8StringElement? element) => element == null ? null : element.Data;
        public UTF8StringElement(Enum id) : base(id) { }
        public override Encoding Encoding { get; } = Encoding.UTF8;
        public UTF8StringElement(Enum id, string value) : base(id, value) { }
    }
}
