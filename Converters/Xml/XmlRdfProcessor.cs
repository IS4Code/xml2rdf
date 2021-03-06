using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml;
using VDS.RDF;
using VDS.RDF.Parsing.Handlers;

namespace IS4.RDF.Converters.Xml
{
    public class XmlRdfProcessor : ProcessorBase, IXmlNodeProcessor<INode>
    {
        readonly IRdfHandler rdf;
        readonly string linkName;
        readonly Dictionary<INode, Uri> localUris = new Dictionary<INode, Uri>();
        readonly Dictionary<string, INode> uriBlankNodes = new Dictionary<string, INode>();

        internal const string LocalNamespace = "%23local";

        public bool UseDtdAsDefaultNamespace { get; set; } = true;
        public WhitespaceHandling WhitespaceHandling { get; set; } = WhitespaceHandling.Significant;
        public bool ExportDefault { get; set; } = false;

        public IDictionary<string, Uri> ProcessingInstructionMapping { get; } = new Dictionary<string, Uri>();

        public XmlRdfProcessor(Graph graph, XmlPlaceholderResolver linkResolver = null) : this(new GraphHandler(graph), linkResolver)
        {

        }

        public XmlRdfProcessor(IRdfHandler rdfHandler, XmlPlaceholderResolver entityResolver = null) : base(rdfHandler)
        {
            rdf = rdfHandler;
            linkName = entityResolver?.InstructionName;

            ProcessingInstructionMapping["xml-stylesheet"] = xmlNSUri;

            rdf.StartRdf();

            rdf.HandleNamespace("rdf", new Uri(rdfNS));
            rdf.HandleNamespace("rdfs", new Uri(rdfsNS));
            rdf.HandleNamespace("xsd", new Uri(xsdNS));
        }

        void IDisposable.Dispose()
        {
            rdf.EndRdf(true);
        }

        INode IXmlNodeProcessor<INode>.ProcessDocument<TProvider>(TProvider provider, Func<INode, IEnumerator<INode>> content)
        {
            var baseNode = MakeUriNode(provider.BaseUri);
            rdf.HandleTriple(baseNode, value, MakeList(content(baseNode)) ?? nil);
            return baseNode;
        }

        void IXmlNodeProcessor<INode>.ProcessDocumentType(string publicId, string systemId, string internalSubset, bool useAsNamespace, INode baseNode, ref INode defaultNamespace)
        {
            INode baseType = null;
            if(publicId != null)
            {
                var publicNode = rdf.CreateUriNode(UriTools.CreatePublicId(publicId));
                rdf.HandleTriple(publicNode, label, rdf.CreateLiteralNode(publicId, xpublic));
                if(systemId != null)
                {
                    rdf.HandleTriple(publicNode, isDefinedBy, MakeAbsoluteNode(baseNode, systemId));
                }
                if(useAsNamespace && UseDtdAsDefaultNamespace) defaultNamespace = defaultNamespace ?? publicNode;
                baseType = publicNode;
            }else if(systemId != null)
            {
                var systemNode = MakeAbsoluteNode(baseNode, systemId);
                baseType = BlankNode("doctype");
                rdf.HandleTriple(baseType, isDefinedBy, systemNode);
                //if(useAsNamespace && UseDtdAsDefaultNamespace) defaultNamespace = defaultNamespace ?? systemNode;
            }
            INode subsetType = null;
            if(!String.IsNullOrWhiteSpace(internalSubset))
            {
                subsetType = rdf.CreateBlankNode("dtd");
                rdf.HandleTriple(subsetType, value, rdf.CreateLiteralNode(internalSubset));
                if(baseType != null)
                {
                    rdf.HandleTriple(subsetType, subClassOf, baseType);
                }
                baseType = subsetType;
            }
            if(baseType != null)
            {
                rdf.HandleTriple(baseNode, a, baseType);
            }
        }

        INode IXmlNodeProcessor<INode>.ProcessWhitespace<TProvider>(TProvider provider, bool significant)
        {
            if((significant && WhitespaceHandling == WhitespaceHandling.Significant) || WhitespaceHandling == WhitespaceHandling.All)
            {
                return ((IXmlNodeProcessor<INode>)this).ProcessText(provider, false);
            }
            return null;
        }

        INode IXmlNodeProcessor<INode>.ProcessText<TProvider>(TProvider provider, bool cdata)
        {
            var textValue = CreateLanguageLiteral(provider, provider.Value);
            if(cdata)
            {
                var cdataNode = BlankNode("cdata");
                rdf.HandleTriple(cdataNode, value, textValue);
                return cdataNode;
            }
            return textValue;
        }

        INode IXmlNodeProcessor<INode>.ProcessComment<TProvider>(TProvider provider)
        {
            var commentNode = BlankNode("comment");
            rdf.HandleTriple(commentNode, value, nil);
            rdf.HandleTriple(commentNode, comment, rdf.CreateLiteralNode(provider.Value));
            return commentNode;
        }

        INode IXmlNodeProcessor<INode>.ProcessProcessingInstruction<TProvider>(TProvider provider, INode defaultNamespace)
        {
            if(linkName == provider.LocalName)
            {
                return MakeUriNode(provider.BaseUri);
            }
            var procNode = BlankNode("proc");
            rdf.HandleTriple(procNode, value, nil);
            var datatypeNode = CreateProcessingInstructionType(provider, defaultNamespace);
            if(datatypeNode is IUriNode uriNode)
            {
                rdf.HandleTriple(procNode, comment, rdf.CreateLiteralNode(provider.Value, uriNode.Uri));
            }else{
                rdf.HandleTriple(procNode, comment, rdf.CreateLiteralNode(provider.Value));
            }
            return procNode;
        }

        struct XmlValueInfo
        {
            public bool? IsNil;
            public IUriNode DataType;
        }

        INode IXmlNodeProcessor<INode>.ProcessElement<TProvider>(TProvider provider, INode baseNode, Uri originalBaseUri, INode defaultNamespace, Func<INode, IEnumerator<INode>> content)
        {
            var empty = provider.IsEmpty;
            bool sameBase = provider.BaseUri == originalBaseUri;

            Action<INode> elementInit = null;
            var elementType = CreateElementType(provider, defaultNamespace);
            elementInit += n => rdf.HandleTriple(n, a, elementType);

            string id = null;
            INode innerBaseNode = baseNode;
            XmlValueInfo info = default;

            while(provider.MoveToNextAttribute())
            {
                if(provider.IsDefault && !ExportDefault) continue;
                var property = CreateAttributeType(provider, elementType);
                var value = CreateAttributeValue(provider);
                elementInit += n => rdf.HandleTriple(n, property, value);

                if(value is ILiteralNode literal && UriComparer.Equals(literal.DataType, ID))
                {
                    id = XmlConvert.VerifyNCName(provider.Value);
                }else if(UriComparer.Equals(provider.Namespace, xmlNSUri))
                {
                    switch(provider.LocalName)
                    {
                        case "id":
                            id = XmlConvert.VerifyNCName(provider.Value);
                            break;
                        case "base":
                            if(sameBase)
                            {
                                innerBaseNode = MakeAbsoluteNode(baseNode, provider.Value);
                            }
                            break;
                    }
                }else if(UriComparer.Equals(provider.Namespace, xsiNSUri))
                {
                    switch(provider.LocalName)
                    {
                        case "nil":
                            info.IsNil = XmlConvert.ToBoolean(provider.Value);
                            break;
                        case "type":
                            info.DataType = ResolveName(provider, XmlConvert.VerifyName(provider.Value), defaultNamespace) as IUriNode;
                            break;
                    }
                }
            }
            var elementNode = id != null ? CreateIdNode(MakeUriNode(provider.BaseUri), id) : BlankNode(provider.LocalName);
            elementInit?.Invoke(elementNode);
            var elementValue = CreateElementValue(content(innerBaseNode), empty, info);
            rdf.HandleTriple(elementNode, value, elementValue);
            return elementNode;
        }

        INode IXmlNodeProcessor<INode>.ProcessEntityReference<TProvider>(TProvider provider)
        {
            return rdf.CreateLiteralNode("&" + provider.LocalName + ";", XMLLiteral);
        }

        private INode MakeList(IEnumerator<INode> enumerator)
        {
            INode listHead = null, listTail = null;

            while(enumerator.MoveNext())
            {
                if(enumerator.Current == null) continue;

                if(listHead == null)
                {
                    listHead = enumerator.Current;
                    continue;
                }else if(listTail == null)
                {
                    var newListHead = BlankNode("list");
                    rdf.HandleTriple(newListHead, first, listHead);
                    listHead = listTail = newListHead;
                }

                var list = BlankNode("list");
                rdf.HandleTriple(listTail, rest, list);
                rdf.HandleTriple(list, first, enumerator.Current);
                listTail = list;
            }

            if(listTail != null)
            {
                rdf.HandleTriple(listTail, rest, nil);
            }

            return listHead;
        }

        private INode CreateElementValue(IEnumerator<INode> enumerator, bool empty, XmlValueInfo info)
        {
            INode elementValue = null;
            if(empty)
            {
                if(info.IsNil != false)
                {
                    elementValue = nil;
                }
            }else{
                elementValue = MakeList(enumerator);
                if(elementValue == null && info.IsNil == true)
                {
                    elementValue = nil;
                }else if(info.DataType != null && elementValue is ILiteralNode literal && !UriComparer.Equals(literal.DataType ?? xstring, xstring))
                {
                    elementValue = rdf.CreateLiteralNode(literal.Value, info.DataType.Uri);
                }
            }
            return elementValue ?? rdf.CreateLiteralNode("", info.DataType?.Uri);
        }

        private INode BlankNode(string name)
        {
            return rdf.CreateBlankNode(GetUniqueName(name));
        }

        private Uri GetDataTypeUri(XmlQualifiedName name)
        {
            return UriTools.ComposeUri(new Uri(name.Namespace), name.Name);
        }

        private INode MakeComposedNode(INode baseNode, string component)
        {
            if(baseNode is IUriNode uriBaseNode)
            {
                return rdf.CreateUriNode(UriTools.ComposeUri(uriBaseNode.Uri, component));
            }else if(localUris.TryGetValue(baseNode, out var baseUri))
            {
                return CacheUriNode(UriTools.ComposeUri(baseUri, component));
            }else{
                return BlankNode("uri");
            }
        }

        private INode MakeAbsoluteNode(INode baseNode, string relativeUri)
        {
            if(baseNode is IUriNode uriBaseNode)
            {
                return rdf.CreateUriNode(new Uri(uriBaseNode.Uri, relativeUri));
            }else{
                var uri = new Uri(relativeUri, UriKind.RelativeOrAbsolute);
                if(uri.IsAbsoluteUri)
                {
                    return rdf.CreateUriNode(uri);
                }else if(localUris.TryGetValue(baseNode, out var baseUri))
                {
                    return CacheUriNode(new Uri(baseUri.GetString() + relativeUri, UriKind.Relative));
                }else{
                    return BlankNode("uri");
                }
            }
        }

        private INode MakeUriNode(Uri uri)
        {
            if(uri != null)
            {
                return rdf.CreateUriNode(uri);
            }else{
                return CacheUriNode(new Uri("", UriKind.Relative));
            }
        }

        private INode CacheUriNode(Uri uri)
        {
            if(!uriBlankNodes.TryGetValue(uri.OriginalString, out var node))
            {
                node = BlankNode("uri");
                uriBlankNodes[uri.OriginalString] = node;
                localUris[node] = uri;
                rdf.HandleTriple(node, label, rdf.CreateLiteralNode(uri.GetString(), anyURI));
            }
            return node;
        }

        private INode CreateSubNode(INode baseNode, string subName)
        {
            var node = MakeComposedNode(baseNode, subName);
            rdf.HandleTriple(node, isDefinedBy, baseNode);
            return node;
        }

        private INode CreateElementTypeNode(INode baseNode, string localName)
        {
            return CreateSubNode(baseNode, localName);
        }

        private INode CreateAttributeTypeNode(INode baseNode, string localName)
        {
            return CreateSubNode(baseNode, "@" + localName);
        }

        private INode CreateIdNode(INode baseNode, string id)
        {
            return CreateSubNode(baseNode, id);
        }

        private INode CreateNotationTypeNode(INode baseNode, string localName)
        {
            return CreateSubNode(baseNode, "?" + localName);
        }

        private INode CreateLocalNamespace(INode baseNode)
        {
            var node = CreateSubNode(baseNode, LocalNamespace);
            UseNamespace("", node);
            return node;
        }

        private void UseNamespace(string prefix, INode namespaceNode)
        {
            if(namespaceNode is IUriNode uriNode)
            {
                rdf.HandleNamespace(prefix, UriTools.GetNamespacePrefix(uriNode.Uri));
            }
        }

        private INode ResolveNamespace<TProvider>(TProvider provider, INode defaultNamespace)
            where TProvider : IBaseUriProvider, IXmlNameProvider
        {
            if(provider.Namespace != null)
            {
                // Use the namespace as the base

                defaultNamespace = rdf.CreateUriNode(provider.Namespace);
                if(!String.IsNullOrEmpty(provider.Prefix))
                {
                    UseNamespace(provider.Prefix, defaultNamespace);
                    rdf.HandleTriple(defaultNamespace, label, rdf.CreateLiteralNode(provider.Prefix, NCName));
                }
            }
            return defaultNamespace ?? CreateLocalNamespace(MakeUriNode(provider.BaseUri));
        }

        private INode ResolveName<TProvider>(TProvider provider, string name, INode defaultNamespace)
            where TProvider : IBaseUriProvider, IXmlNamespaceResolver
        {
            var split = name.Split(new[] { ':' }, 2);
            string localName;
            if(split.Length > 1)
            {
                var ns = provider.LookupNamespace(split[0]);
                if(String.IsNullOrEmpty(ns)) return null;
                defaultNamespace = rdf.CreateUriNode(new Uri(ns));
                localName = split[1];
            }else{
                var ns = provider.LookupNamespace("");
                if(String.IsNullOrEmpty(ns))
                {
                    defaultNamespace = defaultNamespace ?? CreateLocalNamespace(MakeUriNode(provider.BaseUri));
                }else{
                    defaultNamespace = rdf.CreateUriNode(new Uri(ns));
                }
                localName = split[0];
            }
            return MakeComposedNode(defaultNamespace, localName);
        }

        private void AssignNodeName<TProvider>(TProvider provider, INode node)
            where TProvider : IXmlNameProvider
        {
            rdf.HandleTriple(node, label, rdf.CreateLiteralNode(provider.LocalName, NCName));
            if(!String.IsNullOrEmpty(provider.Prefix))
            {
                rdf.HandleTriple(node, label, rdf.CreateLiteralNode(provider.PrefixedName, QName));
            }
        }

        private INode CreateElementType<TProvider>(TProvider provider, INode defaultNamespace)
            where TProvider : IBaseUriProvider, IXmlNameProvider
        {
            defaultNamespace = ResolveNamespace(provider, defaultNamespace);
            var node = CreateElementTypeNode(defaultNamespace, provider.LocalName);
            AssignNodeName(provider, node);
            return node;
        }

        private INode CreateAttributeType<TProvider>(TProvider provider, INode elementType)
            where TProvider : IBaseUriProvider, IXmlNameProvider
        {
            INode node;
            if(provider.Namespace == null)
            {
                // Local (element-scoped) attribute namespace

                node = CreateAttributeTypeNode(elementType, provider.LocalName);
            }else{
                // Global attribute namespace

                var baseNode = rdf.CreateUriNode(provider.Namespace);
                if(!String.IsNullOrEmpty(provider.Prefix))
                {
                    rdf.HandleNamespace(provider.Prefix, UriTools.GetNamespacePrefix(baseNode.Uri));
                    rdf.HandleTriple(baseNode, label, rdf.CreateLiteralNode(provider.Prefix, NCName));
                }
                node = CreateAttributeTypeNode(baseNode, provider.LocalName);
            }
            AssignNodeName(provider, node);
            return node;
        }

        private INode CreateProcessingInstructionType<TProvider>(TProvider provider, INode localNamespaceNode)
            where TProvider : IBaseUriProvider, IXmlNameProvider
        {
            if(ProcessingInstructionMapping.TryGetValue(provider.LocalName, out var nsUri))
            {
                localNamespaceNode = rdf.CreateUriNode(nsUri);
            }else{
                localNamespaceNode = ResolveNamespace(provider, localNamespaceNode);
            }
            var node = CreateNotationTypeNode(localNamespaceNode, provider.LocalName);
            AssignNodeName(provider, node);
            return node;
        }

        ILiteralNode CreateLanguageLiteral<TProvider>(TProvider provider, string value)
            where TProvider : ILanguageProvider
        {
            if(String.IsNullOrEmpty(provider.Language)) return rdf.CreateLiteralNode(value);
            else return rdf.CreateLiteralNode(value, provider.Language);
        }

        INode CreateAttributeValue<TProvider>(TProvider provider)
            where TProvider : IXmlNameProvider, IXmlValueProvider, IBaseUriProvider, ILanguageProvider
        {
            var type = GetFullUri(provider.TypeName);
            if(UriComparer.Equals(type, IDREF))
            {
                return CreateIdNode(MakeUriNode(provider.BaseUri), provider.Value);
            }else if(UriComparer.Equals(type, NOTATION))
            {
                return CreateNotationTypeNode(MakeUriNode(provider.BaseUri), provider.Value);
            }else if(UriComparer.Equals(type, langString))
            {
                return CreateLanguageLiteral(provider, provider.Value);
            }
            return rdf.CreateLiteralNode(provider.Value, type);
        }
    }
}
