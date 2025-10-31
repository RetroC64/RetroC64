// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json;
using RetroC64.Vice.Monitor;
using RetroC64.Vice.Monitor.Commands;

namespace RetroC64.Debugger;

internal class C64DebugVariable : Variable
{
    private readonly Func<C64DebugMachineState, string> _getterValue;
    private readonly Action<ViceMonitor, C64DebugMachineState, ushort>? _setterValue;
    private readonly int? _index;
    private readonly string? _alias;
    private readonly ushort _address;
    private readonly bool _hasAddress;

    public C64DebugVariable(C64DebugVariableScope scope, string name, Func<C64DebugMachineState, string> getter)
    {
        Scope = scope;
        Name = name;
        _getterValue = getter;
    }

    public C64DebugVariable(C64DebugVariableScope scope, int index, string name, Func<C64DebugMachineState, string> getter) : this(scope, name, getter)
    {
        _index = index;

        if (scope == C64DebugVariableScope.CpuFlags)
        {
            _setterValue = (monitor, state, value) =>
            {
                var newValue = (byte)state.SR;
                newValue &= (byte)~(1 << _index.Value);
                newValue |= (byte)((value & 1) << _index.Value);

                RegisterValue[] newRegs = [new(RegisterId.FLAGS, newValue)];
                monitor.SetRegisters(newRegs);
                state.UpdateRegisters(newRegs);
            };
        }
        else
        {
            throw new ArgumentException($"Scope {scope} not handled for custom index");
        }
    }

    public C64DebugVariable(C64DebugVariableScope scope, string name, Func<C64DebugMachineState, string> getter, RegisterId registerId) : this(scope, name, getter)
    {
        // Special case for PC to set memory reference
        if (registerId == RegisterId.PC)
        {
            _getterValue = state =>
            {
                MemoryReference = $"0x{state.PC:x4}";
                return getter(state);
            };
        }

        _setterValue = (monitor, state, value) =>
        {
            RegisterValue[] newRegs = [new(registerId, value)];
            monitor.SetRegisters(newRegs);
            state.UpdateRegisters(newRegs);
        };
    }

    public C64DebugVariable(C64DebugVariableScope scope, ushort address, string? alias = null)
    {
        Scope = scope;
        _address = address;
        _alias = alias;
        _hasAddress = true;

        UpdateNameFromAddress(null);

        _getterValue = state =>
        {
            UpdateNameFromAddress(state);
            var value = state.Ram[address];
            return $"${value:x2} ({value})";
        };

        _setterValue = (monitor, state, value) =>
        {
            var b = (byte)(value & 0xFF);
            if (address < 2)
            {
                RegisterValue[] newRegs = [new(address == 0 ? RegisterId.Zero : RegisterId.One, value)];
                monitor.SetRegisters(newRegs);
                state.UpdateRegisters(newRegs);
            }
            else
            {
                monitor.SetMemory(new MemorySetCommand()
                {
                    StartAddress = address,
                    Data = new[] { b }
                });
            }
            state.Ram[address] = b;
        };
    }

    [JsonIgnore]
    public ushort Address => _address;

    [JsonIgnore]
    public bool HasAddress => _hasAddress;

    [JsonIgnore]
    public bool HasZpName { get; private set; }

    [JsonIgnore]
    public C64DebugVariableScope Scope { get; }

    [JsonIgnore]
    public bool CanWrite => _setterValue != null;

    public void ReadFromMachineState(C64DebugMachineState state)
    {
        Value = _getterValue(state);
    }

    public void WriteToMachineState(ViceMonitor monitor, C64DebugMachineState state, string text)
    {
        if (_setterValue != null)
        {
            if (C64DebugAdapter.TryParseTextAsInt(text, out var value))
            {
                _setterValue(monitor, state, (ushort)value);
                // Update from the set value
                Value = _getterValue(state);
            }
            else
            {
                throw new InvalidOperationException($"Cannot parse value `{text}` as integer");
            }
        }
        else
        {
            throw new InvalidOperationException($"Variable {Name} is read-only");
        }
    }

    private void UpdateNameFromAddress(C64DebugMachineState? state)
    {
        if (_address < 0x100)
        {
            if (state is not null && state.ZpAddresses.TryGetValue((byte)_address, out var name))
            {
                HasZpName = true;
                Name = $"${_address:x2} ({name})";
            }
            else
            {
                HasZpName = false;
                Name = $"${_address:x2}";
            }
        }
        else if (_address >= 0x100 && _address <= 0x1FF)
        {
            var sp = (byte)(_address - 0x100);
            Name = state is not null && state.SP == sp ? $"${sp:x2} (SP)" : $"${sp:x2}";
        }
        else
        {
            Name = _alias is null ? $"${_address:x4}" : $"${_address:x4} ({_alias})";
        }
    }
}