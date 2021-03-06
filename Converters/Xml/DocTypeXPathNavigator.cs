using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace IS4.RDF.Converters.Xml
{
    /// <summary>
    /// Provides a cursor model to navigate XML data as a <see cref="XPathNavigator"/> and also stores the document type pertaining to the root node.
    /// </summary>
    public abstract class DocTypeXPathNavigator : XPathNavigator
    {
        public abstract XDocumentType DocumentType { get; }

        public override void WriteSubtree(XmlWriter writer)
        {
            if(NodeType == XPathNodeType.Root)
            {
                var doctype = DocumentType;
                if(doctype != null)
                {
                    writer.WriteDocType(doctype.Name, doctype.PublicId, doctype.SystemId, doctype.InternalSubset);
                }
            }
            base.WriteSubtree(writer);
        }
    }
}
