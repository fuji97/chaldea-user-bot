using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Telegram.Bot.Advanced.DbContexts;

namespace ChaldeaBot.DbContext
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

        /// <summary>
        /// Empty constructor used by EF Core.
        /// </summary>
        public RegisteredChat() { }

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="masterId">Master ID (Foreign Key)</param>
        /// <param name="chatId">Chat ID (Foreign Key)</param>
        public RegisteredChat(int masterId, long chatId) {
            MasterId = masterId;
            ChatId = chatId;
        }
    }
}
