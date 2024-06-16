using TONEX_CHAN.Config;
using Microsoft.Extensions.Options;

namespace TONEX_CHAN.Services;

public class TONEX_CHANService
(
    IServiceProvider _serviceProvider,
    ILogger<TONEX_CHANService> logger,
    IOptions<ServerConfig> _options) : IHostedService
{
    private readonly ServerConfig _config = _options.Value;
    public async Task StartAsync(CancellationToken cancellationToken)
    {

        var services = _serviceProvider.GetServices<ITONEX_CHANService>();
        foreach (var service in services)
        {
            try
            {
                await service.StartAsync(_config, this, cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError($"Start Error {service.GetType().Name} {e}");
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var services = _serviceProvider.GetServices<ITONEX_CHANService>();
        foreach (var service in services)
        {
            try
            {
                await service.StopAsync();
            }
            catch (Exception e)
            {
                logger.LogError($"Stop Error {service.GetType().Name} {e}");
            }
        }
    }
}