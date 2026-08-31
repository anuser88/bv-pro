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
	private List<string>? Targets;
	private int Wait = 0;
	private static readonly int[] GoodCodes = {200, 429};
	private static readonly int[] NextCodes = {200, 404};
	private bool sft = false;
	private static readonly HttpClient Client = new();
	private readonly object _PCLock = new();
	private async Task TestProxies(List<string> ProxiesToUse, int par = 100, CancellationToken ct = default)
	{
		ProxiedClients = new List<HttpClient>();
		var queue = new ConcurrentQueue<string>(ProxiesToUse);
		var tasks = new Task[par];
		for (int i = 0; i < par; i++)
		{
			tasks[i] = TestProxyWorker(queue, i % Targets!.Count, ct);
		}
		await Task.WhenAll(tasks);
	}
	private async Task TestProxyWorker(ConcurrentQueue<string> queue, int i = 0, CancellationToken ct = default)
	{
		while (queue.TryDequeue(out string? proxy))
		{
			HttpClientHandler? handler = null;
			HttpClient? ProxiedClient = null;
			try
			{
				handler = new HttpClientHandler
				{
					Proxy = new WebProxy(proxy),
					UseProxy = true
				};
				ProxiedClient = new HttpClient(handler);
				ProxiedClient!.Timeout = TimeSpan.FromSeconds(60);
				var res = await ProxiedClient!.PostAsync(Targets?[i], Payload, ct)!;
				int statusCode = (int)res.StatusCode;
				if (!GoodCodes.Contains(statusCode)) throw new Exception("SiMaNiMi");
				lock (_PCLock)
				{
					ProxiedClients!.Add(ProxiedClient);
				}
				Success("unknown", statusCode);
			}
			catch
			{
				Fail("unknown");
				try
				{
					handler?.Dispose();
					ProxiedClient?.Dispose();
				}
				catch {}
			}
			ct.ThrowIfCancellationRequested();
		}
	}
	private async Task RunProxies(CancellationToken ct = default)
	{
		int clientsCount = (int)ProxiedClients?.Count!;
		if (clientsCount == 0) throw new Exception("No live proxy found!");
		var tasks = new Task[clientsCount + 1];
		for (int i = 0; i < clientsCount; i++) {
			tasks[i + 1] = RunProxyWorker(i, ct);
		}
		tasks[0] = SendLocally(ct);
		await Task.WhenAll(tasks);
	}
	private async Task RunProxyWorker(int id, CancellationToken ct = default)
	{
		int i = id % Targets!.Count;
		while (true)
		{
			HttpClient client = ProxiedClients?[id]!;
			ct.ThrowIfCancellationRequested();
			try
			{
				var res = await client?.PostAsync(Targets?[i], Payload, ct)!;
				int statusCode = (int)res.StatusCode;
				Success($"{id}", statusCode);
				if (NextCodes.Contains(statusCode)) i++;
			}
			catch
			{
				Fail(id.ToString());
			}
			i%=Targets!.Count;
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
				if (NextCodes.Contains(statusCode)) i++;
			}
			catch
			{
				Fail("none");
			}
			i%=Targets!.Count;
			await Task.Delay(Wait, ct);
		}
	}
	public async Task Run(List<string> proxies, List<string> targets, int wait, int par, bool showFailedTasks = true, string payload = "{}", CancellationToken ct = default)
	{
		sft = showFailedTasks;
		Payload = new StringContent(payload, Encoding.UTF8, "application/json");
		Targets = targets;
		Wait = wait;
		try
		{
			await TestProxies(proxies, par, ct);
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
