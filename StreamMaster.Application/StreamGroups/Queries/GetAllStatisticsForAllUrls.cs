﻿using StreamMaster.Application.Common.Models;

namespace StreamMaster.Application.StreamGroups.Queries;

public record GetAllStatisticsForAllUrls() : IRequest<List<StreamStatisticsResult>>;

internal class GetAllStatisticsForAllUrlsHandler(IStreamStatisticService streamStatisticService) : IRequestHandler<GetAllStatisticsForAllUrls, List<StreamStatisticsResult>>
{
    public async Task<List<StreamStatisticsResult>> Handle(GetAllStatisticsForAllUrls request, CancellationToken cancellationToken)
    {
        return await streamStatisticService.GetAllStatisticsForAllUrls();
    }
}
