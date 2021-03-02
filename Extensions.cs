using System;
using System.Collections.Generic;
using System.Linq;
using VDS.RDF;

namespace IS4.RDF
{
    internal static class Extensions
    {
        public static void HandleTriple(this IRdfHandler handler, INode subj, INode pred, INode obj)
        {
            handler.HandleTriple(new Triple(subj, pred, obj));
        }

        public static bool ContainsTriple(this IGraph graph, INode subj, INode pred, INode obj)
        {
            return graph.ContainsTriple(new Triple(subj, pred, obj));
        }

        public static IEnumerable<INode> FindObject(this IGraph graph, INode subj, INode pred)
        {
            return graph.GetTriplesWithSubjectPredicate(subj, pred).Select(t => t.Object);
        }

        public static INode CreateNode(this IRdfHandler handler, Uri uri)
        {
            return uri != null ? (INode)handler.CreateUriNode(uri) : handler.CreateBlankNode();
        }

        public static IEnumerable<(INode pred, INode obj)> FindPredicateObject(this IGraph graph, INode subj)
        {
            return graph.GetTriplesWithSubject(subj).Select(t => (t.Predicate, t.Object));
        }

        public static string GetString(this Uri uri)
        {
            return uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.OriginalString;
        }
    }
}
