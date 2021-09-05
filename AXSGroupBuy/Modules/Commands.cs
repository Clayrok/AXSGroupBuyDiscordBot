using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Discord;

namespace AXSGroupBuy.Modules
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        [Command("buyaxs", RunMode = RunMode.Async)]
        public async Task CreatePool()
        {
            await Context.Message.Channel.DeleteMessageAsync(Context.Message);

            if (AXSGroupBuy.BuyPoolChannel == null)
            {
                FindPoolTextChannel();

                if (AXSGroupBuy.BuyPoolChannel != null)
                {
                    await CreateAxsPoolMessage();
                }
            }
        }

        [Command("clearpool", RunMode = RunMode.Async)]
        public async Task ClearPool()
        {
            await Context.Message.Channel.DeleteMessageAsync(Context.Message);

            if (HasUserPoolAdminRole(Context.User as SocketGuildUser))
            {
                await AXSGroupBuy.ClearPool();
            }
        }

        [Command("removeescrow", RunMode = RunMode.Async)]
        public async Task RemoveEscrow()
        {
            await Context.Message.Channel.DeleteMessageAsync(Context.Message);

            if (HasUserPoolAdminRole(Context.User as SocketGuildUser))
            {
                await AXSGroupBuy.RemoveEscrow();
            }
        }


        private void FindPoolTextChannel()
        {
            ConfigData configData = ConfigParser.ParsedConfigData.Value;
            AXSGroupBuy.BuyPoolChannel = Context.Guild.GetTextChannel(ulong.Parse(configData.poolChannelId));
        }

        private async Task CreateAxsPoolMessage()
        {
            if (AXSGroupBuy.BuyPoolChannel != null)
            {
                ConfigData configData = ConfigParser.ParsedConfigData.Value;

                IUserMessage buyPoolMessage = await AXSGroupBuy.BuyPoolChannel.SendMessageAsync(AXSGroupBuy.GetFormattedMessage(configData.poolMessage));
                AXSGroupBuy.BuyPoolMessage = buyPoolMessage;
                await AXSGroupBuy.UpdateMessage();
                await AXSGroupBuy.AddPoolBotReactions();
            }
        }

        private bool HasUserPoolAdminRole(SocketGuildUser _User)
        {
            if (_User != null)
            {
                ConfigData configData = ConfigParser.ParsedConfigData.Value;

                foreach (SocketRole role in _User.Roles)
                {
                    if (configData.poolAdminRoleIds.Contains(role.Id.ToString()))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}