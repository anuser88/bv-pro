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
		using var mutex = new Mutex(
			initiallyOwned: true,
			name: "Ezx6t.BvPro.Runner.SingleInstance",
			createdNew: out bool createdNew
		);
		if (!createdNew)
			return;
		_ = Hear();
		await Run();
	}
	static readonly object _ctsLock = new();
	static CancellationTokenSource cts = new();
	static string[] Targets = new string[0];
	static string payload = "{}";
	static int refresh = 600000;
	static int postInterval = 15000;
	static bool enabled = false;
	static bool showFailedTasks = false;
	static async Task Run()
	{
		await Task.Delay(10000);
		while (true)
		{
			await Task.Delay(5000);
			await ReadConfigs();
			lock (_ctsLock)
			{
				cts.Dispose();
				cts = new CancellationTokenSource();
			}
			var ct = cts.Token;
			_ = Refresher(refresh, ct);
			bool DoStuff = Targets.Length > 0;
			if (DoStuff && enabled)
			{
				Fetcher fetcher = new();
				List<string> proxies = await fetcher.GetProxies(showFailedTasks);
				RunnerF runner = new();
				try
				{
					await runner.Run(proxies, Targets, postInterval, 10, showFailedTasks, payload, ct);
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
				"ezx6t_bvpro_runner_hear",
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
			List<string> targets = new();
			foreach (string line in File.ReadLines("config.txt"))
			{
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
						if (!string.IsNullOrWhiteSpace(line)) targets.Add(line);
						break;
				}
			}
			Targets = targets.ToArray();
		}
		catch {}
	}
}