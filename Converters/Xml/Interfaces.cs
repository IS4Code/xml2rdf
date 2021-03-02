using System;
using System.Xml;

namespace IS4.RDF.Converters.Xml
{
    public interface IBaseUriProvider
    {
        Uri BaseUri { get; }
    }

    public interface IXmlNameProvider
    {
        XmlQualifiedName QualifiedName { get; }
        string LocalName { get; }
        string PrefixedName { get; }
        Uri Namespace { get; }
        string Prefix { get; }
    }

    public interface IXmlValueProvider
    {
        XmlQualifiedName TypeName { get; }
        string Value { get; }
        bool IsEmpty { get; }
    }

    public interface ILanguageProvider
    {
        string Language { get; }
    }

    public interface IXmlAttributeProvider : IXmlNameProvider, IXmlValueProvider
    {
        bool MoveToNextAttribute();
        bool IsDefault { get; }
    }

    public interface IXmlProvider : ILanguageProvider, IXmlNameProvider, IXmlValueProvider, IBaseUriProvider, IXmlAttributeProvider, IXmlNamespaceResolver
    {

    }
}
