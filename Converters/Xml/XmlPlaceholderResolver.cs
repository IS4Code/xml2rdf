using System;
using System.IO;
using System.Text;
using System.Xml;

namespace IS4.RDF.Converters.Xml
{
    /// <summary>
    /// Resolves all external entities as a single processing instruction with a unique target.
    /// </summary>
    public class XmlPlaceholderResolver : XmlResolver
    {
        public string InstructionTarget { get; }

        readonly string resourcestring;
        readonly byte[] resource;

        public XmlPlaceholderResolver()
        {
            InstructionTarget = $"entity{Guid.NewGuid():N}";
            resourcestring = $"<?{InstructionTarget}?>";
            resource = Encoding.UTF8.GetBytes(resourcestring);
        }

        public override bool SupportsType(Uri absoluteUri, Type type)
        {
            return type == null || type.IsAssignableFrom(typeof(MemoryStream)) || type.IsAssignableFrom(typeof(StringReader));
        }

        public override Uri ResolveUri(Uri baseUri, string relativeUri)
        {
            if(relativeUri.IndexOf("//", StringComparison.Ordinal) > 0 && !new Uri(relativeUri, UriKind.RelativeOrAbsolute).IsAbsoluteUri)
            {
                return UriTools.CreatePublicId(relativeUri);
            }
            return base.ResolveUri(baseUri, relativeUri);
        }

        public override object GetEntity(Uri absoluteUri, string role, Type type)
        {
            if(absoluteUri == null) throw new ArgumentNullException(nameof(absoluteUri));

            if(type == null || type.IsAssignableFrom(typeof(MemoryStream)))
            {
                return GetEntityAsStream(absoluteUri, role);
            }else if(type.IsAssignableFrom(typeof(StringReader)))
            {
                return GetEntityAsReader(absoluteUri, role);
            }
            throw new XmlException(null, new NotSupportedException());
        }

        public virtual MemoryStream GetEntityAsStream(Uri absoluteUri, string role)
        {
            return new MemoryStream(resource, false);
        }

        public virtual StringReader GetEntityAsReader(Uri absoluteUri, string role)
        {
            return new StringReader(resourcestring);
        }
    }
}
