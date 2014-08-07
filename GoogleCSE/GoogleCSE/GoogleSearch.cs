﻿using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml.Linq;

namespace GoogleCSE
{
    /// <summary>
    /// This represents a single search result
    /// </summary>
    public class GoogleSearchResult
    {
        /// <summary>
        /// The mine type of the resulting link
        /// </summary>
        public string Mime { get; set; }
        /// <summary>
        /// The URL of the result
        /// </summary>
        public string Url { get; set; }
        /// <summary>
        /// Title of the result
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// Description that should show in results
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// A collection for storing more advanced information about the search.
    /// </summary>
    public class GoogleSearchResults
    {
        /// <summary>
        /// The normal organic search results
        /// </summary>
        public List<GoogleSearchResult> Results { get; set; }
        /// <summary>
        /// Specially designed search results. Advertisements. 
        /// </summary>
        public List<GoogleSearchResult> Promotions { get; set; }
        /// <summary>
        /// Possible search refinement labels 
        /// </summary>
        public Dictionary<string, string> Labels { get; set; }
        /// <summary>
        /// total results, all these results might not actually be available.
        /// </summary>
        public long TotalResults { get; set; }
        /// <summary>
        /// Amount of time taken to preform the search.
        /// </summary>
        public double SearchTime { get; set; }
    }

    //public static class GoogleApiMethod
    //{
    //    public static string XML = "XML";
    //}

// ReSharper disable InconsistentNaming
    public enum GoogleSearchMethod { XML, CSE};
// ReSharper restore InconsistentNaming

    /// <summary>
    /// Class to instantiate for searching a Google custom search engine. You will need to provide your cx
    /// https://developers.google.com/custom-search/docs/xml_results?hl=en&csw=1
    /// You can get your CSE from https://www.google.com/cse/
    /// </summary>
    public class GoogleSearch
    {
        private const string GoogleUrl = "http://www.google.com/search";
        private const string GoogleCseUrl = "https://www.googleapis.com/customsearch/v1";
        private readonly int _maxPages;
        
        /// <summary>
        /// The API used. This was the XML API but will likely move towards the CSE Atom api as a default in the future. XML gives a larger page size.
        /// </summary>
        public GoogleSearchMethod Method;

        /// <summary>
        /// Options used to build the URL. 
        /// </summary>
        public readonly Dictionary<string, string> Options = new Dictionary<string, string>();

        /// <summary>
        /// Create a Google searcher. 
        /// </summary>
        /// <param name="cx">Your search engine ID from https://www.google.com/cse/ </param>
        /// <param name="hl">(optional) language</param>
        /// <param name="extraOptions">(optional)If you need to supply more custom options passed to google please include them here. These options will override any defaults</param>
        /// <param name="pageSize">(optional) Number of results per page, Default are 10 but there is a max of 20 with the XMl API</param>
        /// <param name="maxPages">(optional) Number of pages to grab. Larger number will be slower.  Also each page is a new search on your paid search limit.</param>
        /// <param name="start">The result offset, use to page through results.</param>
        /// <param name="method">(optional) API Used to gather the results. Default is XML https://developers.google.com/custom-search/docs/xml_results the other option is CSE https://developers.google.com/custom-search/json-api/v1/reference/cse/list 
        /// Only the CSA interface gives promotions as a separate object
        /// </param>
        /// <param name="key">Api Key, only needed if you are using the CSE interface</param>
        public GoogleSearch(string cx, string hl = "en", Dictionary<string, string> extraOptions = null, int pageSize = 10, int maxPages = 20, int start = 1, GoogleSearchMethod method = GoogleSearchMethod.XML, string key = "")
        {
            
            Options["client"] = "google-csbe";
            Options["output"] = "xml_no_dtd";
            Options["cx"] = cx;
            Options["hl"] = hl;
            Options["num"] = pageSize.ToString(CultureInfo.InvariantCulture);
            if (start > 1)
            {
                Options["start"] = start.ToString(CultureInfo.InvariantCulture);
            }
            if (!string.IsNullOrEmpty(key))
            {
                Options["key"] = key;
            }
            _maxPages = maxPages;
            Method = method;
            if (extraOptions == null) return;
            foreach (var option in extraOptions)
            {
                Options[option.Key] = option.Value;
            }
        }

        /// <summary>
        /// build a query url
        /// </summary>
        /// <returns></returns>
        protected string QueryUrl()
        {
            var sb = new StringBuilder();
            switch (Method)
            {
                case GoogleSearchMethod.XML:
                    sb.Append(GoogleUrl);
                    break;
                case GoogleSearchMethod.CSE:
                    sb.Append(GoogleCseUrl);
                    Options["alt"] = "ATOM";//force atom. 
                    Options["prettyPrint"] = "false";// we don't need the result human readable
                    //Options.Add("alt","ATOM");
                    //Options.Add("prettyPrint","false"); 
                    break;
            }
            for (var i = 0; i < Options.Keys.Count; i++)
            {
                var key = Options.Keys.ElementAt(i);
                sb.Append(i == 0 ? '?' : '&');
                sb.Append(key);
                sb.Append('=');
                sb.Append(HttpUtility.UrlEncode(Options[key]));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Get the search results 
        /// </summary>
        /// <param name="term">term to search</param>
        /// <param name="label">If you are using custom refinements this will help you search part of your site.</param>
        /// <returns></returns>
        public List<GoogleSearchResult> Search(string term, string label = "")
        {

            if (!string.IsNullOrWhiteSpace(label))
            {
                Options["q"] = term + " more:" + label;
            }
            else
            {
                Options["q"] = term;
            }
            var url = QueryUrl();
            return RecursiveResults(url);
        }

        public GoogleSearchResults SearchDetailed(string term, string label = "")
        {
            if (!string.IsNullOrWhiteSpace(label))
            {
                Options["q"] = term + " more:" + label;
            }
            else
            {
                Options["q"] = term;
            }
            var url = QueryUrl();
            return RecursiveResultDetailed(url);
        }

        private GoogleSearchResults RecursiveResultDetailed(string url, int depth = 1)
        {
            var ret = new GoogleSearchResults
            {
                Results = new List<GoogleSearchResult>(),
                Promotions = new List<GoogleSearchResult>(),
                Labels = new Dictionary<string, string>(),
                TotalResults = 0,
                SearchTime = 0
            };
            if (Method == GoogleSearchMethod.CSE)
            {
                var xResults = XDocument.Load(url);
                var data = xResults.Root;
                if (data == null)
                {
                    return ret;
                }
                var nsOpenSearch = data.GetNamespaceOfPrefix("opensearch");
                var nsCse = data.GetNamespaceOfPrefix("cse");
                var nsAtom = data.GetDefaultNamespace();
                if (depth == 1) // only check this on the first result
                {
                    //get the search information
                    var searchInformation = data.Element(nsCse + "searchInformation");
                    if (searchInformation != null)
                    {
                        var totalResults = searchInformation.Element(nsCse + "totalResults");
                        if (totalResults != null)
                            ret.TotalResults = long.Parse(totalResults.Value);
                        var searchTime = searchInformation.Element(nsCse + "searchTime");
                        if (searchTime != null)
                            ret.SearchTime = double.Parse(searchTime.Value);
                    }
                    //TODO Get labels

                    //TODO Get Promotions
                }
                //get search Results
                //foreach (var searchResult in data.Descendants(nsAtom+"entry"))
                //{
                //    ret.Results.Add(ParseCseResult(searchResult));
                //}
                ret.Results.AddRange(data.Descendants(nsAtom + "entry").Select(ParseCseResult));
                if (depth < _maxPages)
                {
                    var nextPage = data.Elements(nsOpenSearch + "Query").FirstOrDefault(e => e.Attribute("role").Value == "cse:nextPage");
                    if (nextPage != null)
                    {
                        Options["start"] = nextPage.Attribute("startIndex").Value;
                        var nextUrl = QueryUrl();
                        var theRestOfThePages = RecursiveResultDetailed(nextUrl, ++depth);
                        ret.Results.AddRange(theRestOfThePages.Results);
                    }
                }
            }
            if (Method == GoogleSearchMethod.XML)
            {
                //TODO
            }
            return ret;
        }


        private GoogleSearchResult ParseCseResult(XElement searchResult)
        {
            var nsCse = searchResult.GetNamespaceOfPrefix("cse");
            var nsAtom = searchResult.GetDefaultNamespace();
            var tmpResult = new GoogleSearchResult();
            var title = searchResult.Element(nsAtom + "title");
            if (title != null) tmpResult.Title = title.Value;
            var link = searchResult.Element(nsAtom + "link");
            if (link != null)
            {
                if (link.Attribute("href") != null)
                {
                    tmpResult.Url = link.Attribute("href").Value;
                }
            }
            var description = searchResult.Element(nsAtom + "summary");
            if (description != null)
            {
                tmpResult.Description = description.Value;
            }
            var mime = searchResult.Element(nsCse + "mime");
            if (mime != null)
            {
                tmpResult.Mime = mime.Value;
            }
            return tmpResult;
        }


        private GoogleSearchResult ParseXmlResult(XElement r)
        {
            var tmpR = new GoogleSearchResult();
            var xAttribute = r.Attribute("MIME");
            if (xAttribute != null) tmpR.Mime = xAttribute.Value;
            var xElement = r.Element("U");
            if (xElement != null) tmpR.Url = xElement.Value;
            var element = r.Element("T");
            if (element != null) tmpR.Title = element.Value;
            var xElement1 = r.Element("S");// won't work for promoted results but they are just shown inline with normal results
            if (xElement1 != null) tmpR.Description = xElement1.Value;
            return tmpR;
        }

        /// <summary>
        /// Walk through pages of results
        /// </summary>
        /// <param name="url"></param>
        /// <param name="depth"></param>
        /// <returns></returns>
        private List<GoogleSearchResult> RecursiveResults(string url, int depth = 1)
        {
            var ret = new List<GoogleSearchResult>();
            if (Method == GoogleSearchMethod.XML)
            {
                var xResults = XDocument.Load(url);
                var data = xResults.Descendants("RES").FirstOrDefault();
                if (data == null) return ret;
                ret.AddRange(data.Descendants("R").Select(ParseXmlResult));
                var nb = data.Element("NB");
                if (nb == null) return ret;
                var nu = nb.Element("NU");
                if (nu == null) return ret;
                if (depth < _maxPages)
                {
                    ret.AddRange(RecursiveResults("http://www.google.com" + nu.Value, ++depth));
                }
            }
            if (Method == GoogleSearchMethod.CSE)
            {
                
                var xResults = XDocument.Load(url);
                var data = xResults.Root;
                if (data == null)
                {
                    return ret;
                }
                var nsOpenSearch = data.GetNamespaceOfPrefix("opensearch");
                var nsAtom = data.GetDefaultNamespace();
                ret.AddRange(data.Descendants(nsAtom + "entry").Select(ParseCseResult));
                if (depth < _maxPages)
                {
                    var nextPage = data.Elements(nsOpenSearch + "Query").FirstOrDefault(e => e.Attribute("role").Value == "cse:nextPage");
                    if (nextPage != null)
                    {
                        Options["start"] = nextPage.Attribute("startIndex").Value;
                        var nextUrl = QueryUrl();
                        ret.AddRange(RecursiveResults(nextUrl, ++depth));
                    }
                }
            }

            return ret;
        }


        
    }

}
