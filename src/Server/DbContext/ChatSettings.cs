using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Telegram.Bot.Advanced.DbContexts;

namespace Server.DbContext;

public class ChatSettings {
    [Key, ForeignKey("TelegramChat")]
    public long Id { get; set; }
        
    public TelegramChat TelegramChat { get; set; }
        
    public bool ServantListNotifications { get; set; } = false;
        
    public bool SupportListNotifications { get; set; } = false;
}