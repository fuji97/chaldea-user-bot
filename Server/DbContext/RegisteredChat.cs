using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Telegram.Bot.Advanced.DbContexts;

namespace Server.DbContext
{
    public class RegisteredChat {
        [Key]
        public int MasterId { get; set; }
        [Key]
        public long ChatId { get; set; }
        [ForeignKey("MasterId")]
        public Master Master { get; set; }
        [ForeignKey("ChatId")]
        public TelegramChat Chat { get; set; }

        public RegisteredChat() { }

        public RegisteredChat(int masterId, long chatId) {
            MasterId = masterId;
            ChatId = chatId;
        }
    }
}
