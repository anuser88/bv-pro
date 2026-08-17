using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ezx6t.BvPro.Runner.FetcherN;

public class Fetcher
{
	private static string[] ProxiesSources = [
		"https://raw.githubusercontent.com/TheSpeedX/PROXY-List/master/http.txt",
		"https://raw.githubusercontent.com/TheSpeedX/PROXY-List/master/socks4.txt",
		"https://raw.githubusercontent.com/TheSpeedX/PROXY-List/master/socks5.txt",
		"https://raw.githubusercontent.com/jetkai/proxy-list/main/online-proxies/txt/proxies.txt",
		"https://raw.githubusercontent.com/monosans/proxy-list/main/proxies/all.txt", //Linus Torvalds
		"https://raw.githubusercontent.com/roosterkid/openproxylist/main/HTTPS_RAW.txt",
		"https://raw.githubusercontent.com/almroot/proxylist/master/list.txt",
		"https://raw.githubusercontent.com/ShiftyTR/Proxy-List/master/proxy.txt",
		"https://raw.githubusercontent.com/hookzof/socks5_list/master/proxy.txt",
		"https://raw.githubusercontent.com/clarketm/proxy-list/master/proxy-list-raw.txt",
		"https://raw.githubusercontent.com/proxifly/free-proxy-list/main/proxies/all/data.txt",
		"https://raw.githubusercontent.com/ALIILAPRO/Proxy/main/http.txt",
		"https://raw.githubusercontent.com/ALIILAPRO/Proxy/main/socks4.txt",
		"https://raw.githubusercontent.com/ALIILAPRO/Proxy/main/socks5.txt",
		"https://raw.githubusercontent.com/Zaeem20/FREE_PROXIES_LIST/master/http.txt",
		"https://raw.githubusercontent.com/Zaeem20/FREE_PROXIES_LIST/master/https.txt",
		"https://raw.githubusercontent.com/Zaeem20/FREE_PROXIES_LIST/master/socks4.txt",
		"https://raw.githubusercontent.com/Zaeem20/FREE_PROXIES_LIST/master/socks5.txt",
		"https://raw.githubusercontent.com/vakhov/fresh-proxy-list/master/proxylist.txt",
		"https://raw.githubusercontent.com/r00tee/Proxy-List/main/Https.txt",
		"https://raw.githubusercontent.com/r00tee/Proxy-List/main/Socks4.txt",
		"https://raw.githubusercontent.com/r00tee/Proxy-List/main/Socks5.txt",
		"https://github.com/databay-labs/free-proxy-list/raw/master/http.txt",
		"https://github.com/databay-labs/free-proxy-list/raw/master/socks4.txt",
		"https://github.com/databay-labs/free-proxy-list/raw/master/socks5.txt",
		"https://github.com/elliottophellia/proxylist/raw/master/results/mix_checked.txt",
		"https://github.com/rdavydov/proxy-list/raw/main/proxies/http.txt",
		"https://github.com/rdavydov/proxy-list/raw/main/proxies/socks4.txt",
		"https://github.com/rdavydov/proxy-list/raw/main/proxies/socks5.txt",
		"https://github.com/prxchk/proxy-list/raw/main/all.txt",
		"https://github.com/iplocate/free-proxy-list/raw/refs/heads/main/all-proxies.txt",
		"https://api.proxyscrape.com/v2/?request=displayproxies&protocol=all&timeout=10000&country=all&simplified=true",
	];
	private static HttpClient Client = new();
	private bool sft = false;
	public Fetcher()
	{
		try
		{
			Client.Timeout = TimeSpan.FromSeconds(15);
		}
		catch {}
	}
	public async Task<List<string>> GetProxies(bool Sft)
	{
		sft = Sft;
		int sourcesCount = ProxiesSources.Length;
		Task<string[]>[] workers = new Task<string[]>[sourcesCount+2];
		workers[0] = PM();
		workers[1] = GN();
		for (int i = 0; i < sourcesCount; i++)
		{
			workers[i+2] = GetProxiesFromSource(ProxiesSources[i]);
		}
		HashSet<string> result = new();
		foreach (string[] proxies in await Task.WhenAll(workers))
		{
			foreach (string proxy in proxies)
				result.Add(proxy);
		}
		Success("all", result.Count);
		return result.ToList();
	}
	private async Task<string[]> GetProxiesFromSource(string ProxiesSource)
	{
		try
		{
			string content = await Client.GetStringAsync(ProxiesSource);
			string[] proxies = content
				.Replace("http://", "")
				.Replace("socks4://", "")
				.Replace("socks5://", "")
				.Replace("https://", "")
				.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
			Success(ProxiesSource, proxies.Length);;
			return proxies;
		}
		catch
		{
			Fail(ProxiesSource);
			return new string[0];
		}
	}
	private async Task<string[]> PM()
	{
		try
		{
			string content = await Client.GetStringAsync("https://freeproxies-api.website.proxymaven.com/proxies?per_page=100000");
			JsonDocument jsonDoc = JsonDocument.Parse(content);
			JsonElement data = jsonDoc.RootElement.GetProperty("proxies");
			string[] proxies = data
				.EnumerateArray()
				.Select(e => e.GetProperty("proxy").GetString()!)
				.ToArray();
			Success("PM", proxies.Length);
			return proxies;
		}
		catch
		{
			Fail("PM");
			return new string[0];
		}
	}
	private async Task<string[]> GN()
	{
		try
		{
			string content = await Client.GetStringAsync("https://proxylist.geonode.com/api/proxy-list?limit=500");
			JsonDocument jsonDoc = JsonDocument.Parse(content);
			JsonElement data = jsonDoc.RootElement.GetProperty("data");
			string[] proxies = data
				.EnumerateArray()
				.Select(e => e.GetProperty("ip").GetString()! + ":" + e.GetProperty("port").GetString()!)
				.ToArray();
			Success("GN", proxies.Length);
			return proxies;
		}
		catch
		{
			Fail("GN");
			return new string[0];
		}
	}
	private void Success(string src, int amt)
	{
		Console.WriteLine($"\x1b[38;5;10mFetched {amt} proxies from {src}\x1b[0m");
	}
	private void Fail(string src)
	{
		if (sft)
			Console.WriteLine($"\x1b[38;5;9mFailed to fetch {src}\x1b[0m");
	}
}