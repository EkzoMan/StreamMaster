﻿using StreamMaster.Domain.Common;
using StreamMaster.Domain.Repository;
using StreamMaster.Domain.Services;

namespace StreamMaster.Application.SchedulesDirect.Commands;

public record EPGSync() : IRequest<bool>;

public class SDSyncHandler(ISchedulesDirect schedulesDirect, ILogger<EPGSync> logger, IRepositoryWrapper repository, IMapper mapper, ISettingsService settingsService, IPublisher publisher, ISender sender, IHubContext<StreamMasterHub, IStreamMasterHub> hubContext, IMemoryCache memoryCache)
: BaseMediatorRequestHandler(logger, repository, mapper, settingsService, publisher, sender, hubContext, memoryCache), IRequestHandler<EPGSync, bool>
{
    public async Task<bool> Handle(EPGSync request, CancellationToken cancellationToken)
    {
        Setting setting = await GetSettingsAsync().ConfigureAwait(false);
        if (setting.SDSettings.SDEnabled)
        {
            if (await schedulesDirect.SDSync(0, cancellationToken).ConfigureAwait(false))
            {
                logger.LogInformation("Updated Schedules Direct");
                await HubContext.Clients.All.SchedulesDirectsRefresh();
            }
        }

        return true;
    }
}