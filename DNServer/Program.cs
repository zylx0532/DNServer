﻿using ARSoft.Tools.Net.Dns;
using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace DNServer
{
	internal static class Program
	{
		private class Options
		{
			// Omitting long name, defaults to name of property, ie "--verbose"
			[Option(Default = true, HelpText = @"Prints all messages to standard output.")]
			public bool Verbose { get; set; }

			[Option('b', @"bindip", HelpText = @"Listen on...", Default = @"0.0.0.0:53")]
			public string BindIpEndPoint { get; set; }

			[Option('u', @"updns", HelpText = @"Upstream DNS Server", Default = @"101.226.4.6")]
			public string UpDNS { get; set; }

			[Option('p', @"puredns", HelpText = @"Pure DNS Server", Default = null)]
			public string PureDNS { get; set; }

			[Option(@"upport", HelpText = @"Upstream DNS Server Port", Default = DNSDefaultPort)]
			public ushort UpDnsPort { get; set; }

			[Option(@"pureport", HelpText = @"Pure DNS Server Port", Default = DNSDefaultPort)]
			public ushort PureDnsPort { get; set; }

			[Option(@"upecs", HelpText = @"Upstream DNS Server Client Subnet, a ip address", Default = null)]
			public string UpEcs { get; set; }

			[Option(@"pureecs", HelpText = @"Pure DNS Server Client Subnet, a ip address", Default = null)]
			public string PureEcs { get; set; }

			[Option(@"udp", HelpText = @"The count of threads listings on udp, 0 to deactivate udp", Default = DEFAULT_NUMBER_OF_CONCURRENCY)]
			public int UdpCount { get; set; }

			[Option(@"tcp", HelpText = @"The count of threads listings on tcp, 0 to deactivate tcp", Default = DEFAULT_NUMBER_OF_CONCURRENCY)]
			public int TcpCount { get; set; }

			[Option('l', @"list", HelpText = @"Domains list file path", Default = @"chndomains.txt")]
			public string Path { get; set; }
		}

		public static bool Verbose = true;
		public const int DNSDefaultPort = 53;
		public const int DEFAULT_NUMBER_OF_CONCURRENCY = 100;

		public static void Main(string[] args)
		{
			Parser.Default.ParseArguments<Options>(args)
			.WithParsed(opts => RunOptionsAndReturnExitCode(opts))
			.WithNotParsed(HandleParseError);
		}

		private static int RunOptionsAndReturnExitCode(Options options)
		{
			Verbose = options.Verbose;
			IEnumerable<IPAddress> updns;
			IEnumerable<IPAddress> puredns;
			IPAddress upEcs;
			IPAddress pureEcs;
			IPEndPoint bindIpEndPoint;

			List<IPAddress> ipAddresses;
			List<IPAddress> pureDns;
			try
			{
				updns = Common.ToIpAddresses(options.UpDNS, new[] { ',', '，' });
				ipAddresses = updns.ToList();
				Console.WriteLine($@"UpDNS: {Common.FromIpAddresses(ipAddresses)}");

				puredns = Common.ToIpAddresses(options.PureDNS, new[] { ',', '，' });
				if (puredns == null)
				{
					puredns = ipAddresses;
				}
				pureDns = puredns.ToList();
				Console.WriteLine($@"PureDNS: {Common.FromIpAddresses(pureDns)}");

				bindIpEndPoint = Common.ToIPEndPoint(options.BindIpEndPoint, 53);
				Console.WriteLine($@"Listen on: {bindIpEndPoint}");

				upEcs = Common.ToIpAddress(options.UpEcs);
				if (upEcs != null)
				{
					Console.WriteLine($@"Upstream DNS Server Client Subnet: {upEcs}");
				}

				pureEcs = Common.ToIpAddress(options.PureEcs);
				if (pureEcs != null)
				{
					Console.WriteLine($@"Pure DNS Server Client Subnet: {pureEcs}");
				}
			}
			catch
			{
				Console.WriteLine(@"DNS Server ERROR!");
				return 1;
			}
			var list = Common.ReadLines(options.Path);

			StartDNServer(ipAddresses, options.UpDnsPort,
				pureDns, options.PureDnsPort,
				options.UdpCount, options.TcpCount,
				upEcs, pureEcs,
				list, bindIpEndPoint, 10000);
			while (true)
			{

			}
			// return 0;
		}

		private static void HandleParseError(IEnumerable<Error> errs)
		{
			Console.WriteLine(@"ParseError");
			foreach (var error in errs)
			{
				Console.WriteLine(error);
			}
		}

		private static void StartDNServer(
			IEnumerable<IPAddress> upDns, ushort upDnsPort,
			IEnumerable<IPAddress> pureDns, ushort pureDnsPort,
			int udpListenerCount, int tcpListenerCount,
			IPAddress upEcs, IPAddress pureEcs,
			IEnumerable<string> list, IPEndPoint bindIpPoint, int timeout = 10000)
		{
			var server = new ConditionalForwardingDnsServer(bindIpPoint, udpListenerCount, tcpListenerCount)
			{
				PureDns = new DnsClient(pureDns, timeout, pureDnsPort),
				UpStreamDns = new DnsClient(upDns, timeout, upDnsPort),
				Timeout = timeout,
			};
			if (pureEcs != null)
			{
				server.PureEcs = new ClientSubnetOption(32, pureEcs);
			}
			if (upEcs != null)
			{
				server.UpStreamEcs = new ClientSubnetOption(32, upEcs);
			}

			server.LoadDomains(list);
			if (Verbose)
			{
				server.ClientConnected += (sender, e) =>
				{
					return Task.Run(() =>
					{
						Console.WriteLine($@"{e.RemoteEndpoint} Connected: {e.ProtocolType}");
					});
				};
			}
			server.ExceptionThrown += (sender, e) =>
			{
				return Task.Run(() =>
				{
					Console.WriteLine($@"Errored: {e.Exception}");
				});
			};
			server.Start();
		}
	}
}