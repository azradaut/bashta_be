namespace Bashta.Core.DTOs;

public class NotificationResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}