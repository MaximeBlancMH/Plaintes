using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace Plaintes.Tests
{
    /// <summary>Environnement minimal pour FileSystemPieceStorage (la racine de test est un chemin absolu).</summary>
    internal sealed class FakeEnv : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Plaintes.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Test";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
    }
}
