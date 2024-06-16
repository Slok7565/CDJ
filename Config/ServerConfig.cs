namespace TONEX_CHAN.Config;

public class ServerConfig
{
    public const string Section = nameof(ServerConfig);
    
    public string Ip { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 25000;
    public string LogPath { get; set; } = "log_{time}.txt";
    public bool EAC = false;
    public bool LangTextToCN = true;
    public string SendText_zh { get; set; } = "房间号: {RoomCode}\n版本号: {Version}\n人数: {PlayerCount}\n语言: {Language}\n服务器: {ServerName}\n房主: {PlayerName}";
    public string SendText_en { get; set; } = "Room: {RoomCode}\nVersion: {Version}\nPlayer Count: {PlayerCount}\nLanguage: {Language}\nServer: {ServerName}\nHost: {PlayerName}";
    public string BotHttpUrl { get; set; } = "http://localhost:3000";
    public bool SendToGroup { get; set; } = false;
    public long QQID { get; set; }
    public int EACPort { get; set; } = 25250;
    public ulong DiscordChannelId = 1248050001506992139;
    public string DiscordToken = "MTI0ODg1ODIxOTg3OTczMTI1MQ.GIyaLR.auuVFG9p9RmI2SK5y8aHM8GKe9Adq02MP4WBc8";
    public int EACCount { get; set; } = 5;
    public string EACPath { get; set; } = "./EAC.txt";

    public string ReadPath = string.Empty;

    // 分钟
    public int RoomInterval = 30;

    // 分钟
    public int HostInterval = 5;
    
    // 秒
    public int MessageInterval = 30;

    /*public string ApiUrl = string.Empty;
    public int Time = 30;*/
}