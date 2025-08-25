// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Tests;

internal class ViceDownloader
{
    // URL
    private const string ViceZip = "https://github.com/VICE-Team/svn-mirror/releases/download/3.9.0/GTK3VICE-3.9-win64.zip";
    private const string ViceFolder = "GTK3VICE-3.9-win64";

    public static string Initialize()
    {
        var sharedPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".."));
        var vicePath = Path.Combine(sharedPath, ViceFolder);
        if (Directory.Exists(vicePath))
        {
            return vicePath;
        }

        var zipFile = Path.Combine(sharedPath, "GTK3VICE-3.9-win64.zip");
        using (var client = new HttpClient())
        {
            using (var response = client.GetAsync(ViceZip).Result)
            {
                response.EnsureSuccessStatusCode();
                using (var fs = new FileStream(zipFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    response.Content.CopyToAsync(fs).Wait();
                }
            }
        }
        System.IO.Compression.ZipFile.ExtractToDirectory(zipFile, sharedPath, true);
        File.Delete(zipFile);
        if (!Directory.Exists(vicePath))
        {
            throw new InvalidOperationException($"Failed to extract VICE to {vicePath}");
        }

        return vicePath;
    }
}