namespace Auktionshuset.Infrastructure.Service.Lots
{
    /// <summary>
    /// Describes where lot images are stored on disk. The same path is served by the API as
    /// static files, so the store and the file provider must agree on it.
    /// </summary>
    public sealed class LotImageStoreOptions
    {
        /// <summary>
        /// Gets the folder that lot images are written to and served from.
        /// </summary>
        public string RootPath { get; init; } = DefaultRootPath;

        /// <summary>
        /// Gets the folder used when no explicit path is configured, placed next to the running
        /// application so it also works when the API is started from a test host.
        /// </summary>
        public static string DefaultRootPath =>
            Path.Combine(AppContext.BaseDirectory, "uploads", "lots");
    }
}
