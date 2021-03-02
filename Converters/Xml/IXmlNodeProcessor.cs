using System;
using System.Collections.Generic;
using System.Xml;

namespace IS4.RDF.Converters.Xml
{
    public interface IXmlNodeProcessor<TNode, TUriNode> : IDisposable where TUriNode : TNode
    {
        TUriNode ProcessDocument<TProvider>(TProvider provider, Func<TUriNode, IEnumerator<TNode>> content)
            where TProvider : IBaseUriProvider;

        void ProcessDocumentType(string publicId, string systemId, string internalSubset, bool useAsNamespace, TUriNode baseNode, ref TUriNode defaultNamespace);

        TNode ProcessWhitespace<TProvider>(TProvider provider, bool significant)
            where TProvider : ILanguageProvider, IXmlValueProvider;

        TNode ProcessText<TProvider>(TProvider provider, bool cdata)
            where TProvider : ILanguageProvider, IXmlValueProvider;

        TNode ProcessComment<TProvider>(TProvider provider)
            where TProvider : ILanguageProvider, IXmlValueProvider;

        TNode ProcessProcessingInstruction<TProvider>(TProvider provider, TUriNode defaultNamespace)
            where TProvider : IXmlNameProvider, IXmlValueProvider, IBaseUriProvider;

        TNode ProcessElement<TProvider>(TProvider provider, TUriNode baseNode, Uri originalBaseUri, TUriNode defaultNamespace, Func<TUriNode, IEnumerator<TNode>> content)
            where TProvider : IBaseUriProvider, ILanguageProvider, IXmlAttributeProvider, IXmlNamespaceResolver;

        TNode ProcessEntityReference<TProvider>(TProvider provider)
            where TProvider : IXmlNameProvider;
    }
}
