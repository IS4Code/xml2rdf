using IS4.RDF.Converters.Xml;
using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;

namespace IS4.RDF.Converters
{
    public class XPathNodeConverter : INodeConverter<IXPathNavigable>, INodeConverter<XPathNavigator>
    {
        public TNode Convert<TNode, TUriNode>(IXPathNavigable navigable, IXmlNodeProcessor<TNode, TUriNode> processor)
            where TUriNode : TNode
        {
            return Convert(navigable.CreateNavigator(), processor);
        }

        public TNode Convert<TNode, TUriNode>(XPathNavigator navigator, IXmlNodeProcessor<TNode, TUriNode> processor)
            where TUriNode : TNode
        {
            return XPathValue(processor, navigator, default, null, default);
        }

        private IEnumerator<TNode> XPathContents<TNode, TUriNode>(IXmlNodeProcessor<TNode, TUriNode> processor, XPathNavigator navigator, TUriNode baseNode, Uri baseUri, TUriNode defaultNamespace)
             where TUriNode : TNode
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

        private TNode XPathValue<TNode, TUriNode>(IXmlNodeProcessor<TNode, TUriNode> processor, XPathNavigator navigator, TUriNode baseNode, Uri originalBaseUri, TUriNode defaultNamespace)
            where TUriNode : TNode
        {
            var wrapper = new XPathNavigatorWrapper(navigator);
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

            public XPathNavigatorWrapper(XPathNavigator navigator)
            {
                Navigator = navigator;
            }

            public string Language => Navigator.XmlLang;

            public Uri BaseUri => String.IsNullOrEmpty(Navigator.BaseURI) ? null : new Uri(Navigator.BaseURI);

            public bool IsDefault => false;

            public XmlQualifiedName QualifiedName => new XmlQualifiedName(Navigator.LocalName, Navigator.NamespaceURI);

            public string LocalName => Navigator.LocalName;

            public string PrefixedName => Navigator.Name;

            public Uri Namespace => String.IsNullOrEmpty(Navigator.NamespaceURI) ? null : new Uri(Navigator.NamespaceURI);

            public string Prefix => Navigator.Prefix;

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
                        return Navigator.MoveToFirstNamespace() || Navigator.MoveToFirstAttribute();
                    case XPathNodeType.Namespace:
                        return Navigator.MoveToNextNamespace() || (Navigator.MoveToParent() && Navigator.MoveToFirstAttribute());
                    default:
                        return Navigator.MoveToNextAttribute();
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
