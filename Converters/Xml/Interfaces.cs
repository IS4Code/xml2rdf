using System;
using System.Xml;

namespace IS4.RDF.Converters.Xml
{
    /// <summary>
    /// Provides the base URI of a node.
    /// </summary>
    public interface IBaseUriProvider
    {
        Uri BaseUri { get; }
    }

    /// <summary>
    /// Provides the qualified name of an XML node.
    /// </summary>
    public interface IXmlNameProvider
    {
        XmlQualifiedName QualifiedName { get; }
        string LocalName { get; }
        string PrefixedName { get; }
        Uri Namespace { get; }
        string Prefix { get; }
    }

    /// <summary>
    /// Provides the typed XML value of a node.
    /// </summary>
    public interface IXmlValueProvider
    {
        XmlQualifiedName TypeName { get; }
        string Value { get; }
        bool IsEmpty { get; }
    }

    /// <summary>
    /// Provides the language of a node.
    /// </summary>
    public interface ILanguageProvider
    {
        string Language { get; }
    }

    /// <summary>
    /// Provides support for iterating through attributes.
    /// </summary>
    public interface IXmlAttributeIterator : IXmlNameProvider, IXmlValueProvider
    {
        bool MoveToNextAttribute();
        bool IsDefault { get; }
    }

    /// <summary>
    /// Provides the necessary support for inspecting a node in the XML infoset.
    /// </summary>
    public interface IXmlProvider : ILanguageProvider, IXmlNameProvider, IXmlValueProvider, IBaseUriProvider, IXmlAttributeIterator, IXmlNamespaceResolver
    {

    }
}
