namespace TONEX_CHAN.TONEX_CHANData;

public record Room(
    string Code,
    string Version,
    int Count,
    LangName LangId,
    string ServerName,
    string PlayerName,
    string BuildId = "")
{
    public DateTime Time { get; set; }
    
    public bool SendEnd { get; set; }
};