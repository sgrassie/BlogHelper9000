using System.IO.Abstractions;

namespace BlogHelper9000.Core;

public static class CredentialsPaths
{
    private const string FileName = "bloghelper9000.json";

    public static string UnsplashCredentialsPath(IFileSystem fileSystem)
    {
        var appDataDirectory = fileSystem.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BlogHelper9000");

        return fileSystem.Path.Combine(appDataDirectory, FileName);
    }
}
