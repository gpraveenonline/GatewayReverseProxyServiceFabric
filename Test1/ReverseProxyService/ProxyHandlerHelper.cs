using System;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.ServiceFabric.Services.Client;

namespace ReverseProxyService
{
	internal class ProxyHandlerHelper
	{
		public enum PartitionKind
		{
			Int64Range,
			Named
		}

		public enum QueryKey
		{
			PartitionKind,
			PartitionKey,
			Timeout
		}

		public static bool TryGetQueryValue(NameValueCollection queryCollection, QueryKey queryKey, out string value)
		{
			var key = queryCollection.AllKeys.FirstOrDefault(x => x.Equals(queryKey.ToString(), StringComparison.OrdinalIgnoreCase));

			if (key != null)
			{
				value = queryCollection[key];
				return true;
			}

			value = null;
			return false;
		}

		public static bool TryGetQueryValue(NameValueCollection queryCollection, QueryKey queryKey, out int value)
		{
			var key = queryCollection.AllKeys.FirstOrDefault(x => x.Equals(queryKey.ToString(), StringComparison.OrdinalIgnoreCase));

			if (key != null)
				return int.TryParse(queryCollection[key], out value);

			value = default(int);
			return false;
		}

		public static void BuildFabricPath(Uri uri, out string serviceUri, out string localPath)
		{
			var uriSegments = uri.Segments;

			serviceUri = "fabric:/" + String.Join("", uriSegments.Skip(1).Take(2)).TrimEnd('/');
			localPath = String.Join("", uriSegments.Skip(3));
		}

		public static string BuildQuery(NameValueCollection queryCollection)
		{
			string query = String.Empty;
			if (queryCollection.HasKeys())
			{
				var keyNames = Enum.GetNames(typeof(QueryKey));
				var keys =
					queryCollection.AllKeys.Where(x => !keyNames.Any(y => x.Equals(y, StringComparison.OrdinalIgnoreCase))).ToArray();

				if (keys.Any())
					query = keys.Aggregate("?", (s, t) => s + (s.Equals("") ? string.Empty : "&") + t + "=" + queryCollection[t]);
			}

			return query;
		}

		public static ServicePartitionKey GetServicePartitionKey(string partitionKind, string partitionKey)
		{
			PartitionKind partitionKindEnum;

			if (!Enum.TryParse(partitionKind, out partitionKindEnum))
			{
				return null;
			}

			switch (partitionKindEnum)
			{
				case PartitionKind.Int64Range:
					long intPartitionKey;
					return !long.TryParse(partitionKey, out intPartitionKey) ? null : new ServicePartitionKey(intPartitionKey);
				case PartitionKind.Named:
					return new ServicePartitionKey(partitionKey);
				default:
					return null;
			}
		}
	}
}