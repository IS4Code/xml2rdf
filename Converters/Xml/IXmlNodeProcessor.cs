using System;
using System.Collections.Generic;
using System.Xml;

namespace IS4.RDF.Converters.Xml
{
    public delegate IEnumerator<TNode> ContentIterator<TNode>(TNode baseNode);

    /// <summary>
    /// Processes individual XML nodes and converts them into RDF nodes (as <typeparamref name="TNode"/>).
    /// </summary>
    /// <typeparam name="TNode">The type for representation of RDF nodes.</typeparam>
    public interface IXmlNodeProcessor<TNode> : IDisposable
    {
        TNode ProcessDocument<TProvider>(TProvider provider, ContentIterator<TNode> content)
            where TProvider : IBaseUriProvider;

        void ProcessDocumentType(string publicId, string systemId, string internalSubset, bool useAsNamespace, TNode baseNode, ref TNode defaultNamespace);

        TNode ProcessWhitespace<TProvider>(TProvider provider, bool significant)
            where TProvider : ILanguageProvider, IXmlValueProvider;

        TNode ProcessText<TProvider>(TProvider provider, bool cdata)
            where TProvider : ILanguageProvider, IXmlValueProvider;

        TNode ProcessComment<TProvider>(TProvider provider)
            where TProvider : ILanguageProvider, IXmlValueProvider;

        TNode ProcessProcessingInstruction<TProvider>(TProvider provider, TNode defaultNamespace)
            where TProvider : IXmlNameProvider, IXmlValueProvider, IBaseUriProvider;

        TNode ProcessElement<TProvider>(TProvider provider, TNode baseNode, Uri originalBaseUri, TNode defaultNamespace, ContentIterator<TNode> content)
            where TProvider : IBaseUriProvider, ILanguageProvider, IXmlAttributeIterator, IXmlNamespaceResolver;

        TNode ProcessEntityReference<TProvider>(TProvider provider)
            where TProvider : IXmlNameProvider;
    }
}
