using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace IS4.RDF
{
    internal static class UriTools
    {
        const string publicid = "publicid:";

        static readonly Regex pubIdRegex = new Regex(@"(^\s+|\s+$)|(\s+)|(\/\/)|(::)|([+:\/;'?#%])", RegexOptions.Compiled);

        public static Uri CreatePublicId(string id)
        {
            return new Uri("urn:" + publicid + TranscribePublicId(id));
        }

        public static string TranscribePublicId(string id)
        {
            return pubIdRegex.Replace(id, m => {
                if(m.Groups[1].Success)
                {
                    return "";
                }else if(m.Groups[2].Success)
                {
                    return "+";
                }else if(m.Groups[3].Success)
                {
                    return ":";
                }else if(m.Groups[4].Success)
                {
                    return ";";
                }else{
                    return Uri.EscapeDataString(m.Value);
                }
            });
        }

        static readonly Regex uriPubIdRegex = new Regex(@"(\+)|(:)|(;)|((?:%[a-fA-F0-9]{2})+)", RegexOptions.Compiled);

        public static string ExtractPublicId(Uri uri)
        {
            if(uri.IsAbsoluteUri && uri.Scheme == "urn" && String.IsNullOrEmpty(uri.Fragment))
            {
                var path = uri.AbsolutePath;
                if(path.StartsWith(publicid))
                {
                    path = path.Substring(publicid.Length);
                    return uriPubIdRegex.Replace(path, m => {
                        if(m.Groups[1].Success)
                        {
                            return " ";
                        }else if(m.Groups[2].Success)
                        {
                            return "//";
                        }else if(m.Groups[3].Success)
                        {
                            return "::";
                        }else{
                            return Uri.UnescapeDataString(m.Value);
                        }
                    });
                }
            }
            return null;
        }

        public static Uri GenerateTagUri(string authority = null, DateTimeFields dateFields = DateTimeFields.Day, string specific = null)
        {
            var date = DateTime.UtcNow.Date;
            authority = authority ?? "uuid.is4.site";
            specific = specific ?? Guid.NewGuid().ToString("D");

            var culture = CultureInfo.InvariantCulture;
            string dateString;
            switch(dateFields)
            {
                case DateTimeFields.Year:
                    dateString = date.ToString("yyyy", culture);
                    break;
                case DateTimeFields.Month:
                    dateString = date.ToString("yyyy-MM", culture);
                    break;
                case DateTimeFields.Day:
                    dateString = date.ToString("yyyy-MM-dd", culture);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dateFields));
            }
            return new Uri($"tag:{authority},{dateString}:{specific}", UriKind.Absolute);
        }

        public enum DateTimeFields
        {
            Year,
            Month,
            Day
        }

        public static Uri ComposeUri(Uri baseUri, string component)
        {
            var kind = baseUri.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative;
            return new Uri(baseUri.OriginalString + (baseUri.Fragment?.Length > 0 ? "/" : "#") + component.Replace("/", "%2F"), kind);
        }

        public static Uri GetNamespacePrefix(Uri uri)
        {
            return ComposeUri(uri, "");
        }

        public static Uri VerifyNamespacePrefix(Uri uri)
        {
            return DecomposeUri(uri, out uri, out var comp) && comp == "" ? uri : null;
        }

        public static bool DecomposeUri(Uri uri, out Uri baseUri, out string component)
        {
            if(uri.Fragment?.Length > 0)
            {
                int pos = uri.Fragment.LastIndexOf('/');
                if(pos == -1)
                {
                    pos = 0;
                }
                component = uri.Fragment.Substring(pos + 1).Replace("%2F", "/");
                baseUri = new Uri(uri.OriginalString.Substring(0, uri.OriginalString.Length - (uri.Fragment.Length - pos)), uri.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative);
            }
            baseUri = null;
            component = null;
            return false;
        }
    }
}
