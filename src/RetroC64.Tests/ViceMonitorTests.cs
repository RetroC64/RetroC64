// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Runtime.InteropServices;
using RetroC64.Vice;
using RetroC64.Vice.Monitor;
using RetroC64.Vice.Monitor.Commands;
using RetroC64.Vice.Monitor.Responses;

namespace RetroC64.Tests;

[TestClass, RuntimeCondition("win-x64")]
public class ViceMonitorTests
{
    public TestContext? TestContext { get; set; }

    /// <summary>
    /// Test a few commands to ensure the monitor works correctly.
    /// </summary>
    [TestMethod]
    public async Task TestCommands()
    {
        using var runner = new ViceMonitorRunner(TestContext!);
        runner.Start();

        // RegistersAvailableCommand
        {
            var response = await runner.Monitor.SendCommandAsync(new RegistersAvailableCommand());
            Assert.IsTrue(response is RegistersAvailableResponse);

            var registerResponse = (RegistersAvailableResponse)response;
            Assert.IsTrue(registerResponse.Registers.Length > 0, "No registers found");
            Assert.IsTrue(registerResponse.Registers.Any(r => r.Name == "A"), "No A register found");
            Console.WriteLine(response);
        }

        // ResourceGetCommand
        {
            var response = await runner.Monitor.SendCommandAsync(new ResourceGetCommand() { ResourceName = "KernalName" });
            Assert.IsTrue(response is ResourceGetResponse);
            var resourceResponse = (ResourceGetResponse)response;
            Assert.AreEqual("kernal-901227-03.bin", resourceResponse.ResourceValue.AsString, "Resource name mismatch");
            Console.WriteLine(response);
        }

        // DisplayGetCommand
        {
            var response = await runner.Monitor.SendCommandAsync(new DisplayGetCommand());
            Assert.IsTrue(response is DisplayGetResponse);
            var displayResponse = (DisplayGetResponse)response;
            Assert.IsTrue(displayResponse.Width > 0, "Display width is zero");
            Assert.IsTrue(displayResponse.Height > 0, "Display height is zero");
            Console.WriteLine(response);
        }

        // PingCommand
        {
            var response = await runner.Monitor.SendCommandAsync(new PingCommand());
            Assert.IsTrue(response is GenericResponse);
            Console.WriteLine(response);
        }

        // PaletteGetCommand
        {
            var response = await runner.Monitor.SendCommandAsync(new PaletteGetCommand());
            Assert.IsTrue(response is PaletteGetResponse);
            var paletteResponse = (PaletteGetResponse)response;
            Assert.AreEqual(16, paletteResponse.Palette.Length, "Palette does not contain 16 colors");
            Console.WriteLine(response);
        }

        // ViceInfoCommand
        {
            var response = await runner.Monitor.SendCommandAsync(new ViceInfoCommand());
            Assert.IsTrue(response is ViceInfoResponse);
            var viceInfoResponse = (ViceInfoResponse)response;
            Assert.IsTrue(viceInfoResponse.Version >= new Version(3, 9) , $"Version {viceInfoResponse.Version} must be >= 3.9");
            Console.WriteLine(response);
        }

        // BanksAvailableCommand
        {
            var response = await runner.Monitor.SendCommandAsync(new BanksAvailableCommand());
            Assert.IsTrue(response is BanksAvailableResponse);
            var banksResponse = (BanksAvailableResponse)response;
            Assert.IsTrue(banksResponse.Banks.Count > 0, "No banks found");
            Console.WriteLine(response);
        }

        // MemorySetCommand
        {
            var setResponse = await runner.Monitor.SendCommandAsync(new MemorySetCommand() { StartAddress = 0xC000, BankId = new BankId(0), Memspace = MemSpace.MainMemory, Data = new byte[] { 0x34, 0x12 } });
            Assert.IsTrue(setResponse is GenericResponse);
            Console.WriteLine(setResponse);
        }

        // MemoryGetCommand
        {
            var response = await runner.Monitor.SendCommandAsync(new MemoryGetCommand() { StartAddress = 0xC000, EndAddress = 0xC001, BankId = new BankId(0), Memspace = MemSpace.MainMemory});
            Assert.IsTrue(response is GenericResponse);
            var memoryResponse = (GenericResponse)response;
            Assert.AreEqual(4, memoryResponse.Body.Length, "Memory length mismatch ");

            var span = MemoryMarshal.Cast<byte, ushort>(memoryResponse.Body.AsSpan());
            Assert.AreEqual(2, span[0], "Must be length of 2");
            Assert.AreEqual(0x1234, span[1], "Read memory mismatch");
            
            Console.WriteLine(response);
        }
        
        await runner.Shutdown();
    }

    
    private class ViceMonitorRunner : IDisposable
    {
        private readonly TestContext _testContext;
        private readonly ViceRunner _runner;
        private readonly ViceMonitor _monitor;
        private readonly List<string> _outputLines = new List<string>();


        public ViceMonitorRunner(TestContext testContext)
        {
            _testContext = testContext;
            _runner = new ViceRunner();
            _runner.ExecutableName = ViceDownloader.InitializeAndGetExePath("x64sc");
            _runner.Arguments.Add("+logcolorize");
            _runner.Arguments.Add("-console");
            _runner.Verbose = false;

            _monitor = new ViceMonitor();
        }

        public ViceRunner Runner => _runner;

        public ViceMonitor Monitor => _monitor;

        public List<string> OutputLines => _outputLines;


        public void Start()
        {
            var cancellationTokenSource = new CancellationTokenSource(2000);
            Start(cancellationTokenSource.Token);
        }

        public void Start(CancellationToken ct)
        {
            _runner.Start();


            while (_runner.IsRunning)
            {
                ct.ThrowIfCancellationRequested();

                if (_runner.TryDequeueOutput(out var output))
                {
                    _outputLines.Add(output);
                }
                if (_runner.TryDequeueError(out var error))
                {
                    _outputLines.Add($"STDERR: {error}");
                }

                if (!_monitor.IsConnected)
                {
                    if (_monitor.TryConnect())
                    {
                        _monitor.SendCommand(new PingCommand());
                        _monitor.SendCommand(new ExitCommand());
                    }
                }

                if (output is not null && output.StartsWith("Unit 8:  RESET.", StringComparison.Ordinal))
                {
                    break;
                }

                Thread.Sleep(1);
            }
        }

        public async Task Shutdown()
        {
            var cancellationTokenSource = new CancellationTokenSource(2000);
            await _monitor.SendCommandAsync(new QuitCommand(), cancellationTokenSource.Token);
        }
        
        public void Dispose()
        {
            if (_testContext.CurrentTestOutcome != UnitTestOutcome.Passed)
            {
                Console.WriteLine();
                Console.WriteLine("Test failing, current VICE output: ");
                foreach (var output in _outputLines)
                {
                    Console.WriteLine(output);
                }
            }
            
            _monitor.Dispose();
            _runner.Dispose();
        }
    }



}