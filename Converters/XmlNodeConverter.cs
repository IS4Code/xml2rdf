using IS4.RDF.Converters.Xml;
using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;

namespace IS4.RDF.Converters
{
    public class XmlNodeConverter : INodeConverter<XmlReader>
    {
        public bool UseReflection { get; set; } = true;

        public TNode Convert<TNode, TUriNode>(XmlReader reader, IXmlNodeProcessor<TNode, TUriNode> processor)
            where TUriNode : TNode
        {
            var wrapper = new XmlReaderWrapper(reader, UseReflection);
            return processor.ProcessDocument(wrapper, documentNode => ReaderContainer(processor, reader, documentNode, default));
        }
        
        private IEnumerator<TNode> ReaderContainer<TNode, TUriNode>(IXmlNodeProcessor<TNode, TUriNode> processor, XmlReader reader, TUriNode baseNode, TUriNode defaultNamespace)
             where TUriNode : TNode
        {
            var wrapper = new XmlReaderWrapper(reader, UseReflection);
            var originalBaseUri = wrapper.BaseUri;
            while(reader.Read())
            {
                switch(reader.NodeType)
                {
                    case XmlNodeType.DocumentType:
                        processor.ProcessDocumentType(reader.GetAttribute("PUBLIC"), reader.GetAttribute("SYSTEM"), reader.CanResolveEntity ? null : reader.Value, String.IsNullOrWhiteSpace(reader.Value), baseNode, ref defaultNamespace);
                        continue;

                    case XmlNodeType.Whitespace:
                        yield return processor.ProcessWhitespace(wrapper, false);
                        continue;

                    case XmlNodeType.SignificantWhitespace:
                        yield return processor.ProcessWhitespace(wrapper, true);
                        continue;

                    case XmlNodeType.Text:
                        yield return processor.ProcessText(wrapper, false);
                        continue;

                    case XmlNodeType.CDATA:
                        yield return processor.ProcessText(wrapper, true);
                        continue;

                    case XmlNodeType.Comment:
                        yield return processor.ProcessComment(wrapper);
                        continue;

                    case XmlNodeType.ProcessingInstruction:
                        yield return processor.ProcessProcessingInstruction(wrapper, defaultNamespace);
                        continue;

                    case XmlNodeType.EntityReference:
                        yield return processor.ProcessEntityReference(wrapper);
                        continue;

                    case XmlNodeType.Element:
                        yield return processor.ProcessElement(wrapper, baseNode, originalBaseUri, defaultNamespace, innerBaseNode => ReaderContainer(processor, reader, innerBaseNode, defaultNamespace));
                        continue;
                }
                if(reader.NodeType == XmlNodeType.EndElement)
                {
                    break;
                }
            }
        }

        struct XmlReaderWrapper : IXmlProvider
        {
            public XmlReader Reader { get; }

            readonly bool UseReflection;

            public XmlReaderWrapper(XmlReader reader, bool reflectionDatatype)
            {
                Reader = reader;
                UseReflection = reflectionDatatype;
            }

            public string Language => Reader.XmlLang;

            public XmlQualifiedName QualifiedName => new XmlQualifiedName(Reader.LocalName, Reader.NamespaceURI);

            public string LocalName => Reader.LocalName;

            public string PrefixedName => Reader.Name;

            public Uri Namespace => String.IsNullOrEmpty(Reader.NamespaceURI) ? null : new Uri(Reader.NamespaceURI);

            public string Prefix => Reader.Prefix;

            public XmlQualifiedName TypeName{
                get{
                    var attr = Reader.SchemaInfo?.SchemaAttribute;
                    if(attr != null)
                    {
                        if(!attr.SchemaTypeName.IsEmpty)
                        {
                            return attr.SchemaTypeName;
                        }
                        XmlSchemaType type = attr.SchemaType;
                        if(type != null)
                        {
                            while(type != null && type.QualifiedName.IsEmpty)
                            {
                                type = type.BaseXmlSchemaType;
                            }
                            if(type != null)
                            {
                                return type.QualifiedName;
                            }
                        }
                    }else if(UseReflection)
                    {
                        try
                        {
                            var value = Reader.GetType().InvokeMember("SchemaType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.GetField | System.Reflection.BindingFlags.GetProperty, null, Reader, null) as XmlSchemaDatatype;
                            if(value != null && value.TypeCode != XmlTypeCode.String)
                            {
                                return new XmlQualifiedName(value.TokenizedType.ToString(), "http://www.w3.org/2001/XMLSchema");
                            }
                        }catch(MemberAccessException)
                        {

                        }
                    }
                    return null;
                }
            }

            public Type Type => Reader.ValueType;

            public string Value => Reader.Value;

            public Uri BaseUri => String.IsNullOrEmpty(Reader.BaseURI) ? null : new Uri(Reader.BaseURI);

            public bool IsDefault => Reader.IsDefault;

            public bool IsEmpty => Reader.IsEmptyElement;

            public bool MoveToNextAttribute()
            {
                return Reader.MoveToNextAttribute();
            }

            public IDictionary<string, string> GetNamespacesInScope(XmlNamespaceScope scope)
            {
                if(Reader is IXmlNamespaceResolver resolver) return resolver.GetNamespacesInScope(scope);
                throw new NotSupportedException();
            }

            public string LookupNamespace(string prefix)
            {
                return Reader.LookupNamespace(prefix);
            }

            public string LookupPrefix(string namespaceName)
            {
                if(Reader is IXmlNamespaceResolver resolver) return resolver.LookupPrefix(namespaceName);
                throw new NotSupportedException();
            }
        }
    }
}
