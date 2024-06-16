namespace TONEX_CHAN.TONEX_CHANData;

public class SendInfo(string message)
{
    public string Message = message;
    public List<(bool, long)> SendTargets = [];
}