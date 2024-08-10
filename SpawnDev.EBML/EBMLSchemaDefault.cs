using System;
using System.Collections.Generic;

namespace SpawnDev.EBML
{
    public class EBMLSchemaDefault : EBMLSchema<ElementId>
    {
        public override string DocType { get; } = "";

        public override bool ValidChildCheck(ElementId[] parentIdChain, ElementId childElementId)
        {
            return false;
        }
        
        public override Type? GetElementType(ElementId elementId)
        {
            return ElementTypeMap.TryGetValue(elementId, out var ret) ? ret : null;
        }

        /// <summary>
        /// ElementIds mapped to the type that will be used to represent the specified element 
        /// </summary>
        public static Dictionary<ElementId, Type> ElementTypeMap { get; } = new Dictionary<ElementId, Type>
        {
            { ElementId.EBML, typeof(EBMLElement) },
            { ElementId.EBMLVersion, typeof(UintElement) },
            { ElementId.EBMLReadVersion, typeof(UintElement) },
            { ElementId.EBMLMaxIDLength, typeof(UintElement) },
            { ElementId.EBMLMaxSizeLength, typeof(UintElement) },
            { ElementId.DocType, typeof(StringElement) },
            { ElementId.DocTypeVersion, typeof(UintElement) },
            { ElementId.DocTypeReadVersion, typeof(UintElement) },
            { ElementId.DocTypeExtension, typeof(MasterElement) },
            { ElementId.DocTypeExtensionName, typeof(StringElement) },
            { ElementId.DocTypeExtensionVersion, typeof(UintElement) },
            { ElementId.Void, typeof(BinaryElement) },
            { ElementId.CRC32, typeof(BinaryElement) },
        };
    }
}
