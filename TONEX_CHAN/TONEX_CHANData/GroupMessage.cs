namespace TONEX_CHAN.TONEX_CHANData;

public class GroupMessage : SendMessage
{
    public override string message_type { get; } = "group";
    public string group_id { get; set; }
}