using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.RuntimeTrim;

/// <summary>
/// Serves the trimmed videos that <c>configPage.html</c> lists under each library.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("RuntimeTrim")]
public class TrimmedController(TrimStore store, ILibraryManager libraryManager) : ControllerBase
{
    /// <summary>
    /// Gets the videos whose runtime is still trimmed, each with the library it is in.
    /// </summary>
    /// <returns>The videos, unordered.</returns>
    [HttpGet("Trimmed")]
    public IEnumerable<TrimmedVideo> GetTrimmed()
    {
        foreach (var (id, trim) in store.Trims)
        {
            // A re-probe resets the runtime, and the trim is not re-applied in an excluded library.
            if (libraryManager.GetItemById(id) is not Video video
                || video.RunTimeTicks != trim.VideoTicks
                || libraryManager.GetCollectionFolders(video).FirstOrDefault() is not { } library)
            {
                continue;
            }

            var episode = video as Episode;
            yield return new TrimmedVideo(
                id,
                library.Id,
                video.Name,
                video.ProductionYear,
                episode?.SeriesId,
                episode?.SeriesName,
                episode?.ParentIndexNumber,
                episode?.IndexNumber,
                trim.FileTicks,
                trim.VideoTicks);
        }
    }
}

/// <summary>
/// A trimmed video as the settings page shows it. The series fields are set for episodes only.
/// </summary>
/// <param name="Id">The video's item id.</param>
/// <param name="LibraryId">The library it is in.</param>
/// <param name="Name">The movie or episode title.</param>
/// <param name="Year">The production year.</param>
/// <param name="SeriesId">The episode's series.</param>
/// <param name="SeriesName">The episode's series name.</param>
/// <param name="Season">The episode's season number.</param>
/// <param name="Episode">The episode's number within its season.</param>
/// <param name="FileTicks">The runtime before the trim.</param>
/// <param name="VideoTicks">The runtime after the trim.</param>
public sealed record TrimmedVideo(
    Guid Id,
    Guid LibraryId,
    string Name,
    int? Year,
    Guid? SeriesId,
    string? SeriesName,
    int? Season,
    int? Episode,
    long FileTicks,
    long VideoTicks);
