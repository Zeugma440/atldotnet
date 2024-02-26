using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using ATL.AudioData.IO;
using ATL.Logging;
using Commons;

namespace ATL.AudioData
{
    internal class XmlArray
    {
        private readonly string prefix;
        private readonly string displayPrefix;
        private readonly Func<string, bool> isCollection;
        private readonly Func<string, bool> isIndex;
        private readonly ISet<string> structuralAttributes = new HashSet<string>();

        public XmlArray(
            string prefix,
            string displayPrefix,
            Func<string, bool> isCollection,
            Func<string, bool> isIndex
            )
        {
            this.prefix = prefix;
            this.displayPrefix = displayPrefix;
            this.isCollection = isCollection;
            this.isIndex = isIndex;
        }

        public void setStructuralAttributes(ISet<string> attrs)
        {
            foreach (var attr in attrs) structuralAttributes.Add(attr.ToLower());
        }

        public void FromStream(Stream source, MetaDataIO meta, MetaDataIO.ReadTagParams readTagParams, long chunkSize)
        {
            Stack<string> position = new Stack<string>();
            position.Push(displayPrefix);

            long initialOffset = source.Position;
            int nbSkipBegin = StreamUtils.SkipValues(source, new[] { 10, 13, 32, 0 }); // Ignore leading CR, LF, whitespace, null
            source.Seek(initialOffset + chunkSize, SeekOrigin.Begin);
            int nbSkipEnd = StreamUtils.SkipValuesEnd(source, new[] { 10, 13, 32, 0, 0xFF }); // Ignore ending CR, LF, whitespace, null, 0xFF
            source.Seek(initialOffset + nbSkipBegin, SeekOrigin.Begin);

            using MemoryStream mem = new MemoryStream((int)chunkSize - nbSkipBegin - nbSkipEnd);
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
                position = new Stack<string>();
                position.Push(displayPrefix);
                readXml(mem, Encoding.UTF8, position, meta, readTagParams);
            }
        }

        private void readXml(
            Stream mem,
            Encoding encoding,
            Stack<string> position,
            MetaDataIO meta,
            MetaDataIO.ReadTagParams readTagParams)
        {
            Stack<int> listDepth = new Stack<int>();
            IDictionary<int, int> listCounter = new Dictionary<int, int>();

            using XmlReader reader = null == encoding ? XmlReader.Create(mem) : XmlReader.Create(new StreamReader(mem, encoding));
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element: // Element start
                        // Core element
                        string key = reader.Name;
                        if (listDepth.Count > 0 && reader.Depth == listDepth.Peek() + 1 && !isIndex.Invoke(key))
                        {
                            var counter = listCounter[listDepth.Peek()];
                            key = key + "[" + counter + "]";
                            listCounter[listDepth.Peek()] = counter + 1;
                        }
                        if (!key.Equals(prefix, StringComparison.OrdinalIgnoreCase) && !reader.IsEmptyElement) position.Push(key);
                        if (isCollection.Invoke(reader.Name))
                        {
                            listDepth.Push(reader.Depth);
                            listCounter[reader.Depth] = 1;
                        }
                        // Attributes
                        if (reader.HasAttributes)
                        {
                            var here = reader.IsEmptyElement ? "." + key : "";
                            for (int i = 0; i < reader.AttributeCount; i++)
                            {
                                reader.MoveToAttribute(i);
                                if (!string.IsNullOrEmpty(reader.Value))
                                {
                                    meta.SetMetaField(string.Join(".", position.ToArray().Reverse()) + here + "." + reader.Name, reader.Value, readTagParams.ReadAllMetaFrames);
                                }
                            }
                        }
                        break;

                    case XmlNodeType.Text:
                        if (!string.IsNullOrEmpty(reader.Value))
                        {
                            meta.SetMetaField(string.Join(".", position.ToArray().Reverse()), reader.Value, readTagParams.ReadAllMetaFrames);
                        }
                        break;

                    case XmlNodeType.EndElement: // Element end
                        position.Pop();
                        if (listDepth.Count > 0 && isCollection.Invoke(reader.Name))
                        {
                            listDepth.Pop();
                        }
                        break;
                }
            }
        }

        public int ToStream(BinaryWriter w, bool isLittleEndian, MetaDataIO meta)
        {
            IDictionary<string, string> additionalFields = meta.AdditionalFields;
            XmlWriterSettings settings = new XmlWriterSettings
            {
                CloseOutput = false,
                Encoding = Encoding.UTF8
            };

            // Isolate all namespaces
            var nsKeys = meta.AdditionalFields.Keys.Where(k => k.Contains("xmlns:")).ToHashSet();
            var namespaces = new Dictionary<string, string>();
            foreach (var nsKey in nsKeys)
            {
                namespaces.Add(nsKey.Split(':')[^1], additionalFields[nsKey]);
            }

            using XmlWriter writer = XmlWriter.Create(w.BaseStream, settings);
            writer.WriteStartDocument();
            string name = prefix;
            string pfx = null;
            int pfxIdfx = name.IndexOf(':');
            if (pfxIdfx > -1)
            {
                pfx = name[..pfxIdfx];
                name = name[(pfxIdfx + 1)..];
            }
            if (null == pfx) writer.WriteStartElement(name);
            else writer.WriteStartElement(pfx, name, namespaces[pfx]);

            // Register all namespaces on the top level element
            foreach (var ns in namespaces)
            {
                writer.WriteAttributeString(ns.Key,
                    "http://www.w3.org/2000/xmlns/",
                    ns.Value);
            }

            // Path notes : key = node path; value = node name
            Dictionary<string, string> pathNodes = new Dictionary<string, string>();
            List<string> previousPathNodes = new List<string>();
            foreach (var key in additionalFields.Keys
                         .Where(key => key.StartsWith(displayPrefix + "."))
                         .Where(key => !nsKeys.Contains(key))
                     )
            {
                // Create the list of path nodes
                List<string> singleNodes = new List<string>(key.Split('.'));
                singleNodes.RemoveAt(0);// Remove the root node
                StringBuilder nodePrefix = new StringBuilder();
                pathNodes.Clear();
                foreach (string nodeName in singleNodes)
                {
                    nodePrefix.Append('.').Append(nodeName);
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
                // Open all new (i.e. non present in previous path) nodes
                foreach (string nodePath in pathNodes.Keys)
                {
                    if (!previousPathNodes.Contains(nodePath))
                    {
                        var subkey = pathNodes[nodePath];
                        if (subkey.Equals(singleNodes[^1])) continue; // Last node is a leaf, not a node

                        if (subkey.Contains('[')) subkey = subkey[..subkey.IndexOf('[')]; // Remove [x]'s

                        name = subkey;
                        pfx = null;
                        pfxIdfx = name.IndexOf(':');
                        if (pfxIdfx > -1)
                        {
                            pfx = name[..pfxIdfx];
                            name = name[(pfxIdfx + 1)..];
                        }
                        if (null == pfx) writer.WriteStartElement(name);
                        else writer.WriteStartElement(pfx, name, namespaces[pfx]);
                    }
                }
                // Write the last node (=leaf) as a proper value if it does not belong to structural attributes
                name = singleNodes[^1];
                pfx = null;
                pfxIdfx = name.IndexOf(':');
                if (pfxIdfx > -1)
                {
                    pfx = name[..pfxIdfx];
                    name = name[(pfxIdfx + 1)..];
                }
                if (structuralAttributes.Contains(singleNodes[^1].ToLower()))
                {
                    if (null == pfx) writer.WriteAttributeString(name, additionalFields[key]);
                    else writer.WriteAttributeString(pfx, name, namespaces[pfx], additionalFields[key]);
                }
                else
                {
                    if (null == pfx) writer.WriteElementString(name, additionalFields[key]);
                    else writer.WriteElementString(pfx, name, namespaces[pfx], additionalFields[key]);
                }

                previousPathNodes = pathNodes.Keys.ToList();
            }

            // Close all terminated paths
            for (int i = previousPathNodes.Count - 2; i >= 0; i--) writer.WriteEndElement();

            return 14;
        }
    }
}
