using System;
using System.Linq;

namespace SpawnDev.EBML
{
    /// <summary>
    /// Provides information to direct the encoding and decoding of EBML documents with the specified DocType
    /// </summary>
    public abstract class EBMLSchema
    {
        /// <summary>
        /// Schema DocType
        /// </summary>
        public abstract string DocType { get; }
        /// <summary>
        /// The Enum type that the schema will use to represent ElementIds
        /// </summary>
        public abstract Type ElementIdEnumType { get; }
        /// <summary>
        /// Used when trying to determine if an element is a child of an element of unknown size
        /// </summary>
        /// <param name="elementId"></param>
        /// <param name="childElementId"></param>
        /// <returns></returns>
        public abstract bool ValidChildCheck(Enum[] elementId, Enum childElementId);
        /// <summary>
        /// Returns the type to be created to represent this element instance
        /// </summary>
        /// <param name="elementId"></param>
        /// <returns></returns>
        public abstract Type? GetElementType(Enum elementId);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TElementId">The Enum type or ulong type that will be used to represent ElementIds</typeparam>
    public abstract class EBMLSchema<TElementId> : EBMLSchema where TElementId : struct
    {
        /// <summary>
        /// The Enum type that the schema will use to represent ElementIds
        /// </summary>
        public override Type ElementIdEnumType { get; } = typeof(TElementId);
        /// <summary>
        /// Used when trying to determine if an element is a child of an element of unknown size
        /// </summary>
        /// <param name="parentIdChain"></param>
        /// <param name="childElementId"></param>
        /// <returns></returns>
        public abstract bool ValidChildCheck(TElementId[] parentIdChain, TElementId childElementId);
        /// <summary>
        /// Returns the type to be created to represent this element instance
        /// </summary>
        /// <param name="elementId"></param>
        /// <returns></returns>
        public abstract Type? GetElementType(TElementId elementId);
        /// <summary>
        /// Returns the type to be created to represent this element instance
        /// </summary>
        /// <param name="elementId"></param>
        /// <returns></returns>
        public override Type? GetElementType(Enum elementId) => GetElementType((TElementId)(object)elementId);
        /// <summary>
        /// Used when trying to determine if an element is a child of an element of unknown size
        /// </summary>
        /// <param name="parentIdChain"></param>
        /// <param name="childElementId"></param>
        /// <returns></returns>
        public override bool ValidChildCheck(Enum[] parentIdChain, Enum childElementId)
        {
            return ValidChildCheck(parentIdChain.Select(o => o.ToEnum<TElementId>()).ToArray(), childElementId.ToEnum<TElementId>());
        }
    }
}
