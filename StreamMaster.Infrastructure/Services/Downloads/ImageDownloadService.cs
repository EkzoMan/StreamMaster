﻿using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using StreamMaster.Application.Common.Interfaces;
using StreamMaster.Application.Hubs;
using StreamMaster.Domain.Cache;
using StreamMaster.Domain.Common;
using StreamMaster.Domain.Extensions;
using StreamMaster.Domain.Services;
using StreamMaster.SchedulesDirect.Domain.Interfaces;
using StreamMaster.SchedulesDirect.Domain.JsonClasses;
using StreamMaster.SchedulesDirect.Domain.Models;
using StreamMaster.SchedulesDirect.Helpers;

using System.Net;

namespace StreamMaster.Infrastructure.Services.Downloads;

public class ImageDownloadService : IHostedService, IDisposable, IImageDownloadService
{
    private readonly ILogger<ImageDownloadService> logger;
    private readonly ISchedulesDirectDataService schedulesDirectDataService;
    private readonly SemaphoreSlim downloadSemaphore;
    private readonly IMemoryCache memoryCache;
    private readonly IHubContext<StreamMasterHub, IStreamMasterHub> hubContext;

    private readonly IImageDownloadQueue imageDownloadQueue;
    private readonly ISchedulesDirectAPIService schedulesDirectAPI;

    private bool IsActive = false;
    private bool ImageLockOut => ImageLockOutDate.AddHours(1) >= DateTime.Now;
    private DateTime ImageLockOutDate = DateTime.MinValue;
    private readonly object Lock = new();

    public int TotalDownloadAttempts { get; private set; }
    public int TotalInQueue => imageDownloadQueue.Count();
    public int TotalSuccessful { get; private set; }
    public int TotalAlreadyExists { get; private set; }
    public int TotalNoArt { get; private set; }
    public int TotalErrors { get; private set; }

    private bool exitLoop = false;
    public ImageDownloadServiceStatus GetStatus()
    {
        return new ImageDownloadServiceStatus
        {
            TotalDownloadAttempts = TotalDownloadAttempts,
            TotalInQueue = TotalInQueue,
            TotalSuccessful = TotalSuccessful,
            TotalAlreadyExists = TotalAlreadyExists,
            TotalNoArt = TotalNoArt,
            TotalErrors = TotalErrors
        };
    }

    public ImageDownloadService(ILogger<ImageDownloadService> logger, ISchedulesDirectAPIService schedulesDirectAPI, IImageDownloadQueue imageDownloadQueue, IHubContext<StreamMasterHub, IStreamMasterHub> hubContext, IMemoryCache memoryCache, ISchedulesDirectDataService schedulesDirectDataService)
    {
        this.logger = logger;
        this.hubContext = hubContext;
        this.schedulesDirectDataService = schedulesDirectDataService;
        this.schedulesDirectAPI = schedulesDirectAPI;
        this.memoryCache = memoryCache;
        this.imageDownloadQueue = imageDownloadQueue;
        Setting settings = memoryCache.GetSetting();
        downloadSemaphore = new(settings.MaxConcurrentDownloads);

    }

    public void Start()
    {
        lock (Lock)
        {
            if (IsActive)
            {
                return;
            }
            _ = StartAsync(CancellationToken.None).ConfigureAwait(false);
            IsActive = true;

        }
    }
    //public void EnqueueProgramMetadataCollection(IEnumerable<ProgramMetadata> metadataCollection)
    //{
    //    foreach (var metadata in metadataCollection)
    //    {
    //        logger.LogDebug("Enqueue Program Metadata for program id {programId}", metadata.ProgramId);
    //        downloadQueue.Enqueue(metadata);
    //    }
    //    CheckStart();
    //}

    //// Method to add ProgramMetadata to the download queue
    //public void EnqueueProgramMetadata(ProgramMetadata metadata)
    //{
    //    logger.LogDebug("Enqueue Program Metadata for program id {programId}", metadata.ProgramId);
    //    downloadQueue.Enqueue(metadata);

    //    CheckStart();
    //}

    private bool lockedlogged = false;
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !exitLoop)
        {
            if (!ImageLockOut)
            {
                lockedlogged = false;
                //schedulesDirect.CheckToken();
                Setting setting = FileUtil.GetSetting();

                if (setting.SDSettings.SDEnabled && !imageDownloadQueue.IsEmpty() && BuildInfo.SetIsSystemReady)
                {
                    await DownloadImagesAsync(cancellationToken);
                }
            }
            else
            {
                if (!lockedlogged)
                {
                    logger.LogError($"Image downloads locked out,too many requests");
                    lockedlogged = true;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Implement stopping logic if needed        
        exitLoop = true;
        return Task.CompletedTask;
    }

    private async Task<bool> DownloadLogo(string uri, string logoPath, CancellationToken cancellationToken)
    {
        logoPath = FileUtil.CleanUpFileName(logoPath);
        ++TotalDownloadAttempts;

        try
        {
            if (File.Exists(logoPath))
            {
                ++TotalAlreadyExists;
                return false;
            }

            Setting setting = FileUtil.GetSetting();

            //HttpClient httpClient = SDHelpers.CreateHttpClient(setting.ClientUserAgent);
            //using var httpClient = new HttpClient();

            int maxRetryCount = 1; // Set the maximum number of retries

            for (int retryCount = 0; retryCount <= maxRetryCount; retryCount++)
            {
                HttpResponseMessage response = await GetSdImage(uri).ConfigureAwait(false);
                //HttpResponseMessage response = await httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    ++TotalErrors;
                    return false;
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    ++TotalErrors;
                    return false; // Maximum retry attempts reached
                }

                if (response.IsSuccessStatusCode)
                {
                    Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    if (stream != null)
                    {
                        using FileStream outputStream = File.Create(logoPath);
                        await stream.CopyToAsync(outputStream, cancellationToken);
                        stream.Close();
                        stream.Dispose();
                        FileInfo fileInfo = new(logoPath);
                        if (fileInfo.Length < 2000)
                        {
                            ++TotalErrors;
                            logger.LogError($"Failed to download image from {uri} to {logoPath}: {response.StatusCode}");
                            return false;
                        }
                        logger.LogDebug($"Downloaded image from {uri} to {logoPath}: {response.StatusCode}");

                        ++TotalSuccessful;
                        return true;
                    }
                }
                else
                {
                    ++TotalErrors;
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        ImageLockOutDate = DateTime.Now;
                        return false;
                    }
                    logger.LogError($"Failed to download image from {uri} to {logoPath}: {response.StatusCode}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download image from {Url} to {FileName}.", uri, logoPath);
        }
        ++TotalErrors;
        return false;
    }

    private async Task<HttpResponseMessage> GetSdImage(string uri)
    {
        return await schedulesDirectAPI.GetSdImage(uri);
    }

    private async Task DownloadImagesAsync(CancellationToken cancellationToken)
    {
        Setting setting = memoryCache.GetSetting();
        string artworkSize = string.IsNullOrEmpty(setting.SDSettings.ArtworkSize) ? "Md" : setting.SDSettings.ArtworkSize;


        while (!cancellationToken.IsCancellationRequested)
        {
            if (!imageDownloadQueue.TryDequeue(out ProgramMetadata? response))
            {
                // If the queue is empty, break out of the loop and wait for more items
                break;
            }
            try
            {
                string programId = response.ProgramId;
                List<ProgramArtwork> artwork = [];

                MxfProgram? program = schedulesDirectDataService.AllPrograms.Find(a => a.ProgramId == programId);
                if (program != null && program.extras != null)
                {
                    artwork = program.GetArtWork();
                }

                if (!artwork.Any())
                {
                    if (response.Data != null && response.Data.Any())
                    {
                        artwork = SDHelpers.GetTieredImages(response.Data, ["series", "sport", "episode"], artworkSize);
                    }
                    else
                    {
                        ++TotalNoArt;
                        await hubContext.Clients.All.MiscRefresh();
                        return;
                    }
                }

                foreach (ProgramArtwork art in artwork)
                {
                    await downloadSemaphore.WaitAsync(cancellationToken);
                    try
                    {
                        string? logoPath = art.Uri.GetSDImageFullPath();
                        if (logoPath == null)
                        {
                            continue;
                        }

                        string url = art.Uri.StartsWith("http") ? art.Uri : $"image/{art.Uri}";// await sDToken.GetAPIUrl($"image/{art.Uri}", cancellationToken);

                        _ = await DownloadLogo(url, logoPath, cancellationToken);
                    }
                    catch (Exception ex)
                    {

                    }
                    finally
                    {
                        await hubContext.Clients.All.MiscRefresh();
                        _ = downloadSemaphore.Release();
                    }
                }
            }
            catch (Exception ex)
            {

            }
            finally
            {

            }

        }
    }


    public void Dispose()
    {
        downloadSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
