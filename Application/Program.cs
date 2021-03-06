using IS4.RDF.Converters.Xml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Parsing.Handlers;
using VDS.RDF.Writing;
using VDS.RDF.Writing.Formatting;

namespace IS4.RDF.Converters.Application
{
    public class Program
    {
        public int? BufferSize { get; set; }
        public bool Quiet { get; set; }
        public List<Resource> Resources { get; } = new List<Resource>();
        public XmlResolver InputXmlResolver { get; set; } = new XmlUrlResolver();
        public XmlResolver ProcessingXmlResolver { get; set; }
        public XmlReaderSettings XmlReaderSettings { get; } = new XmlReaderSettings()
        {
            DtdProcessing = DtdProcessing.Ignore,
            ValidationFlags = XmlSchemaValidationFlags.ProcessIdentityConstraints | XmlSchemaValidationFlags.ProcessInlineSchema | XmlSchemaValidationFlags.ProcessSchemaLocation | XmlSchemaValidationFlags.ReportValidationWarnings,
            XmlResolver = new XmlPlaceholderResolver()
        };
        public XmlWriterSettings XmlWriterSettings { get; } = new XmlWriterSettings();
        public WhitespaceHandling WhitespaceHandling { get; set; } = WhitespaceHandling.Significant;
        public bool ExportDefault { get; set; }
        public bool UsePublic { get; set; }
        public RdfBrowsingMethod OutputRdfBrowsingMethod { get; set; }
        public TurtleSyntax TurtleSyntax { get; set; } = TurtleSyntax.W3C;
        public string BaseUri { get; set; }
        public bool Streaming { get; set; }

        public void Run()
        {
            if(Resources.Count <= 1)
            {
                throw new ApplicationException("At least two files have to be specified!");
            }
            var output = Resources[Resources.Count - 1];
            var input = Resources.Take(Resources.Count - 1);
            if((output.Format & ResourceFormat.StructuredMask) != 0)
            {
                GraphToStructured(input, output);
                return;
            }
            if((output.Format & ResourceFormat.GraphMask) != 0)
            {
                StructuredToGraph(input, output);
                return;
            }
            throw new InvalidOperationException("Conversion method cannot be detected.");
        }

        private void ValidateMask(IEnumerable<Resource> collection, ResourceFormat mask)
        {
            foreach(var res in collection)
            {
                if((res.Format & mask) == 0)
                {
                    throw new InvalidOperationException("The format of input resources must match!");
                }
            }
        }

        private Uri ResolveUri(string uriString)
        {
            if(uriString == null) return null;
            var uri = new Uri(uriString, UriKind.RelativeOrAbsolute);
            if(!uri.IsAbsoluteUri)
            {
                uri = InputXmlResolver.ResolveUri(null, uriString);
            }
            return uri;
        }

        private Stream OpenInput(string path)
        {
            Stream stream;
            if(path == null)
            {
                stream = BufferSize is int size ? Console.OpenStandardInput(size) : Console.OpenStandardInput();
            }else{
                stream = InputXmlResolver.GetEntity(ResolveUri(path), null, typeof(Stream)) as Stream;
                if(stream == null)
                {
                    throw new InvalidOperationException("Input file could not be opened.");
                }
            }
            return stream;
        }

        private Stream OpenOutput(string path)
        {
            Stream stream;
            if(path == null)
            {
                stream = BufferSize is int size ? Console.OpenStandardOutput(size) : Console.OpenStandardOutput();
            }else{
                stream = BufferSize is int size ? new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, size) : new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            }
            return stream;
        }

        private string GetBaseUri(string path)
        {
            switch(BaseUri)
            {
                case null:
                    return ResolveUri(path)?.AbsoluteUri;
                case "":
                    return null;
                case "tag:":
                    return UriTools.GenerateTagUri().AbsoluteUri;
                case "urn:uuid:":
                    return "urn:uuid:" + Guid.NewGuid().ToString("D");
                default:
                    return BaseUri;
            }
        }

        private void StructuredHandleInputs(IEnumerable<Resource> input, IRdfHandler handler)
        {
            var converter = new XmlNodeConverter();
            foreach(var file in input)
            {
                using(var processor = new XmlRdfProcessor(handler, ProcessingXmlResolver as XmlPlaceholderResolver)
                {
                    WhitespaceHandling = WhitespaceHandling,
                    ExportDefault = ExportDefault,
                    UseDtdAsDefaultNamespace = UsePublic
                })
                {
                    switch(file.Format)
                    {
                        case ResourceFormat.Xml:
                            using(var reader = XmlReader.Create(OpenInput(file.TargetPath), XmlReaderSettings, GetBaseUri(file.TargetPath)))
                            {
                                converter.Convert(reader, processor);
                            }
                            break;
                        default:
                            throw new ApplicationException("Unsupported input format!");
                    }
                }
            }
        }

        private ITripleFormatter GetFormatter(ResourceFormat format, INamespaceMapper namespaceMapper)
        {
            switch(format)
            {
                case ResourceFormat.Turtle:
                    switch(TurtleSyntax)
                    {
                        case TurtleSyntax.Original:
                            return namespaceMapper == null ? new TurtleFormatter() : new TurtleFormatter(namespaceMapper);
                        case TurtleSyntax.W3C:
                            return namespaceMapper == null ? new TurtleW3CFormatter() : new TurtleW3CFormatter(namespaceMapper);
                        default:
                            throw new InvalidOperationException("Unsupported Turtle syntax!");
                    }
                case ResourceFormat.RdfXml:
                    return new RdfXmlFormatter();
                case ResourceFormat.JsonLd:
                    goto default;
                case ResourceFormat.Csv:
                    return new CsvFormatter();
                case ResourceFormat.Html:
                    goto default;
                default:
                    throw new ApplicationException("Unsupported output format!");
            }
        }

        private IRdfWriter GetWriter(ResourceFormat format)
        {
            switch(format)
            {
                case ResourceFormat.Turtle:
                    return new CompressingTurtleWriter(TurtleSyntax);
                case ResourceFormat.RdfXml:
                    return new RdfXmlWriter();
                case ResourceFormat.JsonLd:
                    return new StoreRdfWriter(new JsonLdWriter());
                case ResourceFormat.Csv:
                    return new CsvWriter();
                case ResourceFormat.Html:
                    return new HtmlWriter();
                default:
                    throw new ApplicationException("Unsupported output format!");
            }
        }

        class StoreRdfWriter : IRdfWriter
        {
            readonly IStoreWriter storeWriter;

            public event RdfWriterWarning Warning {
                add {
                    storeWriter.Warning += value.Invoke;
                }
                remove {
                    storeWriter.Warning -= value.Invoke;
                }
            }

            ITripleStore GetStore(IGraph graph)
            {
                var store = new TripleStore();
                store.Add(graph);
                return store;
            }

            public void Save(IGraph g, string filename)
            {
                storeWriter.Save(GetStore(g), filename);
            }

            public void Save(IGraph g, TextWriter output)
            {
                storeWriter.Save(GetStore(g), output);
            }

            public void Save(IGraph g, TextWriter output, bool leaveOpen)
            {
                storeWriter.Save(GetStore(g), output, leaveOpen);
            }

            public StoreRdfWriter(IStoreWriter storeWriter)
            {
                this.storeWriter = storeWriter;
            }
        }

        class NamespaceWriteThroughHandler : WriteThroughHandler
        {
            readonly INamespaceMapper namespaceMapper;

            public NamespaceWriteThroughHandler(ITripleFormatter formatter, INamespaceMapper namespaceMapper, TextWriter writer) : base(formatter, writer)
            {
                this.namespaceMapper = namespaceMapper;
            }

            protected override bool HandleNamespaceInternal(string prefix, Uri namespaceUri)
            {
                namespaceMapper.AddNamespace(prefix, namespaceUri);
                return base.HandleNamespaceInternal(prefix, namespaceUri);
            }
        }

        private void StructuredToGraph(IEnumerable<Resource> input, Resource output)
        {
            ValidateMask(input, ResourceFormat.StructuredMask);
            
            if(Streaming)
            {
                var namespaceMapper = new NamespaceMapper();
                var formatter = GetFormatter(output.Format, namespaceMapper);
                using(var writer = new StreamWriter(OpenOutput(output.TargetPath)))
                {
                    var handler = new NamespaceWriteThroughHandler(formatter, namespaceMapper, writer);
                    StructuredHandleInputs(input, handler);
                }
            }else{
                var rdfWriter = GetWriter(output.Format);
                if(rdfWriter is IPrettyPrintingWriter prettyWriter)
                {
                    prettyWriter.PrettyPrintMode = true;
                }
                var graph = new Graph();
                StructuredHandleInputs(input, new GraphHandler(graph));
                using(var writer = new StreamWriter(OpenOutput(output.TargetPath)))
                {
                    rdfWriter.Save(graph, writer);
                }
            }
        }

        class StoreRdfReader : IRdfReader
        {
            readonly IStoreReader storeReader;

            public event RdfReaderWarning Warning {
                add {
                    storeReader.Warning += value.Invoke;
                }
                remove {
                    storeReader.Warning -= value.Invoke;
                }
            }

            public void Load(IGraph g, StreamReader input)
            {
                storeReader.Load(new GraphHandler(g), input);
            }

            public void Load(IGraph g, TextReader input)
            {
                storeReader.Load(new GraphHandler(g), input);
            }

            public void Load(IGraph g, string filename)
            {
                storeReader.Load(new GraphHandler(g), filename);
            }

            public void Load(IRdfHandler handler, StreamReader input)
            {
                storeReader.Load(handler, input);
            }

            public void Load(IRdfHandler handler, TextReader input)
            {
                storeReader.Load(handler, input);
            }

            public void Load(IRdfHandler handler, string filename)
            {
                storeReader.Load(handler, filename);
            }

            public StoreRdfReader(IStoreReader storeReader)
            {
                this.storeReader = storeReader;
            }
        }

        private IRdfReader GetReader(ResourceFormat format)
        {
            switch(format)
            {
                case ResourceFormat.Turtle:
                    return new TurtleParser(TurtleSyntax);
                case ResourceFormat.RdfXml:
                    return new RdfXmlParser();
                case ResourceFormat.JsonLd:
                    return new StoreRdfReader(new JsonLdParser());
                default:
                    throw new ApplicationException("Unsupported input format!");
            }
        }

        private void GraphToStructured(IEnumerable<Resource> input, Resource output)
        {
            ValidateMask(input, ResourceFormat.GraphMask);

            var graph = new Graph();
            foreach(var file in input)
            {
                if(file.Format == ResourceFormat.RdfGeneric)
                {
                    if(file.TargetPath == null)
                    {
                        throw new ApplicationException("Standard input resource must have a concrete format specified.");
                    }
                    UriLoader.Load(graph, ResolveUri(file.TargetPath));
                }else{
                    var rdfReader = GetReader(file.Format);
                    using(var reader = new StreamReader(OpenInput(file.TargetPath), true))
                    {
                        var baseUri = GetBaseUri(file.TargetPath);
                        graph.BaseUri = baseUri != null ? new Uri(baseUri) : null;
                        rdfReader.Load(graph, reader);
                    }
                }
            }

            switch(output.Format)
            {
                case ResourceFormat.Xml:
                    using(var writer = XmlWriter.Create(OpenOutput(output.TargetPath), XmlWriterSettings))
                    {
                        var root = graph.CreateUriNode(new Uri(GetBaseUri(output.TargetPath)));
                        if(!graph.GetTriplesWithSubject(root).Any())
                        {
                            throw new ApplicationException("Base URI must refer to an existing root!");
                        }
                        switch(OutputRdfBrowsingMethod)
                        {
                            case RdfBrowsingMethod.Writer:
                                new RdfXmlConverter(graph).Write(writer, root);
                                break;
                            case RdfBrowsingMethod.Navigator:
                                new RdfXPathNavigator(graph, root).WriteSubtree(writer);
                                break;
                            default:
                                throw new ApplicationException("Unsupported output method!");
                        }
                    }
                    break;
                default:
                    throw new ApplicationException("Unsupported output format!");
            }
        }

        public enum RdfBrowsingMethod
        {
            Writer,
            Navigator
        }

        public class Resource
        {
            static readonly Dictionary<string, ResourceFormat> resourceTypes = new Dictionary<string, ResourceFormat>(StringComparer.OrdinalIgnoreCase)
            {
                { "xml", ResourceFormat.Xml },

                { "rdf", ResourceFormat.RdfGeneric },
                { "ttl", ResourceFormat.Turtle },
                { "jsonld", ResourceFormat.JsonLd },
                { "rdfxml", ResourceFormat.RdfXml },
                { "rdf+xml", ResourceFormat.RdfXml },
                { "csv", ResourceFormat.Csv },
                { "html", ResourceFormat.Html }
            };

            static readonly HashSet<string> standardPaths = new HashSet<string>(StringComparer.Ordinal)
            {
                "-", "php://stdin", "php://stdout", "file:///dev/fd/0", "file:///dev/fd/1"
            };

            public ResourceFormat Format { get; }
            public string TargetPath { get; }

            public Resource(ResourceFormat format, string path)
            {
                Format = format;
                TargetPath = path;
            }

            public Resource(string argument) : this(ParsePath(ref argument), argument)
            {

            }

            private static ResourceFormat ParsePath(ref string argument)
            {
                var format = ExtractFormat(ref argument);
                if(standardPaths.Contains(argument))
                {
                    argument = null;
                }
                return format;
            }

            private static ResourceFormat ExtractFormat(ref string argument)
            {
                int colon = argument.IndexOf(':');
                if(colon != -1 && resourceTypes.TryGetValue(argument.Substring(0, colon), out var resourceFormat))
                {
                    argument = argument.Substring(colon + 1);
                    return resourceFormat;
                }
                var format = Path.GetExtension(argument);
                if(String.IsNullOrWhiteSpace(format))
                {
                    throw new ArgumentException($"Cannot extract format info from the argument. Please add one of {String.Concat(", ", resourceTypes.Keys.Select(k => k + ":"))} at the beginning.", nameof(argument));
                }
                format = format.TrimStart('.');
                if(!resourceTypes.TryGetValue(format, out resourceFormat))
                {
                    throw new ArgumentException($"Unknown argument format '{format}'. Supported formats are {String.Concat(", ", resourceTypes.Keys)}.", nameof(argument));
                }
                return resourceFormat;
            }
        }

        [Flags]
        public enum ResourceFormat
        {
            Turtle = 1,
            RdfXml = 2,
            JsonLd = 4,
            Html = 8,
            Csv = 16,
            RdfGeneric = 32,
            GraphMask = 255,

            Xml = 256,
            StructuredMask = 65280,
        }

#if DEBUG
        static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

			var options = new Options();
			try{
				options.Parse(args);

                options.Program.Run();
			}catch(Exception e) when(!Debugger.IsAttached)
			{
				if(options.Program.Quiet != true)
				{
					options.Log(e.Message);
				}
            }
        }

        class Options : ApplicationOptions
        {
            public Program Program { get; } = new Program();

            protected override TextWriter Error => Console.Error;

            protected override TextWriter Out => Console.Out;

            protected override int OutputWidth{
                get{
                    try{
                        return Console.WindowWidth;
                    }catch{
                        return 80;
                    }
                }
            }

            protected override string Usage => "[options] [input...] [output]";

            public override void Description()
            {
                base.Description();
                Error.WriteLine();
                Error.Write(" ");
                OutputWrapPad("This applications converts between XML and RDF files.", 1);
            }

            public override IList<OptionInfo> GetOptions()
            {
                return new OptionInfoCollection{
                    {"q", "quiet", null, "does not print any additional messages"},
                    {"s", "schema", "[URI]|[path]", "uses schema for a specific namespace"},
                    {"b", "base-uri", "[URI]", "sets the base URI for conversion"},
                    {"d", "dtd", FormatEnum(typeof(DtdProcessing), " / "), "sets input DTD processing"},
                    {"w", "whitespace", FormatEnum(typeof(WhitespaceHandling), " / "), "sets input whitespace handling"},
                    {"v", "validation", FormatEnum(typeof(ValidationType), " / "), "sets input validation"},
                    {"om", "output-method", FormatEnum(typeof(RdfBrowsingMethod), " / "), "sets output XML method"},
                    {"ts", "turtle-syntax", FormatEnum(typeof(TurtleSyntax), " / "), "sets supported Turtle syntax"},
                    {"ed", "export-default", null, "exports default attributes"},
                    {"up", "use-public", null, "uses DTD PUBLIC identifier as the default namespace"},
                    {"st", "streaming", null, "produce RDF output in streaming fashion"},
                    {"bs", "buffer-size", "size", "sets the size of the buffers"},
                    {"?", "help", null, "displays this help message"},
                };
            }

            protected override void Notes()
            {
                Error.WriteLine();
                Error.WriteLine("Example: " + ExecutableName + " input.xml output.ttl");
            }

            protected override OptionArgument OnOptionFound(string option)
            {
                switch(option)
                {
                    case "s":
                    case "schema":
                        return OptionArgument.Required;
                    case "b":
                    case "base-uri":
                        return OptionArgument.Required;
                    case "d":
                    case "dtd":
                        return OptionArgument.Required;
                    case "w":
                    case "whitespace":
                        return OptionArgument.Required;
                    case "v":
                    case "validation":
                        return OptionArgument.Required;
                    case "om":
                    case "output-method":
                        return OptionArgument.Required;
                    case "bs":
                    case "buffer-size":
                        return OptionArgument.Required;
                    case "ts":
                    case "turtle-syntax":
                        return OptionArgument.Required;
                    case "q":
                    case "quiet":
                        if(Program.Quiet)
                        {
                            throw OptionAlreadySpecified(option);
                        }
                        Program.Quiet = true;
                        Program.XmlReaderSettings.ValidationFlags &= ~XmlSchemaValidationFlags.ReportValidationWarnings;
                        return OptionArgument.None;
                    case "ed":
                    case "export-default":
                        if(Program.ExportDefault)
                        {
                            throw OptionAlreadySpecified(option);
                        }
                        Program.ExportDefault = true;
                        return OptionArgument.None;
                    case "up":
                    case "use-public":
                        if(Program.UsePublic)
                        {
                            throw OptionAlreadySpecified(option);
                        }
                        Program.UsePublic = true;
                        if(Program.XmlReaderSettings.DtdProcessing == DtdProcessing.Ignore)
                        {
                            Program.XmlReaderSettings.DtdProcessing = DtdProcessing.Parse;
                        }
                        return OptionArgument.None;
                    case "st":
                    case "streaming":
                        if(Program.Streaming)
                        {
                            throw OptionAlreadySpecified(option);
                        }
                        Program.Streaming = true;
                        return OptionArgument.None;
                    case "?":
                    case "help":
                        Help();
                        return OptionArgument.None;
                    default:
                        throw UnrecognizedOption(option);
                }
            }

            private string FormatEnum(Type type, string separator)
            {
                return String.Join(separator, Enum.GetNames(type).Select(n => n.ToLowerInvariant()));
            }

            private TEnum ParseEnum<TEnum>(string argument) where TEnum : struct
            {
                if(!Enum.TryParse<TEnum>(argument, true, out var result))
                {
                    throw ArgumentInvalid(argument, FormatEnum(typeof(TEnum), " / "));
                }
                return result;
            }

            protected override void OnOptionArgumentFound(string option, string argument)
            {
                switch(option)
                {
                    case "s":
                    case "schema":
                        var split = argument.Split(new[] { '|' }, 2);
                        if(split.Length <= 1)
                        {
                            throw ArgumentInvalid(option, "[URI]|[path]");
                        }
                        Program.XmlReaderSettings.Schemas.Add(split[0], split[1]);
                        break;
                    case "b":
                    case "base-uri":
                        if(Program.BaseUri != null)
                        {
                            throw OptionAlreadySpecified(option);
                        }
                        Program.BaseUri = argument;
                        break;
                    case "d":
                    case "dtd":
                        Program.XmlReaderSettings.DtdProcessing = ParseEnum<DtdProcessing>(argument);
                        break;
                    case "w":
                    case "whitespace":
                        Program.WhitespaceHandling = ParseEnum<WhitespaceHandling>(argument);
                        break;
                    case "v":
                    case "validation":
                        Program.XmlReaderSettings.ValidationType = ParseEnum<ValidationType>(argument);
                        break;
                    case "om":
                    case "output-method":
                        Program.OutputRdfBrowsingMethod = ParseEnum<RdfBrowsingMethod>(argument);
                        break;
                    case "turtle-syntax":
                        Program.TurtleSyntax = ParseEnum<TurtleSyntax>(argument);
                        break;
                    case "buffer-size":
                        if(Program.BufferSize != null)
                        {
                            throw OptionAlreadySpecified(option);
                        }
                        int bufferSize;
                        if(!Int32.TryParse(argument, out bufferSize))
                        {
                            throw ArgumentInvalid(option, "integer");
                        }
                        Program.BufferSize = bufferSize;
                        break;
                }
            }

            protected override OperandState OnOperandFound(string operand)
            {
                Program.Resources.Add(new Resource(operand));
                return OperandState.ContinueOptions;
            }
        }
#endif
    }
}
