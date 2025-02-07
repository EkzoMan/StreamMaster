﻿using StreamMaster.Domain.Common;
using StreamMaster.SchedulesDirect.Domain.Enums;
using StreamMaster.SchedulesDirect.Helpers;

using System.Collections.Concurrent;
using System.Text.Json;

namespace StreamMaster.SchedulesDirect;
public partial class SchedulesDirectImages
{
    public List<MxfProgram> sportEvents { get; set; } = [];
    private List<string> sportsImageQueue = [];
    private ConcurrentBag<ProgramMetadata> sportsImageResponses = [];

    public async Task<bool> GetAllSportsImages()
    {

        Setting settings = memoryCache.GetSetting();
        // reset counters
        sportsImageQueue = [];
        sportsImageResponses = [];
        //IncrementNextStage(sportEvents.Count);
        if (!settings.SDSettings.SeasonEventImages)
        {
            return true;
        }

        // scan through each series in the mxf
        logger.LogInformation("Entering GetAllSportsImages() for {totalObjects} sports events.", sportEvents.Count);
        foreach (MxfProgram sportEvent in sportEvents)
        {
            string md5 = sportEvent.extras["md5"];
            if (epgCache.JsonFiles.ContainsKey(md5) && !string.IsNullOrEmpty(epgCache.JsonFiles[md5].Images))
            {
                //IncrementProgress();              
                using StringReader reader = new(epgCache.JsonFiles[md5].Images);

                List<ProgramArtwork>? artwork = JsonSerializer.Deserialize<List<ProgramArtwork>>(reader.ReadToEnd());
                if (artwork != null)
                {
                    sportEvent.extras.AddOrUpdate("artwork", artwork);
                    sportEvent.mxfGuideImage = epgCache.GetGuideImageAndUpdateCache(artwork, ImageType.Program);
                }
            }
            else
            {
                sportsImageQueue.Add(sportEvent.ProgramId);
            }
        }
        logger.LogDebug($"Found {processedObjects} cached/unavailable sport event image links.");

        // maximum 500 queries at a time
        if (sportsImageQueue.Count > 0)
        {
            SemaphoreSlim semaphore = new(SchedulesDirect.MaxParallelDownloads, SchedulesDirect.MaxParallelDownloads);
            List<Task> tasks = [];
            int processedCount = 0;

            for (int i = 0; i <= (sportsImageQueue.Count / SchedulesDirect.MaxImgQueries); i++)
            {
                int startIndex = i * SchedulesDirect.MaxImgQueries;
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        int itemCount = Math.Min(sportsImageQueue.Count - startIndex, SchedulesDirect.MaxImgQueries);
                        await DownloadImageResponsesAsync(sportsImageQueue, sportsImageResponses, startIndex).ConfigureAwait(false);
                        Interlocked.Add(ref processedCount, itemCount);
                        logger.LogInformation("Downloaded sport event images {ProcessedCount} of {TotalCount}", processedCount, sportsImageQueue.Count);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error downloading sport event images at {StartIndex}", startIndex);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            // Continue with the rest of your processing
            ProcessSportsImageResponses();
            imageDownloadQueue.EnqueueProgramMetadataCollection(sportsImageResponses);

            if (processedObjects != totalObjects)
            {
                logger.LogWarning("Failed to download and process {FailedCount} sport event image links.", sportEvents.Count - processedObjects);
            }
        }

        //if (sportsImageQueue.Count > 0)
        //{
        //    _ = Parallel.For(0, (sportsImageQueue.Count / SchedulesDirect.MaxImgQueries) + 1, new ParallelOptions { MaxDegreeOfParallelism = SchedulesDirect.MaxParallelDownloads }, i =>
        //    {
        //        DownloadImageResponses(sportsImageQueue, sportsImageResponses, i * SchedulesDirect.MaxImgQueries);
        //    });

        //    ProcessSportsImageResponses();
        //    imageDownloadQueue.EnqueueProgramMetadataCollection(sportsImageResponses);
        //    //await DownloadImages(sportsImageResponses, cancellationToken);
        //    if (processedObjects != totalObjects)
        //    {
        //        logger.LogWarning($"Failed to download and process {sportEvents.Count - processedObjects} sport event image links.");
        //    }
        //}

        //UpdateIcons(sportEvents);

        logger.LogInformation("Exiting GetAllSportsImages(). SUCCESS.");

        sportsImageQueue = []; sportsImageResponses = []; sportEvents.Clear();
        epgCache.SaveCache();
        return true;
    }



    private void ProcessSportsImageResponses()
    {
        // process request response
        if (sportsImageResponses == null)
        {
            return;
        }

        Setting setting = memoryCache.GetSetting();
        string artworkSize = string.IsNullOrEmpty(setting.SDSettings.ArtworkSize) ? "Md" : setting.SDSettings.ArtworkSize;

        foreach (ProgramMetadata response in sportsImageResponses)
        {
            //IncrementProgress();
            if (response.Data == null)
            {
                continue;
            }

            MxfProgram? mxfProgram = sportEvents.SingleOrDefault(arg => arg.ProgramId == response.ProgramId);
            if (mxfProgram == null)
            {
                continue;
            }

            // get sports event images      
            List<ProgramArtwork> artwork = SDHelpers.GetTieredImages(response.Data, ["team event", "sport event"], artworkSize);
            mxfProgram.extras.AddOrUpdate("artwork", artwork);

            mxfProgram.mxfGuideImage = epgCache.GetGuideImageAndUpdateCache(artwork, ImageType.Program, mxfProgram.extras["md5"]);
        }
    }
}

