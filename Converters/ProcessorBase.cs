using System;
using System.Collections.Generic;
using System.Xml;
using VDS.RDF;

namespace IS4.RDF.Converters
{
    public abstract class ProcessorBase
    {
        internal static readonly Uri xmlNSUri = new Uri("http://www.w3.org/XML/1998/namespace");
        internal static readonly Uri xsiNSUri = new Uri("http://www.w3.org/2001/XMLSchema-instance");
        internal const string xlinkNS = "http://www.w3.org/1999/xlink";
        internal const string xiNS = "http://www.w3.org/2001/XInclude";

        internal const string rdfNS = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        internal const string rdfsNS = "http://www.w3.org/2000/01/rdf-schema#";
        internal const string xsdNS = "http://www.w3.org/2001/XMLSchema#";

        internal static readonly Uri XMLLiteral = new Uri(rdfNS + "XMLLiteral");
        internal static readonly Uri langString = new Uri(rdfNS + "langString");
        internal static readonly Uri xstring = new Uri(xsdNS + "string");
        internal static readonly Uri anyURI = new Uri(xsdNS + "anyURI");
        internal static readonly Uri NCName = new Uri(xsdNS + "NCName");
        internal static readonly Uri QName = new Uri(xsdNS + "QName");
        internal static readonly Uri xpublic = new Uri(xsdNS + "public");
        internal static readonly Uri ID = new Uri(xsdNS + "ID");
        internal static readonly Uri IDREF = new Uri(xsdNS + "IDREF");
        internal static readonly Uri NOTATION = new Uri(xsdNS + "NOTATION");

        internal readonly IUriNode a, List, first, rest, nil, value, label, comment, seeAlso, isDefinedBy, subClassOf, member;

        readonly Dictionary<string, int> nameIdCounter = new Dictionary<string, int>(StringComparer.Ordinal);

        protected IEqualityComparer<Uri> UriComparer { get; }

        public ProcessorBase(INodeFactory rdf)
        {
            a = rdf.CreateUriNode(new Uri(rdfNS + "type"));
            List = rdf.CreateUriNode(new Uri(rdfNS + "List"));
            first = rdf.CreateUriNode(new Uri(rdfNS + "first"));
            rest = rdf.CreateUriNode(new Uri(rdfNS + "rest"));
            nil = rdf.CreateUriNode(new Uri(rdfNS + "nil"));
            value = rdf.CreateUriNode(new Uri(rdfNS + "value"));
            label = rdf.CreateUriNode(new Uri(rdfsNS + "label"));
            comment = rdf.CreateUriNode(new Uri(rdfsNS + "comment"));
            seeAlso = rdf.CreateUriNode(new Uri(rdfsNS + "seeAlso"));
            isDefinedBy = rdf.CreateUriNode(new Uri(rdfsNS + "isDefinedBy"));
            subClassOf = rdf.CreateUriNode(new Uri(rdfsNS + "subClassOf"));
            member = rdf.CreateUriNode(new Uri(rdfsNS + "member"));
            
            UriComparer = new UriComparer();
        }

        internal string GetUniqueName(string prefix)
        {
            if(!nameIdCounter.TryGetValue(prefix, out int counter))
            {
                counter = 0;
            }
            nameIdCounter[prefix] = ++counter;
            return prefix + counter;
        }

        protected Uri GetFullUri(XmlQualifiedName xmlName)
        {
            return xmlName != null ? UriTools.ComposeUri(new Uri(xmlName.Namespace), xmlName.Name) : null;
        }
    }
}
