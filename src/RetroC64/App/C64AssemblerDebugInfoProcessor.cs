// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Asm6502;

namespace RetroC64.App;

internal class C64AssemblerDebugInfoProcessor : C64AppContext
{
    private readonly List<C64MemoryRange> _codeMemoryRanges = new();
    private readonly Dictionary<string, List<C64MemoryRange>> _mapFilePathToDebugLines = new(StringComparer.OrdinalIgnoreCase);
    
    private string? _programName;

    internal C64AssemblerDebugInfoProcessor(C64AppBuilder builder) : base(builder)
    {
    }

    public string ProgramName => _programName ?? "program";

    public Dictionary<string, int> Labels { get; } = new();

    public void Reset()
    {
        Labels.Clear();
        _codeMemoryRanges.Clear();
        _mapFilePathToDebugLines.Clear();
        _programName = null;
    }

    public bool TryFindCodeRegion(ushort address, out (ushort StartAddress, ushort Endaddress) range)
    {
        range = default;
        foreach (var codeMemoryRange in _codeMemoryRanges)
        {
            if (address >= codeMemoryRange.StartAddress && address <= codeMemoryRange.EndAddress)
            {
                range = (codeMemoryRange.StartAddress, codeMemoryRange.EndAddress);
                return true;
            }
        }
        return false;
    }

    public bool TryGetAddressFromFileAndLineNumber(string file, int lineNumber, out (ushort StartAddress, ushort Endaddress) range)
    {
        range = default;
        if (_mapFilePathToDebugLines.TryGetValue(file, out var lineInfos))
        {
            var span = CollectionsMarshal.AsSpan(lineInfos);
            var indexOfInfo = span.BinarySearch(new SearchLineComparer(lineNumber));
            if (indexOfInfo >= 0)
            {
                var lineInfo = lineInfos[indexOfInfo];
                range = (lineInfo.StartAddress, lineInfo.EndAddress);
                return true;
            }
        }
        return false;
    }

    public bool TryFindFileAndLineNumber(ushort address, [NotNullWhen(true)] out string? filePath, out int lineNumber)
    {
        // TODO: handle modules to output a path of code section (e.g. line coming from a specific module)
        filePath = null;
        lineNumber = 0;
        foreach (var codeMemoryRange in _codeMemoryRanges)
        {
            if (codeMemoryRange.TryFindFileAndLineNumber(address, out filePath, out lineNumber))
            {
                return true;
            }
        }
        return false;
    }

    public void AddDebugMap(C64AssemblerDebugMap? debugMap)
    {
        if (debugMap is null) return;

        List<C64MemoryRange> newCodeMemoryRanges = new();
        
        Mos6502AssemblerDebugInfo? orgDebugInfo = null;
        Mos6502AssemblerDebugInfo? endDebugInfo = null;

        var codeRangesStack = new Stack<C64MemoryRange>();

        foreach (var item in debugMap.Items)
        {
            switch (item.Kind)
            {
                case Mos6502AssemblerDebugInfoKind.OriginBegin:
                    _programName ??= item.Name;

                    if (orgDebugInfo is not null && codeRangesStack.Count > 0)
                    {
                        CloseCurrentCodeRange(null);
                    }
                    
                    orgDebugInfo = item;
                    break;
                case Mos6502AssemblerDebugInfoKind.LineInfo:
                    _programName ??= Path.GetFileNameWithoutExtension(item.Name);

                    if (codeRangesStack.Count == 0)
                    {
                        this.Warn($"A Code section end at address ${item.Address:x4} was found without a matching code section begin. Creating a fake section begin");
                        var orgCodeRange = new C64MemoryRange
                        {
                            Name = orgDebugInfo?.Name ?? _programName ?? "code",
                            StartAddress = item.Address,
                            EndAddress = item.Address
                        };
                        codeRangesStack.Push(orgCodeRange);
                    }

                    var currentRange = codeRangesStack.Peek();
                    currentRange.Children.Add(new C64MemoryRange()
                    {
                        Name = item.Name ?? "<unknown>",
                        IsLineInfo = true,
                        LineNumber = item.LineNumber ?? 0,
                        StartAddress = item.Address,
                        EndAddress = item.Address
                    });
                    
                    break;
                case Mos6502AssemblerDebugInfoKind.CodeSectionBegin:
                    var beginCodeRange = new C64MemoryRange
                    {
                        Name = item.Name ?? _programName ?? "code",
                        StartAddress = item.Address
                    };
                    codeRangesStack.Push(beginCodeRange);

                    break;
                case Mos6502AssemblerDebugInfoKind.CodeSectionEnd:
                    CloseCurrentCodeRange((ushort)(item.Address - 1));
                    break;
                case Mos6502AssemblerDebugInfoKind.FunctionBegin:
                    _programName ??= Path.GetFileNameWithoutExtension(item.Name);
                    var functionCodeRange = new C64MemoryRange()
                    {
                        Name = item.Name ?? "<unknown>",
                        IsLineInfo = true,
                        LineNumber = item.LineNumber ?? 0,
                        StartAddress = item.Address,
                        EndAddress = item.Address
                    };
                    codeRangesStack.Push(functionCodeRange);
                    break;
                case Mos6502AssemblerDebugInfoKind.FunctionEnd:
                    CloseCurrentCodeRange((ushort)(item.Address - 1));
                    break;

                case Mos6502AssemblerDebugInfoKind.DataSectionBegin:
                    // TODO
                    break;
                case Mos6502AssemblerDebugInfoKind.DataSectionEnd:
                    // TODO
                    break;
                case Mos6502AssemblerDebugInfoKind.End:
                    endDebugInfo = item;
                    break;
            }
        }

        ushort? endAddress = endDebugInfo?.Address;
        if (endAddress != null)
        {
            endAddress = (ushort)(endAddress.Value - 1);
        }

        while (codeRangesStack.Count > 0)
        {
            CloseCurrentCodeRange(endAddress, logWarning: false);
        }

        foreach (var newCodeMemoryRange in newCodeMemoryRanges)
        {
            newCodeMemoryRange.UpdateRange();
        }

        _codeMemoryRanges.AddRange(newCodeMemoryRanges);

        UpdateFilesToLineInfo();
        
        foreach (var label in debugMap.Labels)
        {
            if (!string.IsNullOrEmpty(label.Name))
            {
                Labels[label.Name] = label.Address;
            }
        }

        foreach (var label in debugMap.ZpLabels)
        {
            if (!string.IsNullOrEmpty(label.Name))
            {
                Labels[label.Name] = label.Address;
            }
        }


        void CloseCurrentCodeRange(ushort? address, bool logWarning = true)
        {
            if (codeRangesStack.Count > 0)
            {
                var endCodeRange = codeRangesStack.Pop();
                endCodeRange.EndAddress = address ?? 0;

                if (codeRangesStack.Count > 0)
                {
                    // Nested code range
                    var parentRange = codeRangesStack.Peek();
                    parentRange.Children.Add(endCodeRange);
                }
                else
                {
                    // Top-level code range
                    newCodeMemoryRanges.Add(endCodeRange);
                }
            }
            else
            {
                if (newCodeMemoryRanges.Count > 0)
                {
                    var last = newCodeMemoryRanges[^1];
                    last.EndAddress = address ?? 0;
                }

                if (address is not null && logWarning)
                {
                    this.Warn($"A Code section end at address ${address.Value:x4} was found without a matching code section begin.");
                }
            }
        }
    }

    private void UpdateFilesToLineInfo()
    {
        _mapFilePathToDebugLines.Clear();
        foreach (var codeMemoryRange in _codeMemoryRanges)
        {
            CollectLineInfo(codeMemoryRange);
        }

        // Order line infos for binary search
        foreach (var fileToLocationPair in _mapFilePathToDebugLines)
        {
            fileToLocationPair.Value.Sort((a, b) => a.LineNumber.CompareTo(b.LineNumber));
        }


        void CollectLineInfo(C64MemoryRange memoryRange)
        {
            if (memoryRange.IsLineInfo)
            {
                if (!_mapFilePathToDebugLines.TryGetValue(memoryRange.Name, out var list))
                {
                    list = new List<C64MemoryRange>();
                    _mapFilePathToDebugLines[memoryRange.Name] = list;
                }
                list.Add(memoryRange);
            }
            foreach (var child in memoryRange.Children)
            {
                CollectLineInfo(child);
            }
        }
    }

    private readonly record struct SearchLineComparer(int LineNumber) : IComparable<C64MemoryRange>
    {
        public int CompareTo(C64MemoryRange? other) => LineNumber.CompareTo(other?.LineNumber ?? 0);
    }

    private class C64MemoryRange
    {
        public string Name { get; set; } = "code";

        public bool IsLineInfo { get; set; }

        public ushort StartAddress { get; set; }

        public ushort EndAddress { get; set; }
        
        public List<C64MemoryRange> Children { get; } = new();
        
        public int LineNumber { get; set; }

        public int ColumnNumber { get; set; }

        public bool TryFindFileAndLineNumber(ushort address, [NotNullWhen(true)] out string? filePath, out int lineNumber)
        {
            filePath = null;
            lineNumber = 0;

            if (address >= StartAddress && address <= EndAddress)
            {
                if (IsLineInfo)
                {
                    filePath = Name;
                    lineNumber = LineNumber;
                    return true;
                }

                var span = CollectionsMarshal.AsSpan(Children);
                var index = span.BinarySearch(new SearchAddressComparer(address));

                if (index >= 0 && span[index].TryFindFileAndLineNumber(address, out filePath, out lineNumber))
                {
                    return true;
                }
            }

            return false;
        }

        public void UpdateRange()
        {
            foreach (var child in Children)
            {
                child.UpdateRange();
            }

            Children.Sort((a, b) => a.StartAddress.CompareTo(b.StartAddress));

            if (EndAddress < StartAddress)
            {
                EndAddress = StartAddress;
            }

            var endAddressOfLastChildren = Children.Count > 0 ? (ushort)(Children[^1].EndAddress) : EndAddress;
            if (endAddressOfLastChildren > EndAddress)
            {
                EndAddress = endAddressOfLastChildren;
            }
        }

        private readonly record struct SearchAddressComparer(ushort Address) : IComparable<C64MemoryRange>
        {
            public int CompareTo(C64MemoryRange? other)
            {
                if (Address < other!.StartAddress) return -1;
                if (Address > other.EndAddress) return 1;
                return 0;
            }
        }

        public override string ToString() => ToDebuggerDisplay();

        private string ToDebuggerDisplay()
        {
            if (IsLineInfo)
            {
                if (Children.Count > 0)
                {
                    return $"Function in File {Name} [${StartAddress:x4}-${EndAddress:x4}] Line: {LineNumber} Children = {Children.Count}";
                }
                else
                {
                    return $"File {Name} [${StartAddress:x4}] Line: {LineNumber}";
                }
            }
            else
            {
                return $"{Name} [${StartAddress:x4}-${EndAddress:x4}] Children = {Children.Count}";
            }
        }
    }
}