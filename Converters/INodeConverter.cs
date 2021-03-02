using IS4.RDF.Converters.Xml;

namespace IS4.RDF.Converters
{
    public interface INodeConverter<TInput>
    {
        TNode Convert<TNode>(TInput input, IXmlNodeProcessor<TNode> processor);
    }
}
