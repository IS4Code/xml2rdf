using System.Xml;

namespace IS4.RDF.Converters
{
    /// <summary>
    /// Converts RDF nodes into XML data.
    /// </summary>
    /// <typeparam name="TNode">The type for representation of RDF nodes.</typeparam>
    public interface ITripleConverter<in TNode>
    {
        /// <summary>
        /// Attempts to convert RDF data starting from <paramref name="rootNode"/> into XML using <paramref name="writer"/>.
        /// </summary>
        /// <param name="writer">XML writer that will be used for the output.</param>
        /// <param name="rootNode">Root node that will represent the document node.</param>
        void Write(XmlWriter writer, TNode rootNode);
    }
}
