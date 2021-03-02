using System;
using System.Collections.Generic;
using System.Xml;

namespace IS4.RDF.Converters.Xml
{
    public interface IXmlNodeProcessor<TNode> : IDisposable
    {
        TNode ProcessDocument<TProvider>(TProvider provider, Func<TNode, IEnumerator<TNode>> content)
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

        TNode ProcessElement<TProvider>(TProvider provider, TNode baseNode, Uri originalBaseUri, TNode defaultNamespace, Func<TNode, IEnumerator<TNode>> content)
            where TProvider : IBaseUriProvider, ILanguageProvider, IXmlAttributeProvider, IXmlNamespaceResolver;

        TNode ProcessEntityReference<TProvider>(TProvider provider)
            where TProvider : IXmlNameProvider;
    }
}
