﻿using StreamMaster.Domain.Cache;
using StreamMaster.Domain.Dto;
using StreamMaster.Domain.Models;
using StreamMaster.Domain.Repository;
using StreamMaster.Domain.Services;

namespace StreamMaster.Application.ChannelGroups.Queries;

public record GetChannelGroupFromVideoStreamId(string VideoStreamId) : IRequest<ChannelGroupDto?>;

internal class GetChannelGroupFromVideoStreamIdHandler : BaseMediatorRequestHandler, IRequestHandler<GetChannelGroupFromVideoStreamId, ChannelGroupDto?>
{

    public GetChannelGroupFromVideoStreamIdHandler(ILogger<GetChannelGroupFromVideoStreamId> logger, IRepositoryWrapper repository, IMapper mapper, ISettingsService settingsService, IPublisher publisher, ISender sender, IHubContext<StreamMasterHub, IStreamMasterHub> hubContext, IMemoryCache memoryCache)
 : base(logger, repository, mapper, settingsService, publisher, sender, hubContext, memoryCache) { }
    public async Task<ChannelGroupDto?> Handle(GetChannelGroupFromVideoStreamId request, CancellationToken cancellationToken)
    {
        ChannelGroup? channelGroup = await Repository.ChannelGroup.GetChannelGroupFromVideoStreamId(request.VideoStreamId).ConfigureAwait(false);
        ChannelGroupDto? dto = Mapper.Map<ChannelGroupDto?>(channelGroup);
        MemoryCache.UpdateChannelGroupWithActives(dto);
        return dto;
    }
}