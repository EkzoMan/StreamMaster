﻿using StreamMaster.Domain.Dto;
using StreamMaster.Domain.Repository;
using StreamMaster.Domain.Services;

namespace StreamMaster.Application.VideoStreams.Queries;

public record GetVideoStreamsForChannelGroups(List<int> ChannelGroupIds) : IRequest<List<VideoStreamDto>>;

internal class GetVideoStreamsForChannelGroupsHandler(ILogger<GetVideoStreamsForChannelGroups> logger, IRepositoryWrapper repository, IMapper mapper,ISettingsService settingsService, IPublisher publisher, ISender sender, IHubContext<StreamMasterHub, IStreamMasterHub> hubContext, IMemoryCache memoryCache) : BaseMediatorRequestHandler(logger, repository, mapper,settingsService, publisher, sender, hubContext, memoryCache), IRequestHandler<GetVideoStreamsForChannelGroups, List<VideoStreamDto>>
{
    public async Task<List<VideoStreamDto>> Handle(GetVideoStreamsForChannelGroups request, CancellationToken cancellationToken)
    {
        List<VideoStreamDto> ret = await Repository.VideoStream.GetVideoStreamsForChannelGroups(request.ChannelGroupIds, cancellationToken);
        return ret;
    }
}