// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// Hot Reload support
using RetroC64.App;

[assembly: System.Reflection.Metadata.MetadataUpdateHandlerAttribute(typeof(C64HotReloadService))]

namespace RetroC64.App;

internal static class C64HotReloadService
{
    public static event Action<Type[]?>? ClearCacheEvent;

    public static event Action<Type[]?>? UpdateApplicationEvent;

    internal static void ClearCache(Type[]? types)
    {
        ClearCacheEvent?.Invoke(types);
    }

    internal static void UpdateApplication(Type[]? types)
    {
        UpdateApplicationEvent?.Invoke(types);
    }
}