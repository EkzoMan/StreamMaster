﻿using SixLabors.ImageSharp;

using StreamMaster.Domain.Dto;
using StreamMaster.Domain.Enums;
using StreamMaster.SchedulesDirect.Domain.Enums;

namespace StreamMaster.SchedulesDirect;
public partial class SchedulesDirect
{
    public async Task<List<string>?> GetCustomLogosFromServerAsync(string server)
    {
        return await schedulesDirectAPI.GetApiResponse<List<string>>(APIMethod.GET, server);
    }

    private void UpdateMovieIcons(List<MxfProgram> mxfPrograms)
    {
        foreach (MxfProgram? prog in mxfPrograms.Where(a => a.extras.ContainsKey("artwork")))
        {
            List<ProgramArtwork> artwork = prog.extras["artwork"];
            UpdateIcons(artwork.Select(a => a.Uri), prog.Title);
        }
    }

    private void UpdateIcons(List<MxfProgram> mxfPrograms)
    {
        foreach (MxfProgram? prog in mxfPrograms.Where(a => a.extras.ContainsKey("artwork")))
        {
            List<ProgramArtwork> artwork = prog.extras["artwork"];
            UpdateIcons(artwork.Select(a => a.Uri), prog.Title);
        }
    }

    private void UpdateSeasonIcons(List<MxfSeason> mxfSeasons)
    {
        foreach (MxfSeason? prog in mxfSeasons.Where(a => a.extras.ContainsKey("artwork")))
        {
            List<ProgramArtwork> artwork = prog.extras["artwork"];
            UpdateIcons(artwork.Select(a => a.Uri), prog.Title);
        }
    }
    private void UpdateIcons(List<MxfSeriesInfo> mxfSeriesInfos)
    {
        foreach (MxfSeriesInfo? prog in mxfSeriesInfos.Where(a => a.extras.ContainsKey("artwork")))
        {
            List<ProgramArtwork> artwork = prog.extras["artwork"];
            UpdateIcons(artwork.Select(a => a.Uri), prog.Title);
        }
    }
    private void AddIcon(string artworkUri, string title)
    {
        if (string.IsNullOrEmpty(artworkUri))
        {
            return;
        }

        List<IconFileDto> icons = iconService.GetIcons();

        if (icons.Any(a => a.SMFileType == SMFileTypes.SDImage && a.Source == artworkUri))
        {
            return;
        }

        iconService.AddIcon(new IconFileDto { Source = artworkUri, SMFileType = SMFileTypes.SDImage, Name = title });

    }

    public void UpdateIcons(IEnumerable<string> artworkUris, string title)
    {
        if (!artworkUris.Any())
        {
            return;
        }

        List<IconFileDto> icons = iconService.GetIcons(SMFileTypes.SDImage);

        foreach (string artworkUri in artworkUris)
        {
            if (icons.Any(a => a.Source == artworkUri))
            {
                continue;
            }
            AddIcon(artworkUri, title);
        }
        //iconService.SetIndexes();
    }
}