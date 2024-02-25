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
    }
}
