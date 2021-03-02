using IS4.RDF.Converters.Xml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using VDS.RDF;

namespace IS4.RDF.Converters
{
    public sealed class RdfXPathNavigator : DocTypeXPathNavigator, IXmlProvider
    {
        readonly IGraph graph;
        Cursor cursor;

        RdfXPathNavigator(Cursor cursor)
        {
            this.cursor = cursor;
        }

        public RdfXPathNavigator(IGraph graph, INode rootNode) : this(CreateRootCursor(graph, rootNode))
        {
            this.graph = graph;
        }

        private static Cursor CreateRootCursor(IGraph graph, INode rootNode)
        {
            return new NodeCursor(new Context(graph, rootNode), null, new[] { new NodeCursor.NodeInfo(XPathNodeType.Root, rootNode) }, 0);
        }

        public override XDocumentType DocumentType {
            get {
                var copy = Clone() as RdfXPathNavigator;
                if(copy.MoveToFollowing(XPathNodeType.Element))
                {
                    return copy.cursor.Context.GetDocumentType(copy.Name);
                }
                return null;
            }
        }

        public override object UnderlyingObject => cursor.UnderlyingObject;

        public override string BaseURI => cursor.BaseUri.GetString();

        public override bool IsEmptyElement => cursor.IsEmpty;

        public override string LocalName => cursor.LocalName;

        public override string Name => cursor.PrefixedName;

        public override string NamespaceURI => cursor.Namespace?.GetString() ?? "";

        public override XmlNameTable NameTable => throw new NotImplementedException();

        public override XPathNodeType NodeType => cursor.NodeType;

        public override string Prefix => cursor.Prefix;

        public override string Value => cursor.Value;

        public override string OuterXml => cursor.OuterXml ?? base.OuterXml;

        public override string InnerXml => cursor.InnerXml ?? base.InnerXml;

        string ILanguageProvider.Language => cursor.Language;

        Uri IBaseUriProvider.BaseUri => cursor.BaseUri;

        bool IXmlAttributeProvider.IsDefault => cursor.IsDefault;

        XmlQualifiedName IXmlNameProvider.QualifiedName => cursor.QualifiedName;

        string IXmlNameProvider.PrefixedName => cursor.PrefixedName;

        Uri IXmlNameProvider.Namespace => cursor.Namespace;

        XmlQualifiedName IXmlValueProvider.TypeName => cursor.TypeName;

        bool IXmlValueProvider.IsEmpty => cursor.IsEmpty;

        public override XPathNavigator Clone()
        {
            return new RdfXPathNavigator(cursor.Clone());
        }

        public override bool IsSamePosition(XPathNavigator other)
        {
            return other is RdfXPathNavigator nav && cursor.Equals(nav.cursor);
        }

        public override bool MoveTo(XPathNavigator other)
        {
            if(other is RdfXPathNavigator nav)
            {
                cursor = nav.cursor.Clone();
                return true;
            }
            return false;
        }

        private bool SetCursor(Cursor newCursor)
        {
            if(newCursor != null)
            {
                cursor = newCursor;
                return true;
            }
            return false;
        }

        public override bool MoveToParent()
        {
            return SetCursor(cursor.Parent);
        }

        public override bool MoveToId(string id)
        {
            return SetCursor(CreateRootCursor(graph, graph.CreateUriNode(UriTools.ComposeUri(cursor.BaseUri, "#" + id))));
        }

        public override bool MoveToPrevious()
        {
            return cursor.MoveToPrevious();
        }

        public override bool MoveToNext()
        {
            return cursor.MoveToNext();
        }

        public override bool MoveToFirstChild()
        {
            return SetCursor(cursor.GetFirstChild());
        }

        public override bool MoveToFirstAttribute()
        {
            return SetCursor(cursor.GetFirstAttribute());
        }

        public override bool MoveToNextAttribute()
        {
            return cursor.MoveToNextAttribute();
        }

        public override bool MoveToFirstNamespace(XPathNamespaceScope namespaceScope)
        {
            return SetCursor(cursor.GetFirstNamespace(namespaceScope));
        }

        public override bool MoveToNextNamespace(XPathNamespaceScope namespaceScope)
        {
            return cursor.MoveToNextNamespace(namespaceScope);
        }

        class Context : ProcessorBase
        {
            public IGraph Graph { get; }
            public INode DocumentNode { get; }

            public Uri LocalNamespace { get; }

            public Context(IGraph graph, INode documentNode) : base(graph)
            {
                Graph = graph;
                DocumentNode = documentNode;

                LocalNamespace = DocumentNode is IUriNode uri ? UriTools.ComposeUri(uri.Uri, XmlRdfProcessor.LocalNamespace) : null;
            }

            public IEnumerable<INode> FindObject(INode subj, INode pred)
            {
                return Graph.FindObject(subj, pred);
            }

            public IEnumerable<IGrouping<INode, INode>> FindAttributes(INode subj)
            {
                return Graph.FindPredicateObject(subj).GroupBy(t => t.pred, t => t.obj);
            }

            public string FindPrefix(Uri ns)
            {
                if(UriComparer.Equals(ns, LocalNamespace)) return "";
                try
                {
                    return Graph.NamespaceMap.GetPrefix(UriTools.GetNamespacePrefix(ns));
                }catch(RdfException)
                {
                    return null;
                }
            }

            public Uri FindNamespace(string prefix)
            {
                if(String.IsNullOrEmpty(prefix)) return LocalNamespace;
                return UriTools.VerifyNamespacePrefix(Graph.NamespaceMap.GetNamespaceUri(prefix));
            }

            public XDocumentType GetDocumentType(string root)
            {
                var classes = FindObject(DocumentNode, a);

                foreach(var cls in classes)
                {
                    if(FindObject(cls, subClassOf).FirstOrDefault() is IUriNode publicNode)
                    {
                        if(UriTools.ExtractPublicId(publicNode.Uri) is string pubId)
                        {
                            if(FindObject(publicNode, isDefinedBy).FirstOrDefault() is IUriNode systemNode)
                            {
                                var subset = FindObject(cls, value).OfType<ILiteralNode>().FirstOrDefault();
                                return new XDocumentType(root, pubId, systemNode.Uri.GetString(), subset?.Value);
                            }
                        }
                    }
                }

                foreach(var cls in classes)
                {
                    if(FindObject(cls, subClassOf).FirstOrDefault() is IUriNode publicNode)
                    {
                        if(UriTools.ExtractPublicId(publicNode.Uri) is string pubId)
                        {
                            var subset = FindObject(cls, value).OfType<ILiteralNode>().FirstOrDefault();
                            return new XDocumentType(root, pubId, null, subset?.Value);
                        }
                    }
                }

                foreach(var cls in classes.OfType<IUriNode>())
                {
                    if(UriTools.ExtractPublicId(cls.Uri) is string pubId)
                    {
                        if(FindObject(cls, isDefinedBy).FirstOrDefault() is IUriNode systemNode)
                        {
                            return new XDocumentType(root, pubId, systemNode.Uri.GetString(), null);
                        }
                    }
                }

                foreach(var cls in classes.OfType<IUriNode>())
                {
                    if(UriTools.ExtractPublicId(cls.Uri) is string pubId)
                    {
                        return new XDocumentType(root, pubId, null, null);
                    }
                }

                foreach(var cls in classes)
                {
                    var subset = FindObject(cls, value).OfType<ILiteralNode>().FirstOrDefault();
                    if(subset != null)
                    {
                        return new XDocumentType(root, null, null, subset.Value);
                    }
                }

                return null;
            }
        }

        abstract class Cursor : ProcessorBase, IXmlProvider, ICloneable
        {
            internal Context Context { get; }
            public abstract XPathNodeType NodeType { get; }

            public Cursor Parent { get; }

            public int Position { get; protected set; }

            public Cursor(Context context, Cursor parent) : base(context.Graph)
            {
                Context = context;
                Parent = parent;
            }

            public abstract object UnderlyingObject { get; }

            public virtual bool MoveToPrevious()
            {
                return false;
            }

            public virtual bool MoveToNext()
            {
                return false;
            }

            public virtual Cursor GetFirstChild()
            {
                return null;
            }

            public virtual Cursor GetFirstNamespace(XPathNamespaceScope namespaceScope)
            {
                return null;
            }

            public virtual bool MoveToNextNamespace(XPathNamespaceScope namespaceScope)
            {
                return false;
            }

            public virtual Cursor GetFirstAttribute()
            {
                return null;
            }

            public virtual bool MoveToNextAttribute()
            {
                return false;
            }

            public virtual string InnerXml => null;

            public virtual string OuterXml => null;

            public virtual string Language => null;

            public Uri BaseUri => throw new NotImplementedException();

            public virtual bool IsDefault => false;

            public abstract XmlQualifiedName QualifiedName { get; }

            public string LocalName => QualifiedName?.Name;

            public string PrefixedName => String.IsNullOrEmpty(Prefix) ? LocalName : $"{Prefix}:{LocalName}";

            public Uri Namespace => !String.IsNullOrEmpty(QualifiedName?.Namespace) ? new Uri(QualifiedName.Namespace) : null;

            public string Prefix => !String.IsNullOrEmpty(QualifiedName?.Namespace) ? LookupPrefix(QualifiedName.Namespace) : null;

            public abstract XmlQualifiedName TypeName { get; }

            public abstract string Value { get; }

            public virtual bool IsEmpty => false;

            public abstract Cursor Clone();

            object ICloneable.Clone()
            {
                return Clone();
            }

            public IDictionary<string, string> GetNamespacesInScope(XmlNamespaceScope scope)
            {
                throw new NotImplementedException();
            }

            public string LookupNamespace(string prefix)
            {
                return Context.FindNamespace(prefix)?.GetString();
            }

            public string LookupPrefix(string namespaceName)
            {
                if(String.IsNullOrWhiteSpace(namespaceName)) return null;
                return Context.FindPrefix(new Uri(namespaceName));
            }


            protected string GetStringValue(INode node)
            {
                if(node is ILiteralNode literal && !UriComparer.Equals(literal.DataType, XMLLiteral))
                {
                    return literal.Value;
                }
                throw new NotImplementedException();
            }

            protected string GetLanguage(INode node)
            {
                if(node is ILiteralNode literal)
                {
                    return literal.Language;
                }
                return null;
            }

            protected XmlQualifiedName GetType(INode node)
            {
                if(node is ILiteralNode literal && literal.DataType != null)
                {
                    return GetQualifiedName(Context.Graph.CreateUriNode(literal.DataType));
                }
                return Context.FindObject(node, a).Select(GetQualifiedName).FirstOrDefault(NotNull);
            }

            private bool IsNCName(ILiteralNode node)
            {
                return UriComparer.Equals(node.DataType, NCName);
            }

            protected XmlQualifiedName GetQualifiedName(INode node)
            {
                var ncname = Context.FindObject(node, label).OfType<ILiteralNode>().FirstOrDefault(IsNCName);
                if(ncname != null)
                {
                    var ns = Context.FindObject(node, isDefinedBy).OfType<IUriNode>().FirstOrDefault();
                    if(ns != null)
                    {
                        if(UriComparer.Equals(ns.Uri, Context.LocalNamespace) || UriComparer.Equals(Context.FindObject(ns, isDefinedBy).OfType<IUriNode>().FirstOrDefault()?.Uri, Context.LocalNamespace))
                        {
                            return new XmlQualifiedName(ncname.Value);
                        }else{
                            return new XmlQualifiedName(ncname.Value, ns.Uri.GetString());
                        }
                    }
                }
                return null;
            }

            protected string GetXmlValue(INode node)
            {
                if(node is ILiteralNode literal && UriComparer.Equals(literal.DataType, XMLLiteral))
                {
                    return literal.Value;
                }
                return null;
            }

            protected static bool NotNull<T>(T value) where T : class
            {
                return value != null;
            }
        }

        abstract class ListCursor<T> : Cursor
        {
            public IReadOnlyList<T> Nodes { get; }

            public T Current => Nodes[Position];

            public override object UnderlyingObject => Current;

            public ListCursor(Context context, Cursor parent, IReadOnlyList<T> nodes, int position) : base(context, parent)
            {
                Nodes = nodes;
                Position = position;
            }

            protected bool IncrementPosition()
            {
                if(Position + 1 < Nodes.Count)
                {
                    Position++;
                    return true;
                }
                return false;
            }

            protected bool DecrementPosition()
            {
                if(Position > 0)
                {
                    Position--;
                    return true;
                }
                return false;
            }
        }

        class NodeCursor : ListCursor<NodeCursor.NodeInfo>
        {
            public override XPathNodeType NodeType => Current.Type;

            public override XmlQualifiedName QualifiedName => GetType(Current.Node);

            public override XmlQualifiedName TypeName => GetType(Current.Node);

            public override string Value => GetStringValue(Current.Node);

            public override string Language => GetLanguage(Current.Node);

            public override bool IsEmpty => Context.FindObject(Current.Node, value).Contains(nil);

            public NodeCursor(Context context, Cursor parent, IReadOnlyList<NodeInfo> nodes, int position) : base(context, parent, nodes, position)
            {

            }

            public override bool MoveToNext()
            {
                return IncrementPosition();
            }

            public override bool MoveToPrevious()
            {
                return DecrementPosition();
            }

            public override Cursor GetFirstAttribute()
            {
                var attributes = Context.FindAttributes(Current.Node).Select(g => new AttributeCursor.AttributeInfo(GetQualifiedName(g.Key), g)).Where(a => a.QualifiedName != null);
                var list = attributes.ToList();
                if(list.Count > 0)
                {
                    return new AttributeCursor(Context, this, list, 0);
                }
                return null;
            }

            public override Cursor GetFirstChild()
            {
                var collection =
                    Context.FindObject(Current.Node, value).Select(EnumerateList).Where(NotNull).FirstOrDefault()
                    ?? Context.FindObject(Current.Node, member);
                var list = collection.SelectMany(ExpandNodes).ToList();
                if(list.Count > 0)
                {
                    return new NodeCursor(Context, this, list, 0);
                }
                return null;
            }

            private IEnumerable<INode> EnumerateList(INode list)
            {
                if(nil.Equals(list)) return Enumerable.Empty<INode>();
                var firstNode = Context.FindObject(list, first).FirstOrDefault();
                var restNode = Context.FindObject(list, rest).FirstOrDefault();
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
                    yield return Context.FindObject(list, first).FirstOrDefault();
                    list = Context.FindObject(list, rest).FirstOrDefault();
                }
            }

            private IEnumerable<NodeInfo> ExpandNodes(INode node)
            {
                if(node == null) yield break;

                // Comments on a node are turned to XML comments or PIs
                foreach(var comment in Context.FindObject(node, comment).OfType<ILiteralNode>())
                {
                    var type = comment.DataType;
                    if(type != null && !UriComparer.Equals(type, xstring) && !UriComparer.Equals(type, langString))
                    {
                        yield return new NodeInfo(XPathNodeType.ProcessingInstruction, comment);
                    }else{
                        yield return new NodeInfo(XPathNodeType.Comment, comment);
                    }
                }

                // Non-empty literal value is turned into text or significant whitespace
                if(node is ILiteralNode literal)
                {
                    if(!String.IsNullOrEmpty(literal.Value))
                    {
                        var whitespace = XmlConvert.VerifyWhitespace(literal.Value) != null;
                        yield return new NodeInfo(whitespace ? XPathNodeType.SignificantWhitespace : XPathNodeType.Text, node);
                    }
                    yield break;
                }

                var elementName = Context.FindObject(node, a).Select(GetQualifiedName).FirstOrDefault(NotNull);
                if(elementName != null)
                {
                    // The node is an element; it will have its own node
                    yield return new NodeInfo(XPathNodeType.Element, node);
                }else{
                    foreach(var value in Context.FindObject(node, value))
                    {
                        // Try to see if any other value is expandable
                        var enumerator = ExpandNodes(value).GetEnumerator();
                        if(enumerator.MoveNext())
                        {
                            yield return enumerator.Current;
                            while(enumerator.MoveNext())
                            {
                                yield return enumerator.Current;
                            }
                            break;
                        }
                    }
                }
            }

            public override Cursor Clone()
            {
                return new NodeCursor(Context, Parent?.Clone(), Nodes, Position);
            }

            public struct NodeInfo
            {
                public XPathNodeType Type { get; }
                public INode Node { get; }

                public NodeInfo(XPathNodeType type, INode node)
                {
                    Type = type;
                    Node = node;
                }
            }
        }

        class AttributeCursor : ListCursor<AttributeCursor.AttributeInfo>
        {
            public override XPathNodeType NodeType => XPathNodeType.Attribute;

            public override XmlQualifiedName QualifiedName => Current.QualifiedName;

            public override XmlQualifiedName TypeName => Current.Values.Select(GetType).FirstOrDefault(NotNull);

            public override string Value => Current.Values.Select(GetStringValue).FirstOrDefault(NotNull);

            public override string Language => Current.Values.Select(GetLanguage).FirstOrDefault(NotNull);

            public override string InnerXml => Current.Values.Select(GetXmlValue).FirstOrDefault(NotNull);

            public AttributeCursor(Context context, Cursor parent, IReadOnlyList<AttributeCursor.AttributeInfo> nodes, int position) : base(context, parent, nodes, position)
            {

            }

            public override bool MoveToNextAttribute()
            {
                return IncrementPosition();
            }

            public override Cursor Clone()
            {
                return new AttributeCursor(Context, Parent?.Clone(), Nodes, Position);
            }

            public struct AttributeInfo
            {
                public XmlQualifiedName QualifiedName { get; }
                public IEnumerable<INode> Values { get; }

                public AttributeInfo(XmlQualifiedName qualifiedName, IEnumerable<INode> values)
                {
                    QualifiedName = qualifiedName;
                    Values = values;
                }
            }
        }
    }
}
