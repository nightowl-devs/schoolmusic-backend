namespace schoolmusic_backend.Services
{
    public record ExternalTrackDto(
        string ExternalId,
        string Title,
        string ArtistName,
        string? CoverUrl,
        int DurationMs,
        string? PreviewUrl,
        string Source
    );

    /// <summary>
    /// Interfejs dla zewnętrznych źródeł utworów (np. Spotify API, YouTube itp.)
    /// </summary>
    public interface ISongSourceService
    {
        /// <summary>
        /// Informuje, czy dane źródło ma skonfigurowane niezbędne poświadczenia API
        /// </summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Wyszukuje utwory po frazie
        /// </summary>
        Task<IEnumerable<ExternalTrackDto>> SearchTracksAsync(string query, int limit = 20);

        /// <summary>
        /// Pobiera szczegóły utworu na podstawie zewnętrznego identyfikatora
        /// </summary>
        Task<ExternalTrackDto?> GetTrackByIdAsync(string externalId);
    }
}
