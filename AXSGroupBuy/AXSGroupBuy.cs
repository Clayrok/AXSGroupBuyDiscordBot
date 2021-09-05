using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace AXSGroupBuy
{
    public struct UserData
    {
        public string m_RoninAddress;
    }

    public class AXSGroupBuy
    {
        private static DiscordSocketClient m_Client = null;
        private CommandService m_CommandService = null;
        private IServiceProvider m_ServiceProvider = null;

        private static RestTextChannel m_BuyPoolChannel = null;

        private static RestUserMessage m_BuyPoolMessage = null;
        private static string m_BuyPoolMessageBaseText = "";

        private static Tuple<IUser, UserData> m_Escrow = null;
        private static Dictionary<IUser, UserData> m_Participants = new Dictionary<IUser, UserData>();

        private static string m_ParticipantEmoji = "✅";
        private static string m_EscrowEmoji = "☑️";
        private static string m_LockEmoji = "🔒";

        private static bool m_IsPoolLocked = false;
        private static string m_PoolLockedBaseText = "";


        private static void Main(string[] args) => new AXSGroupBuy().RunBotAsync().GetAwaiter().GetResult();

        public async Task RunBotAsync()
        {
            m_BuyPoolMessageBaseText = "====================\r**AXS Buy Pool**\r__Escrow: __ {0}\r\r__Candidates: __\r{1}\r\r" + ParticipantEmoji + " : Participant\r\r" + EscrowEmoji + " : Escrow\r====================";
            m_PoolLockedBaseText = "The AXS buying pool has been locked, please contact your escrow (<@{0}>) to start the transaction.";

            m_Client = new DiscordSocketClient();
            m_CommandService = new CommandService();
            m_ServiceProvider = new ServiceCollection().AddSingleton(m_Client).AddSingleton(m_CommandService).BuildServiceProvider();

            string token = "ODgyODE0MzQ0Njc1NTkwMTc0.YTA27Q.0hVWjCnVNW2OMLGmaWmDBySKXyU";

            m_Client.Log += ClientLog;
            m_Client.ReactionAdded += OnReactionAdded;
            m_Client.ReactionRemoved += OnReactionRemoved;

            await RegisterCommandsAsync();
            await m_Client.LoginAsync(TokenType.Bot, token);
            await m_Client.StartAsync();
            await Task.Delay(-1);
        }

        public static Task UpdateMessage()
        {
            Task.Run(async () => { await m_BuyPoolMessage.ModifyAsync(property => property.Content = GetFormattedBuyPoolMessage()); });
            return Task.CompletedTask;
        }

        private Task ClientLog(LogMessage arg)
        {
            Console.WriteLine(arg);
            return Task.CompletedTask;
        }

        public Task RegisterCommandsAsync()
        {
            Task.Run(async () => 
            {
                m_Client.MessageReceived += HandleCommandReceivedAsync;
                await m_CommandService.AddModulesAsync(Assembly.GetEntryAssembly(), m_ServiceProvider);
            });

            return Task.CompletedTask;
        }

        private Task HandleCommandReceivedAsync(SocketMessage arg)
        {
            Task.Run(async () => 
            {
                SocketUserMessage message = arg as SocketUserMessage;
                SocketCommandContext context = new SocketCommandContext(m_Client, message);

                if (message.Author.IsBot)
                {
                    return;
                }

                int argPos = 0;
                if (message.HasStringPrefix("!", ref argPos))
                {
                    IResult result = await m_CommandService.ExecuteAsync(context, argPos, m_ServiceProvider);
                    if (!result.IsSuccess)
                    {
                        Console.WriteLine(result.ErrorReason);
                    }
                }
            });

            return Task.CompletedTask;
        }


        private Task OnReactionAdded(Cacheable<IUserMessage, ulong> _Message, ISocketMessageChannel _Channel, SocketReaction _Reaction)
        {
            Task.Run(async () =>
            {
                IUser user = _Channel.GetUserAsync(_Reaction.UserId).Result;

                if (user.Id != m_Client.CurrentUser.Id && !m_IsPoolLocked)
                {
                    if (m_BuyPoolMessage != null && _Reaction.MessageId == m_BuyPoolMessage.Id)
                    {
                        if (_Reaction.Emote.Name == ParticipantEmoji)
                        {
                            await OnParticipantAddClicked(user);
                        }
                        else if (_Reaction.Emote.Name == EscrowEmoji)
                        {
                            await OnEscrowAddClicked(user);
                        }
                        else if (_Reaction.Emote.Name == LockEmoji)
                        {
                            await OnPoolLockClicked(user);
                        }
                        else
                        {
                            IMessage message = _Channel.GetMessageAsync(_Reaction.MessageId).Result;
                            await message.RemoveReactionAsync(_Reaction.Emote, user);
                        }
                    }
                    else
                    {
                        IMessage message = _Channel.GetMessageAsync(_Reaction.MessageId).Result;
                        await message.RemoveReactionAsync(_Reaction.Emote, user);
                    }
                }
            });

            return Task.CompletedTask;
        }

        private Task OnParticipantAddClicked(IUser _User)
        {
            Task.Run(async () =>
            {
                if (!Participants.ContainsKey(_User))
                {
                    Participants.Add(_User, new UserData() { m_RoninAddress = "" });
                    await UpdateMessage();
                }
            });

            return Task.CompletedTask;
        }

        private Task OnEscrowAddClicked(IUser _User)
        {
            Task.Run(async () =>
            {
                if (m_Escrow == null)
                {
                    m_Escrow = new Tuple<IUser, UserData>(_User, new UserData() { m_RoninAddress = "" });
                    await UpdateMessage();

                    if (m_BuyPoolMessage != null)
                    {
                        await m_BuyPoolMessage.AddReactionAsync(new Emoji(AXSGroupBuy.LockEmoji));
                    }
                }
            });

            return Task.CompletedTask;
        }

        private Task OnPoolLockClicked(IUser _User)
        {
            Task.Run(async () =>
            {
                if (m_Escrow != null && _User.Id == m_Escrow.Item1.Id)
                {
                    m_IsPoolLocked = true;
                    foreach (KeyValuePair<IUser, UserData> participant in m_Participants)
                    {
                        await participant.Key.SendMessageAsync(GetFormattedPoolLockedMessage());
                    }
                }
            });

            return Task.CompletedTask;
        }

        private Task OnReactionRemoved(Cacheable<IUserMessage, ulong> _Message, ISocketMessageChannel _Channel, SocketReaction _Reaction)
        {
            Task.Run(async () =>
            {
                IUser user = _Channel.GetUserAsync(_Reaction.UserId).Result;

                if (user.Id != m_Client.CurrentUser.Id && m_BuyPoolMessage != null && _Reaction.MessageId == m_BuyPoolMessage.Id)
                {
                    if (_Reaction.Emote.Name == ParticipantEmoji)
                    {
                        await OnParticipantRemoveClicked(user);
                    }
                    else if (_Reaction.Emote.Name == EscrowEmoji)
                    {
                        await OnEscrowRemoveClicked(user);
                    }
                }
            });

            return Task.CompletedTask;
        }

        private Task OnParticipantRemoveClicked(IUser _User)
        {
            Task.Run(async () =>
            {
                if (m_Participants.ContainsKey(_User))
                {
                    m_Participants.Remove(_User);
                    await UpdateMessage();
                }
            });

            return Task.CompletedTask;
        }

        private Task OnEscrowRemoveClicked(IUser _User)
        {
            Task.Run(async () =>
            {
                if (m_Escrow != null && m_Escrow.Item1.Id == _User.Id)
                {
                    m_Escrow = null;
                    m_IsPoolLocked = false;
                    await UpdateMessage();

                    if (m_BuyPoolMessage != null)
                    {
                        await m_BuyPoolMessage.RemoveAllReactionsForEmoteAsync(new Emoji(LockEmoji));
                    }
                }
            });

            return Task.CompletedTask;
        }


        public static string GetFormattedBuyPoolMessage()
        {
            string escrowMention = "";
            if (m_Escrow != null)
            {
                escrowMention = "<@" + m_Escrow.Item1.Id + ">" + " (" + GetCensoredRoninAddress(m_Escrow.Item2.m_RoninAddress) + ")";
            }

            string participentsList = "";
            foreach (KeyValuePair<IUser, UserData> participent in m_Participants)
            {
                participentsList += "- <@" + participent.Key.Id + ">" + " (" + GetCensoredRoninAddress(participent.Value.m_RoninAddress) + ")\r";
            }

            string result = string.Format(m_BuyPoolMessageBaseText, escrowMention, participentsList);
            return result;
        }

        public static string GetFormattedPoolLockedMessage()
        {
            if (m_Escrow != null)
            {
                return string.Format(m_PoolLockedBaseText, m_Escrow.Item1.Id);
            }
            else
            {
                return "";
            }
        }

        private static string GetCensoredRoninAddress(string _RoninAddress)
        {
            if (_RoninAddress.StartsWith("ronin:"))
            {
                string result = "ronin:";

                for (int i = 6; i < _RoninAddress.Length; i++)
                {
                    if (i >= 9 && i < _RoninAddress.Length - 5)
                    {
                        result += "* ";
                    }
                    else
                    {
                        result += _RoninAddress[i];
                    }

                }

                return result;
            }

            return "";
        }


        public static RestTextChannel BuyPoolChannel
        {
            get => m_BuyPoolChannel;
            set
            {
                m_BuyPoolChannel = value;
            }
        }
        public static RestUserMessage BuyPoolMessage
        {
            get => m_BuyPoolMessage;
            set
            {
                m_BuyPoolMessage = value;
            }
        }
        public static string BuyPoolMessageText
        {
            get => BuyPoolMessageText;
            set
            {
                BuyPoolMessageText = value;
            }
        }
        public static DiscordSocketClient Client { get => m_Client; }
        public static Dictionary<IUser, UserData> Participants { get => m_Participants; }
        public static string ParticipantEmoji { get => m_ParticipantEmoji; }
        public static string EscrowEmoji { get => m_EscrowEmoji; }
        public static string LockEmoji { get => m_LockEmoji; }
    }
}