using IS4.RDF.Converters.Xml;

namespace IS4.RDF.Converters
{
    public interface INodeConverter<TInput>
    {
        TNode Convert<TNode, TUriNode>(TInput input, IXmlNodeProcessor<TNode, TUriNode> processor) where TUriNode : TNode;
    }
}
