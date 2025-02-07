﻿using StreamMaster.Domain.Dto;
using StreamMaster.Domain.Pagination;
using StreamMaster.Domain.Repository;
using StreamMaster.Domain.Services;

using StreamMaster.Application.VideoStreams.Events;

namespace StreamMaster.Application.VideoStreams.Commands;

public record SetVideoStreamsLogoFromEPGFromParametersRequest(VideoStreamParameters Parameters) : IRequest<List<VideoStreamDto>> { }

[LogExecutionTimeAspect]
public class SetVideoStreamsLogoFromEPGFromParametersRequestHandler : BaseMediatorRequestHandler, IRequestHandler<SetVideoStreamsLogoFromEPGFromParametersRequest, List<VideoStreamDto>>
{

    public SetVideoStreamsLogoFromEPGFromParametersRequestHandler(ILogger<SetVideoStreamsLogoFromEPGFromParametersRequest> logger, IRepositoryWrapper repository, IMapper mapper,ISettingsService settingsService, IPublisher publisher, ISender sender, IHubContext<StreamMasterHub, IStreamMasterHub> hubContext, IMemoryCache memoryCache)
: base(logger, repository, mapper, settingsService, publisher, sender, hubContext, memoryCache) { }


    public async Task<List<VideoStreamDto>> Handle(SetVideoStreamsLogoFromEPGFromParametersRequest request, CancellationToken cancellationToken)
    {
        List<VideoStreamDto> results = await Repository.VideoStream.SetVideoStreamsLogoFromEPGFromParameters(request.Parameters, cancellationToken).ConfigureAwait(false);

        if (results.Any())
        {
            await Publisher.Publish(new UpdateVideoStreamsEvent(results), cancellationToken).ConfigureAwait(false);
        }
        return results;
    }
}