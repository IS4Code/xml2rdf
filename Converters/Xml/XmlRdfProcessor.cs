using System;
using System.Collections.Generic;
using System.Xml;
using VDS.RDF;
using VDS.RDF.Parsing.Handlers;

namespace IS4.RDF.Converters.Xml
{
    public class XmlRdfProcessor : ProcessorBase, IXmlNodeProcessor<INode, IUriNode>
    {
        readonly IRdfHandler rdf;
        readonly string linkName;

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
        }

        void IDisposable.Dispose()
        {
            rdf.EndRdf(true);
        }

        IUriNode IXmlNodeProcessor<INode, IUriNode>.ProcessDocument<TProvider>(TProvider provider, Func<IUriNode, IEnumerator<INode>> content)
        {
            var baseNode = rdf.CreateUriNode(provider.BaseUri);
            rdf.HandleTriple(baseNode, value, MakeList(content(baseNode)) ?? nil);
            return baseNode;
        }

        void IXmlNodeProcessor<INode, IUriNode>.ProcessDocumentType(string publicId, string systemId, string internalSubset, bool useAsNamespace, IUriNode baseNode, ref IUriNode defaultNamespace)
        {
            INode baseType = null;
            if(publicId != null)
            {
                var publicNode = rdf.CreateUriNode(UriTools.CreatePublicId(publicId));
                rdf.HandleTriple(publicNode, label, rdf.CreateLiteralNode(publicId, xpublic));
                if(systemId != null)
                {
                    var dtd = new Uri(baseNode.Uri, systemId);
                    rdf.HandleTriple(publicNode, isDefinedBy, rdf.CreateUriNode(dtd));
                }
                if(useAsNamespace && UseDtdAsDefaultNamespace) defaultNamespace = defaultNamespace ?? publicNode;
                baseType = publicNode;
            }else if(systemId != null)
            {
                var systemNode = rdf.CreateUriNode(new Uri(baseNode.Uri, systemId));
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

        INode IXmlNodeProcessor<INode, IUriNode>.ProcessWhitespace<TProvider>(TProvider provider, bool significant)
        {
            if((significant && WhitespaceHandling == WhitespaceHandling.Significant) || WhitespaceHandling == WhitespaceHandling.All)
            {
                return ((IXmlNodeProcessor<INode, IUriNode>)this).ProcessText(provider, false);
            }
            return null;
        }

        INode IXmlNodeProcessor<INode, IUriNode>.ProcessText<TProvider>(TProvider provider, bool cdata)
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

        INode IXmlNodeProcessor<INode, IUriNode>.ProcessComment<TProvider>(TProvider provider)
        {
            var commentNode = BlankNode("comment");
            rdf.HandleTriple(commentNode, value, nil);
            rdf.HandleTriple(commentNode, comment, rdf.CreateLiteralNode(provider.Value));
            return commentNode;
        }

        INode IXmlNodeProcessor<INode, IUriNode>.ProcessProcessingInstruction<TProvider>(TProvider provider, IUriNode defaultNamespace)
        {
            if(linkName == provider.LocalName)
            {
                return rdf.CreateUriNode(provider.BaseUri);
            }
            var procNode = BlankNode("proc");
            rdf.HandleTriple(procNode, value, nil);
            rdf.HandleTriple(procNode, comment, rdf.CreateLiteralNode(provider.Value, CreateProcessingInstructionType(provider, defaultNamespace).Uri));
            return procNode;
        }

        struct XmlValueInfo
        {
            public bool? IsNil;
            public IUriNode DataType;
        }

        INode IXmlNodeProcessor<INode, IUriNode>.ProcessElement<TProvider>(TProvider provider, IUriNode baseNode, Uri originalBaseUri, IUriNode defaultNamespace, Func<IUriNode, IEnumerator<INode>> content)
        {
            var empty = provider.IsEmpty;
            bool sameBase = provider.BaseUri == originalBaseUri;

            Action<INode> elementInit = null;
            var elementType = CreateElementType(provider, defaultNamespace);
            elementInit += n => rdf.HandleTriple(n, a, elementType);

            string id = null;
            IUriNode innerBaseNode = baseNode;
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
                                innerBaseNode = rdf.CreateUriNode(new Uri(baseNode?.Uri, provider.Value));
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
                            info.DataType = ResolveName(provider, XmlConvert.VerifyName(provider.Value), defaultNamespace);
                            break;
                    }
                }
            }
            var elementNode = id != null ? CreateIdNode(rdf.CreateUriNode(provider.BaseUri), id) : BlankNode(provider.LocalName);
            elementInit?.Invoke(elementNode);
            var elementValue = CreateElementValue(content(innerBaseNode), empty, info);
            rdf.HandleTriple(elementNode, value, elementValue);
            return elementNode;
        }

        INode IXmlNodeProcessor<INode, IUriNode>.ProcessEntityReference<TProvider>(TProvider provider)
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

        private IUriNode CreateSubNode(IUriNode baseNode, string subName)
        {
            var node = rdf.CreateUriNode(UriTools.ComposeUri(baseNode.Uri, subName));
            rdf.HandleTriple(node, isDefinedBy, baseNode);
            return node;
        }

        private IUriNode CreateElementTypeNode(IUriNode baseNode, string localName)
        {
            return CreateSubNode(baseNode, localName);
        }

        private IUriNode CreateAttributeTypeNode(IUriNode baseNode, string localName)
        {
            return CreateSubNode(baseNode, "@" + localName);
        }

        private IUriNode CreateIdNode(IUriNode baseNode, string id)
        {
            return CreateSubNode(baseNode, id);
        }

        private IUriNode CreateNotationTypeNode(IUriNode baseNode, string localName)
        {
            return CreateSubNode(baseNode, "?" + localName);
        }

        private IUriNode CreateLocalNamespace(IUriNode baseNode)
        {
            var node = CreateSubNode(baseNode, LocalNamespace);
            UseNamespace("", node);
            return node;
        }

        private void UseNamespace(string prefix, IUriNode namespaceNode)
        {
            rdf.HandleNamespace(prefix, UriTools.GetNamespacePrefix(namespaceNode.Uri));
        }

        private IUriNode ResolveNamespace<TProvider>(TProvider provider, IUriNode defaultNamespace)
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
            return defaultNamespace ?? CreateLocalNamespace(rdf.CreateUriNode(provider.BaseUri));
        }

        private IUriNode ResolveName<TProvider>(TProvider provider, string name, IUriNode defaultNamespace)
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
                    defaultNamespace = defaultNamespace ?? CreateLocalNamespace(rdf.CreateUriNode(provider.BaseUri));
                }else{
                    defaultNamespace = rdf.CreateUriNode(new Uri(ns));
                }
                localName = split[0];
            }
            return rdf.CreateUriNode(UriTools.ComposeUri(defaultNamespace.Uri, localName));
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

        private IUriNode CreateElementType<TProvider>(TProvider provider, IUriNode defaultNamespace)
            where TProvider : IBaseUriProvider, IXmlNameProvider
        {
            defaultNamespace = ResolveNamespace(provider, defaultNamespace);
            var node = CreateElementTypeNode(defaultNamespace, provider.LocalName);
            AssignNodeName(provider, node);
            return node;
        }

        private IUriNode CreateAttributeType<TProvider>(TProvider provider, IUriNode elementType)
            where TProvider : IBaseUriProvider, IXmlNameProvider
        {
            IUriNode node;
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

        private IUriNode CreateProcessingInstructionType<TProvider>(TProvider provider, IUriNode localNamespaceNode)
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
                return CreateIdNode(rdf.CreateUriNode(provider.BaseUri), provider.Value);
            }else if(UriComparer.Equals(type, NOTATION))
            {
                return CreateNotationTypeNode(rdf.CreateUriNode(provider.BaseUri), provider.Value);
            }else if(UriComparer.Equals(type, langString))
            {
                return CreateLanguageLiteral(provider, provider.Value);
            }
            return rdf.CreateLiteralNode(provider.Value, type);
        }
    }
}
