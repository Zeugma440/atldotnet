using System;
using System.Linq;

namespace SpawnDev.EBML
{
    public static class UInt64Extensions
    {
        public static Enum ToEnum(this ulong value, Type enumType) => (Enum)Enum.ToObject(enumType, value);
        public static Enum[] ToEnum(this ulong[] values, Type enumType) => values.Select(o => o.ToEnum(enumType)).ToArray();
    }
}
