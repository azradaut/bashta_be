namespace Bashta.Core.Entities;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<PlantPot> PlantPots { get; set; } = new List<PlantPot>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}