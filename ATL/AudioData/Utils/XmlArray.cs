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
        private readonly IDictionary<string, string> defaultNamespaces = new Dictionary<string, string>();
        // Specific anchors for namespaces
        //   Key = Anchor node name
        //   Value = Namespace name
        // NB1 : If no anchor is defined for a given namespace, it will be written on the root element
        // NB2 : If no namespace is defined for a given node name, all namespaces without an explicit anchor will be written on that node
        private readonly IDictionary<string, ISet<string>> namespaceAnchors = new Dictionary<string, ISet<string>>();

        private sealed class Node
        {
            public string Prefix { get; }
            public string Name { get; }
            public string FullName => (Prefix?.Length > 0) ? Prefix + ":" + Name : Name;

            public Node(string prefix, string name)
            {
                Prefix = prefix;
                Name = name;
            }
        }


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
            structuralAttributes.Clear();
            foreach (var attr in attrs) structuralAttributes.Add(attr.ToLower());
        }

        public void setDefaultNamespaces(IDictionary<string, string> defaultNs)
        {
            defaultNamespaces.Clear();
            foreach (var ns in defaultNs) defaultNamespaces.Add(ns.Key, ns.Value);
        }

        public void setNamespaceAnchors(IDictionary<string, ISet<string>> nsAnchors)
        {
            namespaceAnchors.Clear();
            foreach (var a in nsAnchors) namespaceAnchors.Add(a.Key, a.Value);
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
                                // Don't show namespaces as metadata for simplicity's sake
                                if (!string.IsNullOrEmpty(reader.Value) && !reader.Name.StartsWith("xmlns:"))
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

        public int ToStream(Stream s, MetaDataHolder meta)
        {
            // Filter eligible additionalData
            IDictionary<string, string> additionalFields = meta.AdditionalFields
                .Where(f => f.Key.StartsWith(displayPrefix + ".", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(x => x.Key, x => x.Value);

            // Isolate all namespaces provided as input
            var nsKeys = meta.AdditionalFields.Keys.Where(k => k.Contains("xmlns:")).ToHashSet();
            var namespaces = nsKeys.ToDictionary(nsKey => nsKey.Split(':')[^1], nsKey => additionalFields[nsKey]);

            // Complete with default namespaces if there's any missing
            foreach (var defaultNs in defaultNamespaces)
            {
                if (!namespaces.ContainsKey(defaultNs.Key)) namespaces.Add(defaultNs.Key, defaultNs.Value);
            }

            // Detect all used namespaces
            ISet<string> usedNamespaces = new HashSet<string>();
            var nonNsKeys = meta.AdditionalFields.Keys.Where(k => !k.Contains("xmlns:")).ToHashSet();
            foreach (var nsKey in nonNsKeys)
            {
                var parts = nsKey.Split('.').Where(s1 => s1.Contains(':'));
                foreach (var part in parts) usedNamespaces.Add(part.Split(':')[0]);
            }

            // Start writing
            XmlWriterSettings settings = new XmlWriterSettings
            {
                CloseOutput = false,
                Encoding = Encoding.UTF8
            };

            using XmlWriter writer = XmlWriter.Create(s, settings);
            writer.WriteStartDocument();
            var node = parseNode(prefix);
            if (null == node.Prefix) writer.WriteStartElement(node.Name);
            else writer.WriteStartElement(node.Prefix, node.Name, namespaces[node.Prefix]);

            // Namespaces explicitly attached to current element
            namespaceAnchors.TryGetValue(node.FullName, out var namespacesToAnchor);
            if (namespacesToAnchor != null && namespacesToAnchor.Any(string.IsNullOrEmpty))
            {
                namespacesToAnchor.Clear();
                namespacesToAnchor.UnionWith(usedNamespaces);
            }

            // Register used namespaces on the top level element if declared or unanchored
            foreach (var ns in usedNamespaces)
            {
                if (!namespaces.ContainsKey(ns))
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Namespace not found : " + ns);
                    continue;
                }
                if (null == namespacesToAnchor || !namespacesToAnchor.Contains(ns))
                {
                    // Not attached to any element => Attached to root
                    var isAnchored = namespaceAnchors.Values.Any(s2 => s2.Any(v => string.IsNullOrEmpty(v) || v.Equals(ns, StringComparison.OrdinalIgnoreCase)));
                    if (isAnchored) continue;
                }

                writer.WriteAttributeString(ns, "http://www.w3.org/2000/xmlns/", namespaces[ns]);
            }

            // Path notes : key = node path; value = node name
            Dictionary<string, string> pathNodes = new Dictionary<string, string>();
            List<string> previousPathNodes = new List<string>();
            Stack<string> openedNodes = new Stack<string>();
            var valuesWritten = 0;
            foreach (var key in additionalFields.Keys
                         .Where(key => key.StartsWith(displayPrefix + "."))
                         .Where(key => !nsKeys.Contains(key))
                    )
            {
                // Create the list of path nodes for the current element
                List<string> singleNodes = new List<string>(key.Split('.'));
                singleNodes.RemoveAt(0);// Remove the root node
                StringBuilder nodePrefix = new StringBuilder();
                pathNodes.Clear();
                foreach (string nodeName in singleNodes)
                {
                    nodePrefix.Append('.').Append(nodeName);
                    pathNodes.Add(nodePrefix.ToString(), nodeName);
                }

                // Close all terminated (i.e. previously opened and not present in current path) nodes in reverse order
                if (openedNodes.Count > 0)
                {
                    var openedNode = openedNodes.Peek();
                    while (!pathNodes.ContainsKey(openedNode))
                    {
                        writer.WriteEndElement();
                        openedNodes.Pop();
                        if (0 == openedNodes.Count) break;
                        openedNode = openedNodes.Peek();
                    }
                }

                // Open all new (i.e. non present in previous path) nodes
                foreach (string nodePath in pathNodes.Keys)
                {
                    if (previousPathNodes.Contains(nodePath)) continue;

                    var subkey = pathNodes[nodePath];
                    if (subkey.Equals(singleNodes[^1])) continue; // Last node is a leaf, not a node
                    node = parseNode(subkey);

                    // Namespaces explicitly attached to current element
                    namespaceAnchors.TryGetValue(node.FullName, out namespacesToAnchor);
                    if (namespacesToAnchor != null && namespacesToAnchor.Any(string.IsNullOrEmpty))
                    {
                        namespacesToAnchor.Clear();
                        namespacesToAnchor.UnionWith(usedNamespaces);
                    }

                    openedNodes.Push(nodePath);
                    if (null == node.Prefix) writer.WriteStartElement(node.Name);
                    else writer.WriteStartElement(node.Prefix, node.Name, namespaces[node.Prefix]);

                    // Attach anchored namespaces
                    if (namespacesToAnchor != null)
                        foreach (var ns in namespacesToAnchor.Where(ns => namespaces.ContainsKey(ns)))
                        {
                            writer.WriteAttributeString(ns, "http://www.w3.org/2000/xmlns/", namespaces[ns]);
                        }
                }

                // Write the last node (=leaf) as a proper value if it does not belong to structural attributes
                node = parseNode(singleNodes[^1]);
                if (structuralAttributes.Contains(singleNodes[^1].ToLower()))
                {
                    if (null == node.Prefix) writer.WriteAttributeString(node.Name, additionalFields[key]);
                    else writer.WriteAttributeString(node.Prefix, node.Name, namespaces[node.Prefix], additionalFields[key]);
                }
                else if (previousPathNodes.Contains(key[displayPrefix.Length..]))
                {
                    writer.WriteString(additionalFields[key]);
                }
                else
                {
                    // ElementString is just a Helper for StartElement + String + EndElement
                    if (null == node.Prefix) writer.WriteElementString(node.Name, additionalFields[key]);
                    else writer.WriteElementString(node.Prefix, node.Name, namespaces[node.Prefix], additionalFields[key]);
                }
                valuesWritten++;

                previousPathNodes = pathNodes.Keys.ToList();
            }

            // Close all terminated paths
            while (openedNodes.Count > 0)
            {
                writer.WriteEndElement();
                openedNodes.Pop();
            }

            return valuesWritten;
        }

        /**
         * Returns prefix and name; prefix might be null
         */
        private static Node parseNode(string key)
        {
            string pfx = null;
            string name = key;
            if (name.Contains('[')) name = name[..name.IndexOf('[')]; // Remove [x]'s
            int pfxIdfx = name.IndexOf(':');
            if (pfxIdfx > -1)
            {
                pfx = name[..pfxIdfx];
                name = name[(pfxIdfx + 1)..];
            }
            return new Node(pfx, name);
        }
    }
}
