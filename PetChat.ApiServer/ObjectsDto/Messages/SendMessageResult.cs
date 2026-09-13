namespace PetChat.ApiServer.ObjectsDto.Messages;

public class SendMessageResult(int messageId, int messageIndex, DateTime messageDate, string chatId)
{
    public int MessageId { get; set; } = messageId;
    public int MessageIndex { get; set; } = messageIndex;
    public DateTime MessageDate { get; set; } = messageDate;
    public string ChatId { get; set; } = chatId;
}