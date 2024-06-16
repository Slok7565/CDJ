using TONEX_CHAN.Config;

namespace TONEX_CHAN.Services;

public interface ITONEX_CHANService
{
    public ValueTask StartAsync(ServerConfig config, TONEX_CHANService cdjService, CancellationToken cancellationToken);
    public ValueTask StopAsync();
}