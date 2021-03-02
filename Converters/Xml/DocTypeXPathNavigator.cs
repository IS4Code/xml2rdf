using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace IS4.RDF.Converters.Xml
{
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
