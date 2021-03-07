using IS4.RDF.Converters.Xml;
using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;

namespace IS4.RDF.Converters
{
    /// <summary>
    /// Converts XML data from an instance of <see cref="IXPathNavigable"/> or <see cref="XPathNavigator"/>.
    /// </summary>
    public class XPathNodeConverter : INodeConverter<IXPathNavigable>, INodeConverter<XPathNavigator>
    {
        public bool ExposeNamespaceNodes { get; set; }

        public TNode Convert<TNode>(IXPathNavigable navigable, IXmlNodeProcessor<TNode> processor)
        {
            return Convert(navigable.CreateNavigator(), processor);
        }

        public TNode Convert<TNode>(XPathNavigator navigator, IXmlNodeProcessor<TNode> processor)
        {
            return XPathValue(processor, navigator, default, null, default);
        }

        private IEnumerator<TNode> XPathContents<TNode>(IXmlNodeProcessor<TNode> processor, XPathNavigator navigator, TNode baseNode, Uri baseUri, TNode defaultNamespace)
        {
            if(navigator.MoveToFirstChild())
            {
                do
                {
                    yield return XPathValue(processor, navigator, baseNode, baseUri, defaultNamespace);
                }while(navigator.MoveToNext());
                navigator.MoveToParent();
            }
        }

        private TNode XPathValue<TNode>(IXmlNodeProcessor<TNode> processor, XPathNavigator navigator, TNode baseNode, Uri originalBaseUri, TNode defaultNamespace)
        {
            var wrapper = new XPathNavigatorWrapper(navigator, ExposeNamespaceNodes);
            switch(navigator.NodeType)
            {
                case XPathNodeType.Root:
                    return processor.ProcessDocument(wrapper, newBaseNode => XPathContents(processor, navigator, newBaseNode, wrapper.BaseUri, defaultNamespace));

                case XPathNodeType.Whitespace:
                    return processor.ProcessWhitespace(wrapper, false);

                case XPathNodeType.SignificantWhitespace:
                    return processor.ProcessWhitespace(wrapper, true);

                case XPathNodeType.Text:
                    return processor.ProcessText(wrapper, false);

                case XPathNodeType.Comment:
                    return processor.ProcessComment(wrapper);

                case XPathNodeType.ProcessingInstruction:
                    return processor.ProcessProcessingInstruction(wrapper, defaultNamespace);

                case XPathNodeType.Element:
                    return processor.ProcessElement(wrapper, baseNode, originalBaseUri ?? wrapper.BaseUri, defaultNamespace, innerBaseNode => XPathContents(processor, navigator, innerBaseNode, wrapper.BaseUri, defaultNamespace));

                default:
                case XPathNodeType.Attribute:
                case XPathNodeType.Namespace:
                    throw new NotSupportedException();
            }
        }
        
        struct XPathNavigatorWrapper : IXmlProvider
        {
            public XPathNavigator Navigator { get; }
            readonly bool exposeNamespaces;

            public XPathNavigatorWrapper(XPathNavigator navigator, bool exposeNamespaces)
            {
                Navigator = navigator;
                this.exposeNamespaces = exposeNamespaces;
            }

            public string Language => Navigator.XmlLang;

            public Uri BaseUri => String.IsNullOrEmpty(Navigator.BaseURI) ? null : new Uri(Navigator.BaseURI);

            public bool IsDefault => false;

            public XmlQualifiedName QualifiedName =>
                Navigator.NodeType == XPathNodeType.Namespace ?
                String.IsNullOrEmpty(Navigator.Name) ? new XmlQualifiedName("xmlns", "http://www.w3.org/2000/xmlns/") : new XmlQualifiedName(Navigator.Name, "http://www.w3.org/2000/xmlns/") :
                new XmlQualifiedName(Navigator.LocalName, Navigator.NamespaceURI);

            public string LocalName =>
                Navigator.NodeType == XPathNodeType.Namespace && String.IsNullOrEmpty(Navigator.Name) ?
                "xmlns" :
                Navigator.LocalName;

            public string PrefixedName =>
                Navigator.NodeType == XPathNodeType.Namespace ?
                String.IsNullOrEmpty(Navigator.Name) ? "xmlns" : "xmlns:" + Navigator.Name :
                Navigator.Name;

            public Uri Namespace =>
                Navigator.NodeType == XPathNodeType.Namespace ?
                new Uri("http://www.w3.org/2000/xmlns/") :
                String.IsNullOrEmpty(Navigator.NamespaceURI) ? null : new Uri(Navigator.NamespaceURI);

            public string Prefix => Navigator.NodeType == XPathNodeType.Namespace && !String.IsNullOrEmpty(Navigator.Name) ? "xmlns" : Navigator.Prefix;

            public XmlQualifiedName TypeName {
                get {
                    XmlSchemaType type = Navigator.XmlType;
                    if(type != null)
                    {
                        while(type != null && type.QualifiedName.IsEmpty)
                        {
                            type = type.BaseXmlSchemaType;
                        }
                        if(type != null)
                        {
                            return type.QualifiedName;
                        }
                    }
                    return null;
                }
            }

            public Type Type => Navigator.ValueType;

            public string Value => Navigator.Value;

            public bool IsEmpty => Navigator.IsEmptyElement;

            public bool MoveToNextAttribute()
            {
                switch(Navigator.NodeType)
                {
                    case XPathNodeType.Element:
                        return (exposeNamespaces && Navigator.MoveToFirstNamespace()) || Navigator.MoveToFirstAttribute();
                    case XPathNodeType.Namespace:
                        return Navigator.MoveToNextNamespace() || (Navigator.MoveToParent() && Navigator.MoveToFirstAttribute());
                    default:
                        if(!Navigator.MoveToNextAttribute())
                        {
                            Navigator.MoveToParent();
                            return false;
                        }
                        return true;
                }
            }

            public IDictionary<string, string> GetNamespacesInScope(XmlNamespaceScope scope)
            {
                return Navigator.GetNamespacesInScope(scope);
            }

            public string LookupNamespace(string prefix)
            {
                return Navigator.LookupNamespace(prefix);
            }

            public string LookupPrefix(string namespaceName)
            {
                return Navigator.LookupPrefix(namespaceName);
            }
        }
    }
}
