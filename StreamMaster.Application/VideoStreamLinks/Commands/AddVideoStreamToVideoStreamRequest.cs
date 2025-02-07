﻿using StreamMaster.Domain.Repository;
using StreamMaster.Domain.Services;

namespace StreamMaster.Application.VideoStreamLinks.Commands;

[RequireAll]
public record AddVideoStreamToVideoStreamRequest(string ParentVideoStreamId, string ChildVideoStreamId, int? Rank) : IRequest { }

public class AddVideoStreamToVideoStreamRequestHandler(ILogger<AddVideoStreamToVideoStreamRequest> logger, IRepositoryWrapper repository, IMapper mapper, ISettingsService settingsService, IPublisher publisher, ISender sender, IHubContext<StreamMasterHub, IStreamMasterHub> hubContext, IMemoryCache memoryCache) : BaseMediatorRequestHandler(logger, repository, mapper, settingsService, publisher, sender, hubContext, memoryCache), IRequestHandler<AddVideoStreamToVideoStreamRequest>
{
    public async Task Handle(AddVideoStreamToVideoStreamRequest request, CancellationToken cancellationToken)
    {
        await Repository.VideoStreamLink.AddVideoStreamTodVideoStream(request.ParentVideoStreamId, request.ChildVideoStreamId, request.Rank, cancellationToken).ConfigureAwait(false);
        await HubContext.Clients.All.VideoStreamLinksRefresh([request.ChildVideoStreamId]);
    }
}