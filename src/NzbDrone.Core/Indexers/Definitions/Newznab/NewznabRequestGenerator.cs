using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Indexers.Newznab
{
    public class NewznabRequestGenerator : IIndexerRequestGenerator
    {
        private readonly INewznabCapabilitiesProvider _capabilitiesProvider;
        public int MaxPages { get; set; }
        public int PageSize { get; set; }
        public NewznabSettings Settings { get; set; }

        public NewznabRequestGenerator(INewznabCapabilitiesProvider capabilitiesProvider)
        {
            _capabilitiesProvider = capabilitiesProvider;

            MaxPages = 30;
            PageSize = 100;
        }

        private bool SupportsSearch
        {
            get
            {
                var capabilities = _capabilitiesProvider.GetCapabilities(Settings);

                return capabilities.SearchParams != null &&
                       capabilities.SearchParams.Contains(SearchParam.Q);
            }
        }

        private bool SupportsImdbSearch
        {
            get
            {
                var capabilities = _capabilitiesProvider.GetCapabilities(Settings);

                return capabilities.MovieSearchParams != null &&
                       capabilities.MovieSearchParams.Contains(MovieSearchParam.ImdbId);
            }
        }

        private bool SupportsTmdbSearch
        {
            get
            {
                var capabilities = _capabilitiesProvider.GetCapabilities(Settings);

                return capabilities.MovieSearchParams != null &&
                       capabilities.MovieSearchParams.Contains(MovieSearchParam.TmdbId);
            }
        }

        private bool SupportsAggregatedIdSearch
        {
            get
            {
                var capabilities = _capabilitiesProvider.GetCapabilities(Settings);

                // TODO: Fix this, return capabilities.SupportsAggregateIdSearch;
                return true;
            }
        }

        public IndexerPageableRequestChain GetSearchRequests(MovieSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();

            AddMovieIdPageableRequests(pageableRequests, MaxPages, searchCriteria.Categories, searchCriteria);

            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(MusicSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        public IndexerPageableRequestChain GetSearchRequests(TvSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        public IndexerPageableRequestChain GetSearchRequests(BookSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        public IndexerPageableRequestChain GetSearchRequests(BasicSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();

            pageableRequests.Add(GetPagedRequests(MaxPages,
                    searchCriteria.Categories,
                    "search",
                    string.Format("&q={0}", NewsnabifyTitle(searchCriteria.SearchTerm))));

            return pageableRequests;
        }

        private void AddMovieIdPageableRequests(IndexerPageableRequestChain chain, int maxPages, IEnumerable<int> categories, MovieSearchCriteria searchCriteria)
        {
            var includeTmdbSearch = SupportsTmdbSearch && searchCriteria.TmdbId > 0;
            var includeImdbSearch = SupportsImdbSearch && searchCriteria.ImdbId.IsNotNullOrWhiteSpace();

            if (SupportsAggregatedIdSearch && (includeTmdbSearch || includeImdbSearch))
            {
                var ids = "";

                if (includeTmdbSearch)
                {
                    ids += "&tmdbid=" + searchCriteria.TmdbId;
                }

                if (includeImdbSearch)
                {
                    ids += "&imdbid=" + searchCriteria.ImdbId.Substring(2);
                }

                chain.Add(GetPagedRequests(maxPages, categories, "movie", ids));
            }
            else
            {
                if (includeTmdbSearch)
                {
                    chain.Add(GetPagedRequests(maxPages,
                        categories,
                        "movie",
                        string.Format("&tmdbid={0}", searchCriteria.TmdbId)));
                }
                else if (includeImdbSearch)
                {
                    chain.Add(GetPagedRequests(maxPages,
                        categories,
                        "movie",
                        string.Format("&imdbid={0}", searchCriteria.ImdbId.Substring(2))));
                }
            }

            if (SupportsSearch)
            {
                chain.AddTier();

                var searchQuery = searchCriteria.SearchTerm;

                if (!Settings.RemoveYear)
                {
                    searchQuery = string.Format("{0}", searchQuery);
                }

                chain.Add(GetPagedRequests(MaxPages,
                    categories,
                    "movie",
                    string.Format("&q={0}", NewsnabifyTitle(searchQuery))));
            }
        }

        private IEnumerable<IndexerRequest> GetPagedRequests(int maxPages, IEnumerable<int> categories, string searchType, string parameters)
        {
            var baseUrl = string.Format("{0}{1}?t={2}&extended=1", Settings.BaseUrl.TrimEnd('/'), Settings.ApiPath.TrimEnd('/'), searchType);

            if (categories != null && categories.Any())
            {
                var categoriesQuery = string.Join(",", categories.Distinct());
                baseUrl += string.Format("&cats={0}", categoriesQuery);
            }

            if (Settings.ApiKey.IsNotNullOrWhiteSpace())
            {
                baseUrl += "&apikey=" + Settings.ApiKey;
            }

            if (PageSize == 0)
            {
                yield return new IndexerRequest(string.Format("{0}{1}", baseUrl, parameters), HttpAccept.Rss);
            }
            else
            {
                for (var page = 0; page < maxPages; page++)
                {
                    yield return new IndexerRequest(string.Format("{0}&offset={1}&limit={2}{3}", baseUrl, page * PageSize, PageSize, parameters), HttpAccept.Rss);
                }
            }
        }

        private static string NewsnabifyTitle(string title)
        {
            return title.Replace("+", "%20");
        }

        public Func<IDictionary<string, string>> GetCookies { get; set; }
        public Action<IDictionary<string, string>, DateTime?> CookiesUpdater { get; set; }
    }
}