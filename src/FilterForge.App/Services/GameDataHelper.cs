using System.IO;
using System.Windows;
using ThunderEagle.FilterForge.Core.Data;

namespace ThunderEagle.FilterForge.App.Services;

internal static class GameDataHelper
{
    public static async Task ExtractAsync()
    {
        var targetPath = Path.Combine(AppContext.BaseDirectory, "d4-data.json");

        if (File.Exists(targetPath))
        {
            var result = MessageBox.Show(
                $"d4-data.json already exists at:\n{targetPath}\n\nOverwrite it?",
                "Overwrite?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
        }

        using var src = FilterDataExporter.OpenEmbeddedStream();
        await using var dst = File.Create(targetPath);
        await src.CopyToAsync(dst);

        MessageBox.Show(
            $"Extracted to:\n{targetPath}\n\nEdit the file, then restart FilterForge to apply your changes.",
            "Game Data Extracted",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
