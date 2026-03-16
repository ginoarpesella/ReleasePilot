using MediatR;
using ReleasePilot.Application.Interfaces;
using ReleasePilot.Application.ReadModels;

namespace ReleasePilot.Application.Queries;

public record GetEnvironmentStatusQuery(string ApplicationName) : IRequest<EnvironmentStatusReadModel>;

public class GetEnvironmentStatusHandler : IRequestHandler<GetEnvironmentStatusQuery, EnvironmentStatusReadModel>
{
    private readonly IPromotionReadRepository _readRepository;

    public GetEnvironmentStatusHandler(IPromotionReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<EnvironmentStatusReadModel> Handle(GetEnvironmentStatusQuery request, CancellationToken cancellationToken)
    {
        return await _readRepository.GetEnvironmentStatusAsync(request.ApplicationName, cancellationToken);
    }
}
