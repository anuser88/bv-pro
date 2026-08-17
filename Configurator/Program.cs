using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ezx6t.BvPro.Configurator.Main;

class Program
{
	static async Task Main()
	{
		try
		{
			Process.Start("Runner.exe");
		}
		catch {}
		bool changes = false;
		bool exit = false;
		ClearTerminal();
		await ReadConfigs();
		Console.WriteLine("\x1b[38;5;11mWelcome to bv-pro configurator!\x1b[0m");
		while (!exit)
		{
			Instructions(changes);
			Console.Write("\x1b[38;5;13m>>> \x1b[38;5;12m");
			string? typed = Console.ReadLine()!;
			Console.Write("\x1b[0m");
			ClearTerminal();
			switch (typed?.ToLowerInvariant())
			{
				case "toggle":
					changes = true;
					enabled = !enabled;
					Console.WriteLine("\x1b[38;5;10mOperation completed successfully!\x1b[0m");
					break;
				case "targets":
					changes = changes | await TargetsConfigs();
					break;
				case "more":
					changes = changes | await MoreConfigs();
					break;
				case "reset":
					changes = true;
					Reset();
					break;
				case "save":
					changes = false;
					await WriteConfigs();
					break;
				case "discard":
					changes = false;
					await ReadConfigs();
					Console.WriteLine("\x1b[38;5;10mOperation completed successfully!\x1b[0m");
					break;
				case "exit":
					if (!changes)
					{
						exit = true;
					}
					else
					{
						Console.WriteLine("\x1b[38;5;9mThere are unsaved changes, please either type \"save\" to save or type \"discard\" to discard changes.\x1b[0m");
					}
					break;
				case "":
					Console.WriteLine("\x1b[38;5;9mPlease type a command!\x1b[0m");
					break;
				default:
					Console.WriteLine("\x1b[38;5;9mInvalid command!\x1b[0m");
					break;
			}
		}
	}
	static List<string> targets = new();
	static bool enabled = false;
	static bool sft = false;
	static int refr = 600000;
	static int postIntv = 15000;
	static string payload = "{}";
	static HttpClient client = new();
	static void ClearTerminal()
	{
		try
		{
			Console.Clear();
		}
		catch {}
	}
	static void Instructions(bool changes)
	{
		if (changes)
			Console.WriteLine("\n\x1b[38;5;11mNotes: Remember to save your changes to apply configs.\x1b[0m");
		Console.WriteLine();
		Console.WriteLine($"\x1b[38;5;14mViews booster enabled?: {enabled}\x1b[0m");
		Console.WriteLine("\x1b[38;5;14mType \"toggle\" to toggle views booster.\x1b[0m");
		Console.WriteLine("\x1b[38;5;14mType \"targets\" to configure views booster targets.\x1b[0m");
		Console.WriteLine("\x1b[38;5;14mType \"more\" to configure more advanced settings.\x1b[0m");
		Console.WriteLine("\x1b[38;5;14mType \"reset\" to reset configurations.\x1b[0m");
		Console.WriteLine("\x1b[38;5;14mType \"save\" to apply configurations.\x1b[0m");
		Console.WriteLine("\x1b[38;5;14mType \"discard\" to discard changes.\x1b[0m");
		Console.WriteLine("\x1b[38;5;14mType \"exit\" to exit configurator.\x1b[0m");
	}
	static void Reset()
	{
		targets = new List<string>();
		enabled = false;
		sft = false;
		refr = 600000;
		postIntv = 15000;
		payload = "{}";
	}
	static async Task ReloadConfigsRequest()
	{
		try
		{
			using var client = new NamedPipeClientStream(
				".",
				"ezx6t_bvpro_runner_hear",
				PipeDirection.Out
			);
			client.Connect(1000);
			using var writer = new StreamWriter(client, Encoding.UTF8)
			{
				AutoFlush = true
			};
			writer.WriteLine("reload");
		}
		catch {}
	}
	static async Task ReadConfigs()
	{
		try
		{
			int i = 0;
			List<string> targets = new();
			await foreach (string line in File.ReadLinesAsync("config.txt"))
			{
				switch (i)
				{
					case 0:
						bool.TryParse(line, out enabled);
						break;
					case 1:
						bool.TryParse(line, out sft);
						break;
					case 2:
						int.TryParse(line, out refr);
						break;
					case 3:
						int.TryParse(line, out postIntv);
						break;
					case 4:
						payload = line;
						break;
					default:
						if (!string.IsNullOrWhiteSpace(line)) targets.Add(line);
						break;
				}
				i++;
			}
		}
		catch
		{
			await WriteConfigs();
		}
	}
	static async Task WriteConfigs()
	{
		try
		{
			StreamWriter writer = new StreamWriter("config.tmp");
			writer.WriteLine(enabled);
			writer.WriteLine(sft);
			writer.WriteLine(refr);
			writer.WriteLine(postIntv);
			writer.WriteLine(payload);
			foreach (string target in targets)
			{
				writer.WriteLine(target);
			}
			writer.Dispose();
			File.Move("config.tmp", "config.txt", true);
			_ = ReloadConfigsRequest();
			Console.WriteLine("\x1b[38;5;10mOperation completed successfully!\x1b[0m");
		}
		catch (Exception e) {Console.WriteLine(e);}
	}
	static async Task<bool> TargetsConfigs()
	{
		bool changes = false;
		bool quit = false;
		while (!quit)
		{
			TInstructions();
			Console.Write("\x1b[38;5;13m>>> \x1b[38;5;12m");
			string? typed = Console.ReadLine()!;
			Console.Write("\x1b[0m");
			ClearTerminal();
			switch (typed?.ToLowerInvariant())
			{
				case "addbyprid":
					try
					{
						Console.Write("\x1b[38;5;13mEnter project ID: \x1b[38;5;12m");
						int id = int.Parse(Console.ReadLine()!);
						if (id == -1)
						{
							// targets.Add("https://api.scratch.mit.edu/users/{projectAuthorUsername}/projects/{id}/views");
						}
						else
						{
							JsonDocument projectData = JsonDocument.Parse(await client.GetStringAsync($"https://api.scratch.mit.edu/projects/{id}"));
							JsonElement author = projectData.RootElement.GetProperty("author");
							string projectAuthorUsername = author.GetProperty("username").GetString()!;
							targets.Add($"https://api.scratch.mit.edu/users/{projectAuthorUsername}/projects/{id}/views");
						}
						changes = true;
						Console.WriteLine("\x1b[38;5;10mOperation completed successfully!\x1b[0m");
					}
					catch
					{
						Console.WriteLine("\x1b[38;5;9mInvalid ID or unshared project!\x1b[0m");
					}
					break;
				case "addadvanced":
					Console.Write("Example for Scratch MIT: https://api.scratch.mit.edu/users/{projectAuthorUsername}/projects/{id}/views\n\x1b[38;5;13mEnter views API: \x1b[38;5;12m");
					targets.Add(Console.ReadLine()!);
					changes = true;
					Console.WriteLine("\x1b[38;5;10mOperation completed successfully!\x1b[0m");
					break;
				case "remove":
					try
					{
						Console.Write("\x1b[38;5;13mEnter index: \x1b[38;5;12m");
						targets.RemoveAt(int.Parse(Console.ReadLine()!));
						changes = true;
						Console.WriteLine("\x1b[38;5;10mOperation completed successfully!\x1b[0m");
					}
					catch
					{
						Console.WriteLine("\x1b[38;5;9mInvalid index!\x1b[0m");
					}
					break;
				case "clear":
					targets.Clear();
					changes = true;
					Console.WriteLine("\x1b[38;5;10mOperation completed successfully!\x1b[0m");
					break;
				case "quit":
					quit = true;
					Console.WriteLine("\x1b[38;5;10mOperation completed successfully!\x1b[0m");
					break;
				case "":
					Console.WriteLine("\x1b[38;5;9mPlease type a command!\x1b[0m");
					break;
				default:
					Console.WriteLine("\x1b[38;5;9mInvalid command!\x1b[0m");
					break;
			}
		}
		return changes;
	}
	static void TInstructions()
	{
		Console.WriteLine();
		Console.WriteLine("\x1b[38;5;14mTargets List:\x1b[0m");
		int i = 0;
		foreach (string target in targets)
			Console.WriteLine($"\x1b[38;5;14mIndex: {i++} Target: {target}\x1b[0m");
		Console.WriteLine("\x1b[38;5;14m--- End of targets list ---\x1b[0m");
		Console.WriteLine("\x1b[38;5;14mType \"addByPrId\" to add a target by project ID (Recommended).\x1b[0m");
		Console.WriteLine("\x1b[38;5;14mType \"addAdvanced\" to add a target by views API (Advanced).\x1b[0m");
		Console.WriteLine("\x1b[38;5;14mType \"remove\" to remove a target.\x1b[0m");
		Console.WriteLine("\x1b[38;5;14mType \"clear\" to remove all targets.\x1b[0m");
		Console.WriteLine("\x1b[38;5;14mType \"quit\" to return to main menu.\x1b[0m");
	}
	static async Task<bool> MoreConfigs()
	{
		bool changes = false;
		bool quit = false;
		while (!quit)
		{
			MInstructions();
			Console.Write("\x1b[38;5;13m>>> \x1b[38;5;12m");
			string? typed = Console.ReadLine()!;
			Console.Write("\x1b[0m");
			ClearTerminal();
			switch (typed?.ToLowerInvariant())
			{
				case "sft":
					sft = !sft;
					Console.WriteLine("\x1b[38;5;10mOperation completed successfully!\x1b[0m");
					break;
				case "refr":
					try
					{
						Console.Write("\x1b[38;5;13mEnter milliseconds: \x1b[38;5;12m");
						refr = int.Parse(Console.ReadLine()!);
						Console.WriteLine("\x1b[38;5;10mOperation completed successfully!\x1b[0m");
					}
					catch
					{
						Console.WriteLine("\x1b[38;5;9mInvalid number!\x1b[0m");
					}
					break;
				case "postintv":
					try
					{
						Console.Write("\x1b[38;5;13mEnter milliseconds: \x1b[38;5;12m");
						postIntv = int.Parse(Console.ReadLine()!);
						Console.WriteLine("\x1b[38;5;10mOperation completed successfully!\x1b[0m");
					}
					catch
					{
						Console.WriteLine("\x1b[38;5;9mInvalid number!\x1b[0m");
					}
					break;
				case "payload":
					Console.Write("\x1b[38;5;13mEnter string payload: \x1b[38;5;12m");
					payload = Console.ReadLine()!;
					Console.WriteLine("\x1b[38;5;10mOperation completed successfully!\x1b[0m");
					break;
				case "quit":
					quit = true;
					Console.WriteLine("\x1b[38;5;10mOperation completed successfully!\x1b[0m");
					break;
				case "":
					Console.WriteLine("\x1b[38;5;9mPlease type a command!\x1b[0m");
					break;
				default:
					Console.WriteLine("\x1b[38;5;9mInvalid command!\x1b[0m");
					break;
			}
		}
		return changes;
	}
	static void MInstructions()
	{
		Console.WriteLine();
		Console.WriteLine($"\x1b[38;5;14m(Debug): Show failed tasks: {sft}. Type \"sft\" to toggle.\x1b[0m");
		Console.WriteLine($"\x1b[38;5;14mAutomatically restart views booster each: {refr} milliseconds. Type \"refr\" to change.\x1b[0m");
		Console.WriteLine($"\x1b[38;5;14mEach proxy post 1 request each: {postIntv} milliseconds. Type \"postIntv\" to change.\x1b[0m");
		Console.WriteLine($"\x1b[38;5;14mEach request has payload: \"{payload}\". Type \"payload\" to change.\x1b[0m");
		Console.WriteLine($"\x1b[38;5;14mType \"quit\" to quit.\x1b[0m");
	}
}