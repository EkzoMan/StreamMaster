﻿using StreamMaster.Domain.Dto;
using StreamMaster.Domain.Pagination;
using StreamMaster.Domain.Repository;
using StreamMaster.Domain.Services;

namespace StreamMaster.Application.EPGFiles.Queries;

public record GetPagedEPGFiles(EPGFileParameters Parameters) : IRequest<PagedResponse<EPGFileDto>>;

internal class GetPagedEPGFilesHandler : BaseMediatorRequestHandler, IRequestHandler<GetPagedEPGFiles, PagedResponse<EPGFileDto>>
{
    public GetPagedEPGFilesHandler(ILogger<GetPagedEPGFiles> logger, IRepositoryWrapper repository, IMapper mapper, ISettingsService settingsService, IPublisher publisher, ISender sender, IHubContext<StreamMasterHub, IStreamMasterHub> hubContext, IMemoryCache memoryCache)
: base(logger, repository, mapper, settingsService, publisher, sender, hubContext, memoryCache) { }

    public async Task<PagedResponse<EPGFileDto>> Handle(GetPagedEPGFiles request, CancellationToken cancellationToken = default)
    {
        PagedResponse<EPGFileDto> epgFiles = await Repository.EPGFile.GetPagedEPGFiles(request.Parameters);

        if (request.Parameters.PageSize == 0)
        {
            return Repository.EPGFile.CreateEmptyPagedResponse();
        }


        //foreach (EPGFileDto epgFileDto in epgFiles.Data)
        //{
        //    var c = await Sender.Send(new GetProgrammesRequest(), cancellationToken).ConfigureAwait(false);
        //   var proprammes = c.Where(a => a.EPGFileId == epgFileDto.Id).ToList();
        //    if (proprammes.Any())
        //    {
        //        epgFileDto.EPGStartDate = proprammes.Min(a => a.StartDateTime);
        //        epgFileDto.EPGStopDate = proprammes.Max(a => a.StopDateTime);
        //    }
        //}
        return epgFiles;
    }
}