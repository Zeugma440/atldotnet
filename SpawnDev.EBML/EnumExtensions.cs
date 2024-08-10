using System;
using System.Linq;

namespace SpawnDev.EBML
{
    public static class EnumExtensions
    {
        public static string ToString(this Enum[] enumArray, string delimeter) => string.Join(delimeter, enumArray.Select(o => o.ToString()).ToArray());
        public static ulong[] ToUInt64(this Enum[] enumArray) => enumArray.Select(ToUInt64).ToArray();
        public static ulong ToUInt64(this Enum value) => (ulong)(object)value;
        public static Enum ToEnum(this Enum value, Type enumType) => enumType == value.GetType() ? value : (Enum)Enum.ToObject(enumType, value);
        public static T ToEnum<T>(this Enum value) where T : struct => (T)(object)value.ToEnum(typeof(T));
    }
}
