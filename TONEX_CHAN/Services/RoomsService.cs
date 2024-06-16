using System.Diagnostics.CodeAnalysis;
using TONEX_CHAN.TONEX_CHANData;
using TONEX_CHAN.Config;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text.RegularExpressions;

namespace TONEX_CHAN.Services;

public class RoomsService(EnvironmentalTextService service, IOptions<ServerConfig> config, ILogger<RoomsService> logger)
{
    public readonly List<Room> _Rooms = [];

    public bool CheckRoom(Room room)
    {
        var r = _Rooms.FirstOrDefault(n => n.Code == room.Code);
        if (r == null)
            return true;

        var time = DateTime.Now - r.Time;
        if (time.TotalMinutes < config.Value.RoomInterval)
        {
            return false;
        }
        
        
        return true;
    }
    
    public bool TryPareRoom(string text,[MaybeNullWhen(false)] out Room room)
    {
        room = null;
        try
        {
            var strings = text.Split('|');
            logger.LogInformation("Checking the length of the input string array");
            if (strings.Length < 6)
            {
                logger.LogWarning("Input string does not contain enough parts");
                return false;
            }

            logger.LogInformation("Extracting room code");
            var code = strings[0];
            string codePattern = @"^[a-zA-Z]{4}$|^[a-zA-Z]{6}$";
            if (!Regex.IsMatch(code, codePattern))
            {
                Log.Logger.Warning("Invalid room code: must be 6 letters");
                return false;
            }

            room = _Rooms.FirstOrDefault(n => n.Code == code);
            logger.LogInformation("Extracting room version");
            var version = strings[1];

            logger.LogInformation("Validating room version");
            bool isValidVersion = IsVersionValid(version);
            if (!isValidVersion)
            {
                logger.LogWarning("Invalid version format");
                return false;
            }

            logger.LogInformation("Extracting player count");
            string input = strings[2];

            logger.LogInformation("Validating player count");
            string pattern = @"^\d+$";
            if (!Regex.IsMatch(input, pattern))
            {
                logger.LogWarning("Player count is not a valid number");
                return false;
            }

            if (!int.TryParse(input, out var count))
            {
                logger.LogWarning("Failed to parse player count as integer");
                return false;
            }

            logger.LogInformation("Player count parsed successfully: {Count}", count);

            logger.LogInformation("Extracting room language");
            if (!Enum.TryParse<LangName>(strings[3], out var langId))
            {
                logger.LogWarning("Invalid language ID");
                return false;
            }

            logger.LogInformation("Extracting server name");
            var serverName = strings[4];

            logger.LogInformation("Extracting host player name");
            var playName = strings[5];
            var has = true;
            if (room == null)
            {
                room = new Room(code, version, count, langId, serverName, playName);
                logger.LogInformation("Successfully created and added room");
                has = false;
            }
            
            if (!CheckRoom(room))
            {
                logger.LogInformation("CheckRoom:False");
                return false;
            }
            
            room.Time = DateTime.Now;
            if (!has)
                _Rooms.Add(room);
            
            service
                .Update("RoomCode", code)
                .Update("Version", version)
                .Update("PlayerCount", count.ToString())
                .Update("Language", (config.Value.LangTextToCN ? lang_forZh[langId] : lang_forEn[langId])!)
                .Update("ServerName", serverName)
                .Update("PlayerName", playName);
        }
        catch (Exception e)
        {
            logger.LogWarning(e.ToString());
            return false;
        }
        
        return true;
    }
    

    static bool IsVersionValid(string version)
    {
        string pattern = @"^\d+\.\d+_\d{8}_(Debug|Canary|Dev|Preview)(?:_\d+)?$";
        Regex regex = new(pattern);
        return regex.IsMatch(version);
    }


    public static readonly Dictionary<LangName, string> lang_forZh = new()
    {
        { LangName.English, "英语" },
    { LangName.Latam, "拉丁美洲" },
    { LangName.Brazilian, "巴西" },
    { LangName.Portuguese, "葡萄牙" },
    { LangName.Korean, "韩语" },
    { LangName.Russian, "俄语" },
    { LangName.Dutch, "荷兰语" },
    { LangName.Filipino, "菲律宾语" },
    { LangName.French, "法语" },
    { LangName.German, "德语" },
    { LangName.Italian, "意大利语" },
    { LangName.Japanese, "日语" },
    { LangName.Spanish, "西班牙语" },
    { LangName.SChinese, "简体中文" },
    { LangName.TChinese, "繁体中文" },
    { LangName.Irish, "爱尔兰语" }
    };
    public static readonly Dictionary<LangName, string> lang_forEn = new()
    {

    { LangName.English, "English" },
    { LangName.Latam, "Latam" },
    { LangName.Brazilian, "Brazilian" },
    { LangName.Portuguese, "Portuguese" },
    { LangName.Korean, "Korean" },
    { LangName.Russian, "Russian" },
    { LangName.Dutch, "Dutch" },
    { LangName.Filipino, "Filipino" },
    { LangName.French, "French" },
    { LangName.German, "German" },
    { LangName.Italian, "Italian" },
    { LangName.Japanese, "Japanese" },
    { LangName.Spanish, "Spanish" },
    { LangName.SChinese, "SChinese" },
    { LangName.TChinese, "TChinese" },
    { LangName.Irish, "Irish" }

};

}