using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using static ATL.AudioData.IO.MetaDataIO;
using System.Linq;
using ATL.Logging;

namespace ATL.AudioData.IO
{
    internal static class IXmlTag
    {
        public const string CHUNK_IXML = "iXML";

        private static string getPosition(IEnumerable<string> position)
        {
            StringBuilder result = new StringBuilder();
            bool first = true;

            foreach (string s in position)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    result.Append(".");
                }
                result.Append(s);
            }

            return result.ToString();
        }

        public static void FromStream(Stream source, MetaDataIO meta, ReadTagParams readTagParams, long chunkSize)
        {
            IList<string> position = new List<string> { "ixml" };
            long initialOffset = source.Position;
            int nbSkipBegin = StreamUtils.SkipValues(source, new[] { 10, 13, 32, 0 }); // Ignore leading CR, LF, whitespace, null
            source.Seek(initialOffset + chunkSize, SeekOrigin.Begin);
            int nbSkipEnd = StreamUtils.SkipValuesEnd(source, new[] { 10, 13, 32, 0 }); // Ignore ending CR, LF, whitespace, null
            source.Seek(initialOffset + nbSkipBegin, SeekOrigin.Begin);

            using (MemoryStream mem = new MemoryStream((int)chunkSize - nbSkipBegin - nbSkipEnd))
            {
                StreamUtils.CopyStream(source, mem, chunkSize - nbSkipBegin - nbSkipEnd); // Isolate XML structure in a clean memory chunk
                mem.Seek(0, SeekOrigin.Begin);

                try
                {
                    // Try using the declared encoding
                    readXml(mem, null, position, meta, readTagParams);
                }
                catch (Exception e) // Fallback to forcing UTF-8 when the declared encoding is invalid (e.g. "UTF - 8")
                {
                    Utils.TraceException(e, Log.LV_DEBUG);
                    mem.Seek(0, SeekOrigin.Begin);
                    position = new List<string> { "ixml" };
                    readXml(mem, Encoding.UTF8, position, meta, readTagParams);
                }
            }
        }

        private static void readXml(Stream mem, Encoding encoding, IList<string> position, MetaDataIO meta, ReadTagParams readTagParams)
        {
            bool inList = false;
            int listDepth = 0;
            int listCounter = 1;

            using (XmlReader reader = (null == encoding) ? XmlReader.Create(mem) : XmlReader.Create(new StreamReader(mem, encoding)))
            {
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element: // Element start
                            string key = reader.Name;
                            if (inList && reader.Depth == listDepth + 1 && !key.EndsWith("COUNT", StringComparison.OrdinalIgnoreCase))
                            {
                                key = key + "[" + listCounter + "]";
                                listCounter++;
                            }
                            if (!key.Equals("BWFXML", StringComparison.OrdinalIgnoreCase)) position.Add(key);
                            if (!inList && reader.Name.EndsWith("LIST", StringComparison.OrdinalIgnoreCase))
                            {
                                inList = true;
                                listDepth = reader.Depth;
                                listCounter = 1;
                            }
                            break;

                        case XmlNodeType.Text:
                            if (!string.IsNullOrEmpty(reader.Value))
                            {
                                meta.SetMetaField(getPosition(position), reader.Value, readTagParams.ReadAllMetaFrames);
                            }
                            break;

                        case XmlNodeType.EndElement: // Element end
                            position.RemoveAt(position.Count - 1);
                            if (inList && reader.Name.EndsWith("LIST", StringComparison.OrdinalIgnoreCase))
                            {
                                inList = false;
                            }
                            break;
                    }
                }
            }
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
