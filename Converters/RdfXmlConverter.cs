using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using VDS.RDF;

namespace IS4.RDF.Converters
{
    /// <summary>
    /// Converts RDF nodes represented by <see cref="INode"/> to XML.
    /// </summary>
    public sealed class RdfXmlConverter : ProcessorBase, ITripleConverter<INode>
    {
        readonly IGraph graph;

        public RdfXmlConverter(IGraph graph) : base(graph)
        {
            this.graph = graph;
        }

        public void Write(XmlWriter writer, INode documentNode)
        {
            writer.WriteStartDocument();
            try{
                var context = new Context(this, documentNode);
                WriteDocumentType(writer, documentNode, context);
                WriteValues(writer, documentNode, context);
            }finally{
                writer.WriteEndDocument();
            }
        }

        private XmlQualifiedName GetXmlName(INode node, INode documentNode, INode elementType)
        {
            if(graph.FindObject(node, label).OfType<ILiteralNode>().FirstOrDefault(n => UriComparer.Equals(n.DataType, NCName))?.Value is string name)
            {
                var ns = graph.FindObject(node, isDefinedBy).OfType<IUriNode>().FirstOrDefault();
                if(ns != null && !ns.Equals(elementType) && !graph.ContainsTriple(ns, isDefinedBy, documentNode))
                {
                    return new XmlQualifiedName(name, ns.Uri.GetString());
                }
                return new XmlQualifiedName(name);
            }
            return null;
        }

        private void WriteValues(XmlWriter writer, INode node, Context context)
        {
            foreach(var value in GetValues(node))
            {
                if(WriteValue(writer, value, context)) return;
            }
            WriteFallback(writer, node, context);
        }

        private bool WriteValue(XmlWriter writer, INode node, Context context)
        {
            if(node == null) return true;

            // If raw XML value is provided, use that
            var literalValue = graph.FindObject(node, label).OfType<ILiteralNode>().FirstOrDefault(n => UriComparer.Equals(n.DataType, XMLLiteral) == true);
            if(literalValue != null)
            {
                writer.WriteRaw(literalValue.Value);
                return true;
            }

            // Write all comments first
            foreach(var comment in graph.FindObject(node, comment).OfType<ILiteralNode>())
            {
                if(comment.DataType != null)
                {
                    var piName = GetXmlName(graph.CreateUriNode(comment.DataType), context.DocumentNode, null);
                    if(piName != null)
                    {
                        writer.WriteProcessingInstruction(piName.Name, comment.Value);
                        continue;
                    }
                }
                writer.WriteComment(comment.Value);
            }

            if(node is ILiteralNode literal)
            {
                if(UriComparer.Equals(literal.DataType, XMLLiteral))
                {
                    writer.WriteRaw(literal.Value);
                }else{
                    writer.WriteString(literal.Value);
                }
                return true;
            }

            var (elementType, elementName) = graph.FindObject(node, a).Select(t => (t, n: GetXmlName(t, context.DocumentNode, null))).FirstOrDefault(t => t.n != null);
            if(elementName != null)
            {
                if(context.FindRoot) throw new RootNameSignal(elementName.Name);
                writer.WriteStartElement(null, elementName.Name, elementName.Namespace);

                foreach(var (pred, obj) in graph.FindPredicateObject(node))
                {
                    var attributeName = GetXmlName(pred, context.DocumentNode, elementType);
                    if(attributeName != null)
                    {
                        if(!(obj is ILiteralNode value))
                        {
                            value = graph.FindPredicateObject(obj).Select(t => t.obj).OfType<ILiteralNode>().FirstOrDefault(l => UriComparer.Equals(l.DataType, ID));
                            if(value == null) continue;
                        }
                        writer.WriteAttributeString(attributeName.Name, attributeName.Namespace, value.Value);
                    }
                }
            }
            try{
                foreach(var value in GetValues(node))
                {
                    if(WriteValue(writer, value, context)) return true;
                }
                var list = EnumerateList(node);
                if(list != null)
                {
                    foreach(var element in list)
                    {
                        if(!WriteValue(writer, element, context))
                        {
                            WriteFallback(writer, element, context);
                        }
                    }
                    return true;
                }
                bool any = false;
                foreach(var member in graph.FindObject(node, member))
                {
                    if(WriteValue(writer, member, context)) any = true;
                }
                if(elementName != null)
                {
                    if(!any) WriteFallback(writer, node, context);
                    return true;
                }
                return any;
            }finally{
                if(elementName != null) writer.WriteEndElement();
            }
        }

        private void WriteFallback(XmlWriter writer, INode node, Context context)
        {
            // Cannot be further described
            if(node is IUriNode uriNode)
            {
                WriteExternalEntity(writer, uriNode.Uri, context.Entities);
            }
        }

        static readonly Regex nameRegex = new Regex("[^a-zA-Z]+", RegexOptions.Compiled);

        private void WriteExternalEntity(XmlWriter writer, Uri uri, IDictionary<Uri, string> entities)
        {
            if(!entities.TryGetValue(uri, out var name))
            {
                name = GetUniqueName(nameRegex.Replace(Uri.UnescapeDataString(uri.GetString()), ""));
                entities[uri] = name;
            }
            writer.WriteEntityRef(name);
        }

        private XmlWriter CreateNullWriter()
        {
            return XmlWriter.Create(TextWriter.Null, new XmlWriterSettings
            {
                CheckCharacters = false,
                CloseOutput = false,
                Indent = false,
                ConformanceLevel = ConformanceLevel.Fragment,
                OmitXmlDeclaration = true
            });
        }

        private string GetDtdSubset(INode documentNode, Context context)
        {
            using(var nullWriter = CreateNullWriter())
            {
                foreach(var value in GetValues(documentNode))
                {
                    if(WriteValue(nullWriter, value, context)) break;
                }
                var sb = new StringBuilder();
                sb.AppendLine();
                foreach(var pair in context.Entities)
                {
                    var key = pair.Key;
                    var name = pair.Value;

                    var pubId = UriTools.ExtractPublicId(pair.Key);
                    if(pubId != null)
                    {
                        var sysId = graph.FindObject(graph.CreateUriNode(key), isDefinedBy).OfType<IUriNode>().FirstOrDefault()?.Uri;
                        if(sysId == null)
                        {
                            sysId = key;
                        }
                        sb.AppendLine($"<!ENTITY {name} PUBLIC \"{pubId}\" \"{sysId.GetString()}\">");
                    }else{
                        sb.AppendLine($"<!ENTITY {name} SYSTEM \"{key.GetString()}\">");
                    }
                }
                return sb.ToString();
            }
        }

        private void WriteDocumentType(XmlWriter writer, INode documentNode, Context context)
        {
            var root = GetRootType(documentNode);
            if(root == null) return;

            var classes = graph.FindObject(documentNode, a);

            var entitySubset = GetDtdSubset(documentNode, context);

            foreach(var cls in classes)
            {
                if(graph.FindObject(cls, subClassOf).FirstOrDefault() is IUriNode publicNode)
                {
                    if(UriTools.ExtractPublicId(publicNode.Uri) is string pubId)
                    {
                        if(graph.FindObject(publicNode, isDefinedBy).FirstOrDefault() is IUriNode systemNode)
                        {
                            var subset = GetValues(cls).OfType<ILiteralNode>().FirstOrDefault();
                            writer.WriteDocType(root, pubId, systemNode.Uri.GetString(), subset?.Value + entitySubset);
                            return;
                        }
                    }
                }
            }

            foreach(var cls in classes)
            {
                if(graph.FindObject(cls, subClassOf).FirstOrDefault() is IUriNode publicNode)
                {
                    if(UriTools.ExtractPublicId(publicNode.Uri) is string pubId)
                    {
                        var subset = GetValues(cls).OfType<ILiteralNode>().FirstOrDefault();
                        writer.WriteDocType(root, pubId, null, subset?.Value + entitySubset);
                        return;
                    }
                }
            }

            foreach(var cls in classes.OfType<IUriNode>())
            {
                if(UriTools.ExtractPublicId(cls.Uri) is string pubId)
                {
                    if(graph.FindObject(cls, isDefinedBy).FirstOrDefault() is IUriNode systemNode)
                    {
                        writer.WriteDocType(root, pubId, systemNode.Uri.GetString(), entitySubset);
                        return;
                    }
                }
            }

            foreach(var cls in classes.OfType<IUriNode>())
            {
                if(UriTools.ExtractPublicId(cls.Uri) is string pubId)
                {
                    writer.WriteDocType(root, pubId, null, entitySubset);
                    return;
                }
            }

            foreach(var cls in classes)
            {
                var subset = GetValues(cls).OfType<ILiteralNode>().FirstOrDefault();
                if(subset != null)
                {
                    writer.WriteDocType(root, null, null, subset.Value + entitySubset);
                    return;
                }
            }

            if(!String.IsNullOrEmpty(entitySubset))
            {
                writer.WriteDocType(root, null, null, entitySubset);
            }
        }

        private string GetRootType(INode documentNode)
        {
            using(var nullWriter = CreateNullWriter())
            {
                try
                {
                    var context = new Context(this, documentNode, true);
                    WriteValues(nullWriter, documentNode, context);
                    return null;
                }catch(RootNameSignal signal)
                {
                    return signal.Name;
                }
            }
        }

        private IEnumerable<INode> EnumerateList(INode list)
        {
            if(nil.Equals(list)) return Enumerable.Empty<INode>();
            var firstNode = graph.FindObject(list, first).FirstOrDefault();
            var restNode = graph.FindObject(list, rest).FirstOrDefault();
            if(firstNode == null && restNode == null)
            {
                // Not a list
                return null;
            }
            var result = new[] { firstNode };
            if(restNode == null) return result;
            return result.Concat(EnumerateListInner(restNode));
        }

        private IEnumerable<INode> EnumerateListInner(INode list)
        {
            while(list != null && !nil.Equals(list))
            {
                yield return graph.FindObject(list, first).FirstOrDefault();
                list = graph.FindObject(list, rest).FirstOrDefault();
            }
        }

        private IEnumerable<INode> GetValues(INode node)
        {
            return graph.FindObject(node, value);
        }

        class Context
        {
            public INode DocumentNode { get; }
            public IDictionary<Uri, string> Entities { get; }
            public bool FindRoot { get; }

            public Context(RdfXmlConverter instance, INode documentNode, bool findRoot = false)
            {
                DocumentNode = documentNode;
                Entities = new Dictionary<Uri, string>(instance.UriComparer);
                FindRoot = findRoot;
            }
        }

        class RootNameSignal : Exception
        {
            public string Name { get; }

            public RootNameSignal(string name)
            {
                Name = name;
            }
        }
    }
}
