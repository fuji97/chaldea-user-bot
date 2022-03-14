using ChaldeaBot.DbContext;
using Microsoft.Extensions.Caching.Memory;
using Rayshift;
using Telegram.Bot.Advanced.Controller;

namespace ChaldeaBot.TelegramController {
    public class Controller : TelegramController<MasterContext> {
        private readonly ILogger _logger;
        protected readonly IMemoryCache Cache;
        protected readonly IConfiguration Configuration;
        protected readonly IRayshiftClient RayshiftClient;

        public Controller(ILogger logger, IMemoryCache cache, IConfiguration configuration, IRayshiftClient rayshiftClient) {
            _logger = logger;
            Cache = cache;
            Configuration = configuration;
            RayshiftClient = rayshiftClient;
        }

        public Controller() {
        }
    }

    public static class ConversationState {
        public const string? Idle = null;
        public const string? Nome = "ChaldeabotController_Nome";
        public const string? FriendCode = "ChaldeabotController_FriendCode";
        public const string? Server = "ChaldeabotController_Server";
        public const string? SupportList = "ChaldeabotController_SupportList";
        public const string? ServantList = "ChaldeabotController_ServantList";
        public const string? UpdatingSupportList = "ChaldeabotController_UpdatingSupportList";
        public const string? UpdatingServantList = "ChaldeabotController_UpdatingServantList";
        public const string? WaitingRayshift = "ChaldeabotController_WaitingRayshift";
    }
}
