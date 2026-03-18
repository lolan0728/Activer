using System.Reflection;
using Activer.Mvvm;

namespace Activer.ViewModels;

public sealed class VersionViewModel : ObservableObject
{
    public VersionViewModel()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var assemblyVersion = assembly.GetName().Version;
        var version = assemblyVersion is null
            ? "1.0.3"
            : $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";

        VersionText = $"Activer v{version}";
        AuthorText = "Author: Eos Lolan";
        CopyrightText = "© 2025 Eos Lolan. Licensed under the MIT License.";
    }

    public string VersionText { get; }

    public string AuthorText { get; }

    public string CopyrightText { get; }
}
