using ATL.AudioData;
using ATL.AudioData.IO;

namespace ATL.test
{
    [TestClass]
    public class XmlArrayTest
    {
        private static string getFrom(string name)
        {
            var dataPath = TestUtils.GetResourceLocationRoot() + "_Xml" + Path.DirectorySeparatorChar + name;
            using var source = new FileStream(dataPath, FileMode.Open, FileAccess.Read);

            var reader = new StreamReader(source);
            return reader.ReadToEnd().ReplaceLineEndings("").Replace("\t", "");
        }

        [TestMethod]
        public void XmlArray_writeBasic()
        {
            var xmlArray = new XmlArray(
                "root",
                "test",
                _ => false,
                _ => false
            );

            TagHolder holder = new TagHolder();
            var data = new Dictionary<string, string>
            {
                ["test.one"] = "aaa",
                ["test.two"] = "bbb"
            };
            holder.AdditionalFields = data;

            var memStream = new MemoryStream();
            xmlArray.ToStream(memStream, holder);
            var reader = new StreamReader(memStream);
            memStream.Position = 0;
            string result = reader.ReadToEnd();

            Assert.IsTrue(result.Length > 0);

            var expected = getFrom("basic.xml");
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void XmlArray_writeAttributes()
        {
            var xmlArray = new XmlArray(
                "root",
                "test",
                _ => false,
                _ => false
            );
            xmlArray.setStructuralAttributes(new HashSet<string> { "hey", "PIP" });

            TagHolder holder = new TagHolder();
            var data = new Dictionary<string, string>
            {
                ["test.one.hey"] = "ho",
                ["test.one"] = "aaa",
                ["test.two.pip"] = "boy",
                ["test.two"] = "bbb"
            };
            holder.AdditionalFields = data;

            var memStream = new MemoryStream();
            xmlArray.ToStream(memStream, holder);
            var reader = new StreamReader(memStream);
            memStream.Position = 0;
            string result = reader.ReadToEnd();

            Assert.IsTrue(result.Length > 0);

            var expected = getFrom("attributes.xml");
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void XmlArray_writeCollections()
        {
            var xmlArray = new XmlArray(
                "root",
                "test",
                e => e.EndsWith("LIST", StringComparison.OrdinalIgnoreCase),
                _ => false
            );

            TagHolder holder = new TagHolder();
            var data = new Dictionary<string, string>
            {
                ["test.one"] = "aaa",
                ["test.two"] = "bbb",
                ["test.theList.elt[0].value"] = "11",
                ["test.theList.elt[1].value"] = "22",
                ["test.theList.elt[2].value"] = "33"
            };
            holder.AdditionalFields = data;

            var memStream = new MemoryStream();
            xmlArray.ToStream(memStream, holder);
            var reader = new StreamReader(memStream);
            memStream.Position = 0;
            string result = reader.ReadToEnd();

            Assert.IsTrue(result.Length > 0);

            var expected = getFrom("collection.xml");
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void XmlArray_writeRootNs()
        {
            IDictionary<string, string> DEFAULT_NAMESPACES = new Dictionary<string, string> { { "pap", "test:ns:meta/" } };
            var xmlArray = new XmlArray(
                "root",
                "test",
                _ => false,
                _ => false
            );
            // No namespace anchors (=> default anchor goes to root)
            xmlArray.setDefaultNamespaces(DEFAULT_NAMESPACES);

            TagHolder holder = new TagHolder();
            var data = new Dictionary<string, string>
            {
                ["test.pap:one"] = "aaa",
                ["test.pap:two"] = "bbb"
            };
            holder.AdditionalFields = data;

            var memStream = new MemoryStream();
            xmlArray.ToStream(memStream, holder);
            var reader = new StreamReader(memStream);
            memStream.Position = 0;
            string result = reader.ReadToEnd();

            Assert.IsTrue(result.Length > 0);

            var expected = getFrom("rootNs.xml");
            Assert.AreEqual(expected, result);


            // All namespaces explicitly anchored to root
            IDictionary<string, ISet<string>> NAMESPACE_ANCHORS = new Dictionary<string, ISet<string>> { { "root", new HashSet<string> { "" } } };
            xmlArray.setNamespaceAnchors(NAMESPACE_ANCHORS);

            memStream = new MemoryStream();
            xmlArray.ToStream(memStream, holder);
            reader = new StreamReader(memStream);
            memStream.Position = 0;
            result = reader.ReadToEnd();

            Assert.IsTrue(result.Length > 0);

            Assert.AreEqual(expected, result);


            // Specific namespace explicitly anchored to root
            IDictionary<string, ISet<string>> NAMESPACE_ANCHORS2 = new Dictionary<string, ISet<string>> { { "root", new HashSet<string> { "pap" } } };
            xmlArray.setNamespaceAnchors(NAMESPACE_ANCHORS2);

            memStream = new MemoryStream();
            xmlArray.ToStream(memStream, holder);
            reader = new StreamReader(memStream);
            memStream.Position = 0;
            result = reader.ReadToEnd();

            Assert.IsTrue(result.Length > 0);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void XmlArray_writeAnchoredNs()
        {
            IDictionary<string, string> DEFAULT_NAMESPACES = new Dictionary<string, string> { { "pap", "test:ns:meta/" } };
            // Anchor one specific ns to one specific node
            IDictionary<string, ISet<string>> NAMESPACE_ANCHORS = new Dictionary<string, ISet<string>> { { "container", new HashSet<string> { "pap" } } };
            var xmlArray = new XmlArray(
                "root",
                "test",
                _ => false,
                _ => false
            );
            xmlArray.setDefaultNamespaces(DEFAULT_NAMESPACES);
            xmlArray.setNamespaceAnchors(NAMESPACE_ANCHORS);

            TagHolder holder = new TagHolder();
            var data = new Dictionary<string, string>
            {
                ["test.container.pap:one"] = "aaa",
                ["test.container.pap:two"] = "bbb"
            };
            holder.AdditionalFields = data;

            var memStream = new MemoryStream();
            xmlArray.ToStream(memStream, holder);
            var reader = new StreamReader(memStream);
            memStream.Position = 0;
            string result = reader.ReadToEnd();

            Assert.IsTrue(result.Length > 0);

            var expected = getFrom("anchoredNs.xml");
            Assert.AreEqual(expected, result);

            // Anchor all ns'es to one specific node
            IDictionary<string, ISet<string>> NAMESPACE_ANCHORS2 = new Dictionary<string, ISet<string>> { { "container", new HashSet<string> { "" } } };
            xmlArray.setNamespaceAnchors(NAMESPACE_ANCHORS2);

            memStream = new MemoryStream();
            xmlArray.ToStream(memStream, holder);
            reader = new StreamReader(memStream);
            memStream.Position = 0;
            result = reader.ReadToEnd();

            Assert.IsTrue(result.Length > 0);

            Assert.AreEqual(expected, result);
        }
    }
}
