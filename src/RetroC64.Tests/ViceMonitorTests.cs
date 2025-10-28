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
            var registers = runner.Monitor.GetRegisters();
            Assert.IsGreaterThan(9, registers.Length, "No registers found");
            Assert.IsTrue(registers.Any(r => r.RegisterId == RegisterId.A), "No A register found");
            Console.WriteLine($"{string.Join(", ", registers)}");
        }

        // ResourceGetCommand
        {
            var resourceValue = runner.Monitor.GetResource("KernalName");
            Assert.AreEqual("kernal-901227-03.bin", resourceValue.AsString, "Resource name mismatch");
            Console.WriteLine(resourceValue);
        }

        // DisplayGetCommand
        {
            var displayResponse = runner.Monitor.GetDisplay();
            Assert.IsGreaterThan(0, displayResponse.Width, "Display width is zero");
            Assert.IsGreaterThan(0, displayResponse.Height, "Display height is zero");
            Console.WriteLine(displayResponse);
        }

        // PingCommand
        {
            runner.Monitor.Ping();
        }

        // PaletteGetCommand
        {
            var palette = runner.Monitor.GetPalette();
            Assert.HasCount(16, palette, "Palette does not contain 16 colors");
            Console.WriteLine(string.Join(", ", palette));
        }

        // ViceInfoCommand
        {
            var viceInfoResponse = runner.Monitor.GetViceInfo();
            Assert.IsTrue(viceInfoResponse.Version >= new Version(3, 9) , $"Version {viceInfoResponse.Version} must be >= 3.9");
            Console.WriteLine(viceInfoResponse);
        }

        // BanksAvailableCommand
        {
            var banks = runner.Monitor.GetBanksAvailable();
            Assert.IsNotEmpty(banks, "No banks found");
            Console.WriteLine(string.Join(", ", banks));
        }

        // MemorySetCommand
        {
            runner.Monitor.SetMemory(
                new MemorySetCommand()
                {
                    StartAddress = 0xC000,
                    BankId = new BankId(0),
                    Data = new byte[] { 0x34, 0x12 }
                });
        }

        // MemoryGetCommand
        {
            var buffer = runner.Monitor.GetMemory(
                new MemoryGetCommand()
                {
                    StartAddress = 0xC000,
                    EndAddress = 0xC001,
                    BankId = new BankId(0),
                });
            Assert.HasCount(2, buffer, "Memory length mismatch ");

            var span = MemoryMarshal.Cast<byte, ushort>(buffer);
            Assert.AreEqual(0x1234, span[0], "Read memory mismatch");

            Console.WriteLine(string.Join(", ", buffer.Select(x => $"${x:x2}")));
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