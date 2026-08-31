using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ezx6t.BvPro.Runner.FetcherN;
using Ezx6t.BvPro.Runner.RunnerN;

namespace Ezx6t.BvPro.Runner.Main;

class Program
{
	static async Task Main()
	{
		Directory.SetCurrentDirectory(AppContext.BaseDirectory);
		using var mutex = new Mutex(
			initiallyOwned: true,
			name: "Ezx6t.BvPro.Runner.SingleInstance",
			createdNew: out bool createdNew
		);
		if (!createdNew)
			return;
		_ = Hear();
		while (true)
			await Run();
	}
	static readonly object _ctsLock = new();
	static CancellationTokenSource cts = new();
	static List<string> Targets = new();
	static string payload = "{}";
	static int refresh = 1800000;
	static int postInterval = 15000;
	static bool enabled = false;
	static bool showFailedTasks = false;
	static readonly int[] GoodCodes = {200, 429};
	static HttpClient client = new();
	static async Task Run()
	{
		var ct = cts.Token;
		try
		{
			await Task.Delay(15000, ct);
		}
		catch {}
		Fetcher fetcher = new();
		while (true)
		{
			await ReadConfigs();
			lock (_ctsLock)
			{
				cts.Dispose();
				cts = new CancellationTokenSource();
			}
			ct = cts.Token;
			_ = Refresher(refresh, ct);
			await TestTargets(ct);
			bool DoStuff = Targets.Count > 0;
			if (DoStuff && enabled)
			{
				RunnerF runner = new();
				try
				{
					List<string> proxies = await fetcher.GetProxies(showFailedTasks);
					await runner.Run(proxies, Targets, postInterval, 1000, showFailedTasks, payload, ct);
				}
				catch {}
				finally
				{
					runner.Dispose();
				}
			}
			else
			{
				try
				{
					await Task.Delay(-1, ct);
				}
				catch {}
			}
		}
	}
	static async Task Hear()
	{
		while (true)
		{
			using var server = new NamedPipeServerStream(
				"ezx6t_bvpro_runner_hear0",
				PipeDirection.In
			);
			await server.WaitForConnectionAsync();
			using var reader = new StreamReader(server, Encoding.UTF8);
			string? cmd = await reader.ReadLineAsync();
			if (cmd == "reload")
			{
				lock (_ctsLock)
				{
					cts.Cancel();
				}
				Console.WriteLine("\x1b[33mInterrupted\x1b[0m");
			}
		}
	}
	static async Task Refresher(int dur, CancellationToken ct = default)
	{
		try
		{
			await Task.Delay(dur, ct);
			lock (_ctsLock)
			{
				cts.Cancel();
			}
		}
		catch {}
	}
	static async Task ReadConfigs()
	{
		try
		{
			int i = 0;
			Targets = new();
			foreach (string line in File.ReadLines("config.txt"))
			{
				Console.WriteLine(line);
				switch (i++)
				{
					case 0:
						bool.TryParse(line, out enabled);
						break;
					case 1:
						bool.TryParse(line, out showFailedTasks);
						break;
					case 2:
						int.TryParse(line, out refresh);
						break;
					case 3:
						int.TryParse(line, out postInterval);
						break;
					case 4:
						payload = line;
						break;
					default:
						if (!string.IsNullOrWhiteSpace(line)) Targets.Add(line);
						break;
				}
			}
		}
		catch {}
	}
	static async Task TestTargets(CancellationToken ct = default)
	{
		try
			{
			int i = 0;
			while (i < Targets.Count)
			{
				try
				{
					var content = new StringContent(payload, Encoding.UTF8, "application/json");
					var res = await client.PostAsync(Targets[i], content, ct);
					if (!GoodCodes.Contains((int)res.StatusCode)) throw new Exception("ai hoi");
					i++;
				}
				catch
				{
					Targets.RemoveAt(i);
				}
				ct.ThrowIfCancellationRequested();
			}
		}
		catch {}
	}
}