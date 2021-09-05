using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Rest;
using Discord;

namespace AXSGroupBuy.Modules
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        [Command("createaxspool")]
        public async Task CreateAxsPoolChannel()
        {
            await Context.Message.Channel.DeleteMessageAsync(Context.Message);

            if (AXSGroupBuy.BuyPoolChannel == null)
            {
                IReadOnlyCollection<SocketCategoryChannel> guildCategoryChannels = Context.Guild.CategoryChannels;
                SocketCategoryChannel contextChannel = null;

                foreach (SocketCategoryChannel categoryChannel in guildCategoryChannels)
                {
                    foreach (SocketGuildChannel channel in categoryChannel.Channels)
                    {
                        if (channel.Id == Context.Channel.Id)
                        {
                            contextChannel = categoryChannel;
                            break;
                        }
                    }

                    if (contextChannel != null)
                    {
                        break;
                    }
                }

                if (contextChannel != null)
                {
                    RestTextChannel buyPoolChannel = await Context.Guild.CreateTextChannelAsync("AXS Buy Pool", property => property.CategoryId = contextChannel.Id);
                    await CreateAxsPoolMessage(buyPoolChannel);
                }
            }
        }

        private async Task CreateAxsPoolMessage(RestTextChannel _BuyPoolChannel)
        {
            RestUserMessage buyPoolMessage = await _BuyPoolChannel.SendMessageAsync(AXSGroupBuy.GetFormattedBuyPoolMessage());
            AXSGroupBuy.BuyPoolMessage = buyPoolMessage;
            await AXSGroupBuy.UpdateMessage();

            await buyPoolMessage.AddReactionAsync(new Emoji(AXSGroupBuy.ParticipantEmoji));
            await buyPoolMessage.AddReactionAsync(new Emoji(AXSGroupBuy.EscrowEmoji));
        }
    }
}