using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ReverseProxyService
{
	public static class HttpRequestMessageExtensions
	{
		// http://stackoverflow.com/a/34049029/71515
		public static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage req)
		{
			var clone = new HttpRequestMessage(req.Method, req.RequestUri);

			var ms = new MemoryStream();
			if (req.Content != null)
			{
				await req.Content.CopyToAsync(ms).ConfigureAwait(false);
				ms.Position = 0;

				if ((ms.Length > 0 || req.Content.Headers.Any()) && clone.Method != HttpMethod.Get)
				{
					clone.Content = new StreamContent(ms);

					if (req.Content.Headers != null)
						foreach (var h in req.Content.Headers)
							clone.Content.Headers.Add(h.Key, h.Value);
				}
			}

			clone.Version = req.Version;

			foreach (var prop in req.Properties)
				clone.Properties.Add(prop);

			foreach (var header in req.Headers)
				clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

			return clone;
		}
	}
}