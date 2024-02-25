using System;
using Commons;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using static ATL.AudioData.IO.MetaDataIO;
using System.Linq;

namespace ATL.AudioData.IO
{
    internal static class IXmlTag
    {
        public const string CHUNK_IXML = "iXML";
        
        public static void FromStream(Stream source, MetaDataIO meta, ReadTagParams readTagParams, long chunkSize)
        {
            XmlArray xmlArray = new XmlArray(
                "BWFXML", 
                "ixml",
                e => e.EndsWith("LIST", StringComparison.OrdinalIgnoreCase),
                e => e.EndsWith("COUNT", StringComparison.OrdinalIgnoreCase)
                );
            xmlArray.FromStream(source, meta, readTagParams, chunkSize);
        }

        public static bool IsDataEligible(MetaDataIO meta)
        {
            return WavHelper.IsDataEligible(meta, "ixml.");
        }

        public static int ToStream(BinaryWriter w, bool isLittleEndian, MetaDataIO meta)
        {
            IDictionary<string, string> additionalFields = meta.AdditionalFields;
            w.Write(Utils.Latin1Encoding.GetBytes(CHUNK_IXML));

            long sizePos = w.BaseStream.Position;
            w.Write(0); // Placeholder for chunk size that will be rewritten at the end of the method


            XmlWriterSettings settings = new XmlWriterSettings
            {
                CloseOutput = false,
                Encoding = Encoding.UTF8
            };

            using (XmlWriter writer = XmlWriter.Create(w.BaseStream, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("BWFXML");

                // Path notes : key = node path; value = node name
                Dictionary<string, string> pathNodes = new Dictionary<string, string>();
                List<string> previousPathNodes = new List<string>();
                string subkey;
                foreach (var key in additionalFields.Keys.Where(key => key.StartsWith("ixml.")))
                {
                    // Create the list of path nodes
                    List<string> singleNodes = new List<string>(key.Split('.'));
                    singleNodes.RemoveAt(0);// Remove the "ixml" node
                    StringBuilder nodePrefix = new StringBuilder();
                    pathNodes.Clear();
                    foreach (string nodeName in singleNodes)
                    {
                        nodePrefix.Append(".").Append(nodeName);
                        pathNodes.Add(nodePrefix.ToString(), nodeName);
                    }
                    // Close all terminated (i.e. non present in current path) nodes in reverse order
                    for (int i = previousPathNodes.Count - 2; i >= 0; i--)
                    {
                        if (!pathNodes.ContainsKey(previousPathNodes[i]))
                        {
                            writer.WriteEndElement();
                        }
                    }
                    // Opens all new (i.e. non present in previous path) nodes
                    foreach (string nodePath in pathNodes.Keys)
                    {
                        if (!previousPathNodes.Contains(nodePath))
                        {
                            subkey = pathNodes[nodePath];
                            if (subkey.Equals(singleNodes[^1])) continue; // Last node is a leaf, not a node

                            if (subkey.Contains("[")) subkey = subkey.Substring(0, subkey.IndexOf("[")); // Remove [x]'s
                            writer.WriteStartElement(subkey.ToUpper());
                        }
                    }
                    // Write the last node (=leaf) as a proper value
                    writer.WriteElementString(singleNodes[^1], additionalFields[key]);
                    previousPathNodes = pathNodes.Keys.ToList();
                }

                // Closes all terminated paths
                if (previousPathNodes != null)
                    for (int i = previousPathNodes.Count - 2; i >= 0; i--)
                    {
                        writer.WriteEndElement();
                    }
            } // using XmlWriter

            // Add the extra padding byte if needed
            long finalPos = w.BaseStream.Position;
            long paddingSize = (finalPos - sizePos) % 2;
            if (paddingSize > 0) w.BaseStream.WriteByte(0);

            w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
            if (isLittleEndian)
            {
                w.Write((int)(finalPos - sizePos - 4));
            }
            else
            {
                w.Write(StreamUtils.EncodeBEInt32((int)(finalPos - sizePos - 4)));
            }

            return 14;
        }

    }
}
