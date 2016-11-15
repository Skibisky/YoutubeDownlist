using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace YoutubeDownlist
{
	public class YouTubeAPI
	{
		string baseUri = "https://www.googleapis.com/youtube/v3/";
		string key = "";


		public YouTubeAPI(string key)
		{
			this.key = key;
		}

		public string RawRequest(string endpoint, QueryCollection qc = null)
		{
			if (qc == null)
				qc = new QueryCollection();

			if (!qc.ContainsKey("part"))
				qc.Add("part", "snippet");

			if (!qc.ContainsKey("key"))
				qc.Add("key", key);

			string loc = baseUri + endpoint + qc.ToString();

			var req = (HttpWebRequest)HttpWebRequest.Create(loc);
			//req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/50.0.2661.102 Safari/537.36";
			//req.Accept = "application/json";// "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
			string resp = null;
			using (StreamReader sr = new StreamReader(req.GetResponse().GetResponseStream()))
			{
				resp = sr.ReadToEnd();
			}
			return resp;
		}
		
		public YTVideo Video(Uri location)
		{
			// extract id from location
			string id = null;
			int wloc = location.Query.IndexOf("v=") + "v=".Length;
			int aloc = location.Query.IndexOf("&", wloc);
			if (aloc == -1)
				id = location.Query.Substring(wloc);
			else
				id = location.Query.Substring(wloc, aloc - wloc);

			return Video(id);
		}

		public YTVideo Video(string id)
		{
			var resp = RawRequest("videos", new QueryCollection() { { "id", id } });
			var srch = JsonConvert.DeserializeObject<YTSearchResponse<YTVideo>>(resp);
			if (srch.items.Count() == 0)
				return null;
			return srch.items.First();
		}

        public YTPlaylist Playlist(Uri location)
        {
            // extract id from location
            string id = null;
            int wloc = location.Query.IndexOf("list=") + "list=".Length;
            int aloc = location.Query.IndexOf("&", wloc);
            if (aloc == -1)
                id = location.Query.Substring(wloc);
            else
                id = location.Query.Substring(wloc, aloc - wloc);

            return Playlist(id);
        }

        public YTPlaylist Playlist(string id)
        {
            var resp = RawRequest("playlists", new QueryCollection() { { "id", id } });
            var srch = JsonConvert.DeserializeObject<YTSearchResponse<YTPlaylist>>(resp);
            if (srch.items.Count() == 0)
                return null;
            return srch.items.First();
        }

        public YTSearchResponse<YTVideo> PlaylistItems(Uri location)
		{
			// extract id from location
			string id = null;
			int wloc = location.Query.IndexOf("list=") + "list=".Length;
			int aloc = location.Query.IndexOf("&", wloc);
			if (aloc == -1)
				id = location.Query.Substring(wloc);
			else
				id = location.Query.Substring(wloc, aloc - wloc);

			return PlaylistItems(id);
		}

		public YTSearchResponse<YTVideo> PlaylistItems(string id)
		{
			var resp = RawRequest("playlistItems", new QueryCollection() { { "playlistId", id }, { "maxResults", "50" } });
			var srch = JsonConvert.DeserializeObject<YTSearchResponse<YTVideo>>(resp);
			return srch;
		}

		public YTSearchResponse<YTSearchResult> Search(string searchstr, int count = 5)
		{
			var resp = RawRequest("search", new QueryCollection() { { "q", Uri.EscapeUriString(searchstr) }, { "maxResults", count.ToString() } });
			return JsonConvert.DeserializeObject<YTSearchResponse<YTSearchResult>>(resp);
		}
	}

	public class QueryCollection : Dictionary<string, string>
	{
		public override string ToString()
		{
			string ret = "?";
			foreach (var p in this)
			{
				ret += p.Key + "=" + p.Value + "&";
			}
			// remove trailing &
			ret = ret.Substring(0, ret.Length - 1);
			return ret;
		}
	}

	public class YTEntity
	{
		public string kind;
		public string etag;
	}

	public class YTSearchResponse<T> : YTEntity
	{
		public string nextPageToken;
		public string regionCode;
		public class PageInfo
		{
			public int totalResults;
			public int resultsPerPage;
		}
		public PageInfo pageInfo;
		public IEnumerable<T> items;
	}
	
	public class YTVideo : YTEntity
	{
		public string id;
		public YTSnippet snippet;
	}

	public class YTPlaylist : YTEntity
	{
		public string id;
		public YTSnippet snippet;
	}

	public struct YTID
	{
		public string kind;
		public string videoId;
	}

	public class YTSearchResult : YTEntity
	{
		public YTID id;
		public YTSnippet snippet;
	}

	public class YTSnippet
	{
		public DateTime publishedAt;
		public string channelId;
		public string title;
		public string description;
		public YTThumbs thumbnails;
		public string channelTitle;
		public YTID resourceId;
	}

	public class YTThumbs
	{
		[JsonProperty("default")]
		public YTThumbnail Default;
		public YTThumbnail medium;
		public YTThumbnail high;
	}

	public class YTThumbnail
	{
		public Uri url;
		public int width;
		public int height;
	}
}
