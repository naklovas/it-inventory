using System.Threading.Channels;
using MailRelay.Service.Options;
using Microsoft.Extensions.Options;

namespace MailRelay.Service.Services;

// Yeni kuyruklanan/tekrar denenecek mail id'lerini worker'lara aninda sinyal etmek icin
// kullanilan bellek ici kanal. Kalicilik saglamaz - o is dbo.MailQueue'nun kendisinde
// (MailQueueProcessor'daki periyodik DB taramasiyla) guvenceye alinir; kanal sadece
// polling gecikmesini ortadan kaldirir.
public sealed class MailQueueChannel
{
    private readonly Channel<long> _channel;

    public MailQueueChannel(IOptions<QueueOptions> options)
    {
        _channel = Channel.CreateBounded<long>(new BoundedChannelOptions(Math.Max(1, options.Value.ChannelCapacity))
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite,
        });
    }

    public ChannelReader<long> Reader => _channel.Reader;

    // Kanal doluysa (asiri yuk altinda) sessizce dusurulur; kayit kaybolmaz, sadece
    // isleme aninda degil bir sonraki DB taramasinda ele alinir.
    public void TryEnqueue(long id) => _channel.Writer.TryWrite(id);
}
