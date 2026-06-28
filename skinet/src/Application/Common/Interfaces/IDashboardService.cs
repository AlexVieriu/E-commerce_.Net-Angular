namespace Application.Common.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}
