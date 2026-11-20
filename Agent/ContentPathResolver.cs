using System;
using System.IO;
using Microsoft.Extensions.Hosting;

namespace Agent
{
    /// <summary>
    /// Turns a relative path from config (e.g. "Agent/system-prompt.md") into a full path.
    ///
    /// Relative paths are read from the app's CONTENT ROOT: the API project folder when you use
    /// "dotnet run", and the publish folder in Docker. Without a web host (e.g. in tests), the
    /// folder the program runs from is used instead.
    /// </summary>
    public sealed class ContentPathResolver
    {
        private readonly string _root;

        /// <summary>Uses the host's content root when there is a host, otherwise the program's own folder.</summary>
        public ContentPathResolver(IServiceProvider services)
        {
            IHostEnvironment environment = services.GetService(typeof(IHostEnvironment)) as IHostEnvironment;
            _root = environment != null ? environment.ContentRootPath : AppContext.BaseDirectory;
        }

        /// <summary>The folder relative paths are read from.</summary>
        public string Root
        {
            get { return _root; }
        }

        /// <summary>Returns the full path. Full paths are returned unchanged.</summary>
        public string Resolve(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (Path.IsPathRooted(path))
                return path;

            return Path.GetFullPath(Path.Combine(_root, path));
        }
    }
}
