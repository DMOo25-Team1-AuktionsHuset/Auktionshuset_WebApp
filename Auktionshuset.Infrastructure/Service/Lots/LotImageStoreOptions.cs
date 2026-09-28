namespace Auktionshuset.Infrastructure.Service.Lots
{
    /// <summary>
    /// Locates images written by older versions of the API so they can be imported into PostgreSQL.
    /// </summary>
    public sealed class LotImageStoreOptions
    {
        /// <summary>
        /// Gets the legacy image folder.
        /// </summary>
        public string RootPath { get; init; } = DefaultRootPath;

        /// <summary>
        /// Gets the former default folder beside the running application.
        /// </summary>
        public static string DefaultRootPath =>
            Path.Combine(AppContext.BaseDirectory, "uploads", "lots");
    }
}
