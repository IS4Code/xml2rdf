using IS4.RDF.Converters.Xml;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace IS4.RDF.Converters
{
    /// <summary>
    /// Converts XML data taken from <typeparamref name="TInput"/> into RDF nodes.
    /// </summary>
    /// <typeparam name="TInput">The supported input type, for example <see cref="XmlReader"/>, <see cref="IXPathNavigable"/>, <see cref="XNode"/> etc.</typeparam>
    public interface INodeConverter<in TInput>
    {
        /// <summary>
        /// Uses <paramref name="processor"/> to read data from <paramref name="input"/>.
        /// </summary>
        /// <typeparam name="TNode">The type for representation of RDF nodes.</typeparam>
        /// <param name="input">Input object that is used for conversion.</param>
        /// <param name="processor">The processor that accepts individual XML nodes.</param>
        /// <returns>The root node.</returns>
        TNode Convert<TNode>(TInput input, IXmlNodeProcessor<TNode> processor);
    }
}
