using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ezx6t.BvPro.Runner.RunnerN;

public class RunnerF : IDisposable
{
	private List<HttpClient>? ProxiedClients;
	private StringContent? Payload;
	private string[]? Targets;
	private int Wait = 0;
	private int[] GoodCodes = {200, 429};
	private bool sft = false;
	private static readonly HttpClient Client = new();
	public async Task TestProxies(List<string> ProxiesToUse, int ts = 9, CancellationToken ct = default)
	{
		ProxiedClients = new List<HttpClient>();
		HttpClientHandler handler;
		foreach (string proxy in ProxiesToUse!)
		{
			ct.ThrowIfCancellationRequested(); 
			try
			{
				handler = new HttpClientHandler
				{
					Proxy = new WebProxy(proxy),
					UseProxy = true
				};
				HttpClient ProxiedClient = new HttpClient(handler);
				ProxiedClient.Timeout = TimeSpan.FromSeconds(60);
				ProxiedClients.Add(ProxiedClient);
			}
			catch {}
		}
		int clientsCount = ProxiedClients.Count;
		if (clientsCount == 0) throw new Exception("No proxy found!");
		HashSet<int> liveSet = new();
		Task<int>[] workers = new Task<int>[clientsCount];
		int k = 0;
		while (k < clientsCount)
		{
			ct.ThrowIfCancellationRequested(); 
			int j = 0;
			while (k < clientsCount && j < ts)
			{
				workers[k] = TestProxyWorker(k, ct);
				k++;
				j++;
			}
			await Task.Delay(20);
		}
		foreach (int id in await Task.WhenAll(workers))
		{
			ct.ThrowIfCancellationRequested(); 
			if (id >= 0)
				liveSet.Add(id);
		}
		for (int i = clientsCount-1; i >= 0; i--)
		{
			ct.ThrowIfCancellationRequested(); 
			if (!liveSet.Contains(i))
			{
				ProxiedClients?[i].Dispose();
				ProxiedClients?.RemoveAt(i);
			}
		}
		Console.WriteLine($"\x1b[38;5;11mThere are {ProxiedClients?.Count} live proxies in total\x1b[0m");
	}
	private async Task<int> TestProxyWorker(int id, CancellationToken ct = default) {
		HttpClient client = ProxiedClients?[id]!;
		try
		{
			var res = await client?.PostAsync(Targets?[0], Payload, ct)!;
			int statusCode = (int)res.StatusCode;
			if (!GoodCodes.Contains(statusCode)) throw new Exception("Blocked");
			Success(id.ToString(), statusCode);
			return id;
		}
		catch
		{
			Fail(id.ToString());
			return -1;
		}
	}
	public async Task RunProxies(CancellationToken ct = default) {
		int clientsCount = (int)ProxiedClients?.Count!;
		if (clientsCount == 0) throw new Exception("No live proxy found!");
		for (int i = 0; i < clientsCount; i++) {
			_ = RunProxyWorker(i, ct);
		}
		await SendLocally(ct);
	}
	private async Task RunProxyWorker(int id, CancellationToken ct = default)
	{
		int i = 0;
		while (true)
		{
			HttpClient client = ProxiedClients?[id]!;
			ct.ThrowIfCancellationRequested();
			try
			{
				var res = await client?.PostAsync(Targets?[i], Payload, ct)!;
				int statusCode = (int)res.StatusCode;
				Success(id.ToString(), statusCode);
			}
			catch
			{
				Fail(id.ToString());
			}
			i++;
			i%=Targets!.Length;
			await Task.Delay(Wait, ct);
		}
	}
	private async Task SendLocally(CancellationToken ct = default) {
		int i = 0;
		while (true)
		{
			try {
				var res = await Client.PostAsync(Targets?[i], Payload, ct)!;
				int statusCode = (int)res.StatusCode;
				Success("none", statusCode);
			}
			catch
			{
				Fail("none");
			}
			i++;
			i%=Targets!.Length;
			await Task.Delay(Wait, ct);
		}
	}
	public async Task Run(List<string> proxies, string[] targets, int wait, int ts, bool showFailedTasks = true, string payload = "{}", CancellationToken ct = default)
	{
		sft = showFailedTasks;
		Payload = new StringContent(payload, Encoding.UTF8, "application/json");
		Targets = targets;
		Wait = wait;
		try
		{
			await TestProxies(proxies, ts, ct);
			await RunProxies(ct);
		}
		catch {}
	}
	private void Success(string id, int code)
	{
		Console.WriteLine($"\x1b[38;5;10mWorking proxy: {id} code: {code}\x1b[0m");
	}
	private void Fail(string id)
	{
		if (sft)
			Console.WriteLine($"\x1b[38;5;9mNot working proxy: {id}\x1b[0m");
	}
	public void Dispose()
	{
		for (int i = 0; i < ProxiedClients?.Count; i++) {
			ProxiedClients?[i].Dispose();
		}
		ProxiedClients?.Clear();
		Payload?.Dispose();
	}
}