using System;
using System.Linq;
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

        private static IMessageChannel m_BuyPoolChannel = null;
        private static IUserMessage m_BuyPoolMessage = null;

        private static Tuple<IUser, UserData> m_Escrow = null;
        private static Dictionary<IUser, UserData> m_Participants = new Dictionary<IUser, UserData>();

        private static bool m_IsPoolLocked = false;


        private static void Main(string[] args) => new AXSGroupBuy().RunBotAsync().GetAwaiter().GetResult();

        public async Task RunBotAsync()
        {
            m_Client = new DiscordSocketClient();
            m_CommandService = new CommandService();
            m_ServiceProvider = new ServiceCollection().AddSingleton(m_Client).AddSingleton(m_CommandService).BuildServiceProvider();

            string token = ConfigParser.ParsedConfigData.Value.botToken;

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
            ConfigData configData = ConfigParser.ParsedConfigData.Value;
            m_BuyPoolMessage.ModifyAsync(property => property.Content = GetFormattedMessage(configData.poolMessage));
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

        public static Task ClearPool()
        {
            Task.Run(async () =>
            {
                ClearPoolValues();
                await UpdateMessage();
                await m_BuyPoolMessage.RemoveAllReactionsAsync();
                await AddPoolBotReactions();
            });

            return Task.CompletedTask;
        }

        public static Task RemoveEscrow()
        {
            Task.Run(async () =>
            {
                ConfigData configData = ConfigParser.ParsedConfigData.Value;

                await m_BuyPoolMessage.RemoveReactionAsync(new Emoji(configData.addEscrowEmoji), m_Escrow.Item1);
                await m_BuyPoolMessage.RemoveAllReactionsForEmoteAsync(new Emoji(configData.lockEmoji));

                m_Escrow = null;
                m_IsPoolLocked = false;
                await UpdateMessage();

                m_Participants.Clear();
                await UpdateMessage();
                await m_BuyPoolMessage.RemoveAllReactionsAsync();
                await AddPoolBotReactions();
            });

            return Task.CompletedTask;
        }

        public static Task AddPoolBotReactions()
        {
            Task.Run(async () =>
            {
                ConfigData configData = ConfigParser.ParsedConfigData.Value;
                await m_BuyPoolMessage.AddReactionAsync(new Emoji(configData.addParticipantEmoji));
                await m_BuyPoolMessage.AddReactionAsync(new Emoji(configData.addEscrowEmoji));
            });

            return Task.CompletedTask;
        }

        private static void ClearPoolValues()
        {
            m_Escrow = null;
            m_IsPoolLocked = false;
            m_Participants.Clear();
        }

        private Task HandleCommandReceivedAsync(SocketMessage arg)
        {
            Task.Run(async () => 
            {
                SocketUserMessage message = arg as SocketUserMessage;
                if (message != null)
                {
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
                }
            });

            return Task.CompletedTask;
        }

        private Task OnReactionAdded(Cacheable<IUserMessage, ulong> _Message, ISocketMessageChannel _Channel, SocketReaction _Reaction)
        {
            Task.Run(async () =>
            {
                IUser user = _Channel.GetUserAsync(_Reaction.UserId).Result;

                if (user.Id != m_Client.CurrentUser.Id)
                {
                    if (m_BuyPoolMessage != null && _Reaction.MessageId == m_BuyPoolMessage.Id)
                    {
                        ConfigData configData = ConfigParser.ParsedConfigData.Value;
                        bool isSuccess = false;

                        if (_Reaction.Emote.Name == configData.addParticipantEmoji)
                        {
                            isSuccess = OnParticipantAddClicked(user).Result;
                        }
                        else if (_Reaction.Emote.Name == configData.addEscrowEmoji)
                        {
                            isSuccess = OnEscrowAddClicked(user).Result;
                        }
                        else if (_Reaction.Emote.Name == configData.lockEmoji)
                        {
                            isSuccess = OnPoolLockClicked(user).Result;
                        }
                        else if (_Reaction.Emote.Name == configData.closeEmoji)
                        {
                            isSuccess = OnCloseClicked(user).Result;
                        }

                        if (!isSuccess)
                        {
                            IMessage message = _Channel.GetMessageAsync(_Reaction.MessageId).Result;
                            await message.RemoveReactionAsync(_Reaction.Emote, user);
                        }
                    }
                }
            });

            return Task.CompletedTask;
        }

        private async Task<bool> OnParticipantAddClicked(IUser _User)
        {
            Task<bool> taskResult = Task<bool>.Run(async () =>
            {
                if (!m_IsPoolLocked && !Participants.ContainsKey(_User))
                {
                    Participants.Add(_User, new UserData() { m_RoninAddress = "" });
                    await UpdateMessage();
                    return true;
                }

                return false;
            });

            return await taskResult;
        }

        private async Task<bool> OnEscrowAddClicked(IUser _User)
        {
            Task<bool> taskResult = Task<bool>.Run(async () =>
            {
                if (!m_IsPoolLocked && m_Escrow == null)
                {
                    m_Escrow = new Tuple<IUser, UserData>(_User, new UserData() { m_RoninAddress = "" });
                    await UpdateMessage();

                    ConfigData configData = ConfigParser.ParsedConfigData.Value;
                    await m_BuyPoolMessage.AddReactionAsync(new Emoji(configData.lockEmoji));

                    return true;
                }

                return false;
            });

            return await taskResult;
        }

        private async Task<bool> OnPoolLockClicked(IUser _User)
        {
            Task<bool> taskResult = Task<bool>.Run(async () =>
            {
                if (!m_IsPoolLocked && m_Escrow != null && _User.Id == m_Escrow.Item1.Id)
                {
                    ConfigData configData = ConfigParser.ParsedConfigData.Value;
                    m_IsPoolLocked = true;
                    foreach (KeyValuePair<IUser, UserData> participant in m_Participants)
                    {
                        await participant.Key.SendMessageAsync(GetFormattedMessage(configData.poolLockedPrivateMessage));
                    }

                    await m_BuyPoolMessage.AddReactionAsync(new Emoji(configData.closeEmoji));

                    return true;
                }

                return false;
            });

            return await taskResult;
        }

        private async Task<bool> OnCloseClicked(IUser _User)
        {
            Task<bool> taskResult = Task<bool>.Run(async () =>
            {
                if (m_IsPoolLocked && m_Escrow != null && m_Escrow.Item1.Id == _User.Id)
                {
                    ConfigData configData = ConfigParser.ParsedConfigData.Value;

                    await m_BuyPoolMessage.DeleteAsync();
                    m_BuyPoolMessage = null;

                    await m_BuyPoolChannel.SendMessageAsync(GetFormattedMessage(configData.poolSuccessMessage));

                    ClearPoolValues();

                    return true;
                }

                return false;
            });

            return await taskResult;
        }

        private Task OnReactionRemoved(Cacheable<IUserMessage, ulong> _Message, ISocketMessageChannel _Channel, SocketReaction _Reaction)
        {
            Task.Run(async () =>
            {
                IUser user = _Channel.GetUserAsync(_Reaction.UserId).Result;

                if (user.Id != m_Client.CurrentUser.Id && m_BuyPoolMessage != null && _Reaction.MessageId == m_BuyPoolMessage.Id)
                {
                    ConfigData configData = ConfigParser.ParsedConfigData.Value;

                    if (_Reaction.Emote.Name == configData.addParticipantEmoji)
                    {
                        await OnParticipantRemoveClicked(user);
                    }
                    else if (_Reaction.Emote.Name == configData.addEscrowEmoji)
                    {
                        await OnEscrowRemoveClicked(user);
                    }
                }
            });

            return Task.CompletedTask;
        }

        private async Task OnParticipantRemoveClicked(IUser _User)
        {
            await Task.Run(async () =>
            {
                if (m_Participants.ContainsKey(_User))
                {
                    m_Participants.Remove(_User);
                    await UpdateMessage();
                }
            });
        }

        private async Task OnEscrowRemoveClicked(IUser _User)
        {
            await Task.Run(async () =>
            {
                if (m_Escrow != null && m_Escrow.Item1.Id == _User.Id)
                {
                    m_Escrow = null;
                    m_IsPoolLocked = false;
                    await UpdateMessage();

                    if (m_BuyPoolMessage != null)
                    {
                        ConfigData configData = ConfigParser.ParsedConfigData.Value;
                        await m_BuyPoolMessage.RemoveAllReactionsForEmoteAsync(new Emoji(configData.lockEmoji));
                        await m_BuyPoolMessage.RemoveAllReactionsForEmoteAsync(new Emoji(configData.closeEmoji));
                    }
                }
            });
        }


        public static string GetFormattedMessage(string _Message)
        {
            string escrowMention = m_Escrow == null ? "" : "<@" + m_Escrow.Item1.Id + ">";

            _Message = _Message
                .Replace("{Escrow}", escrowMention)
                .Replace("{Participants}", GetParticipantsListString("- ", "\r"))
                .Replace("{AddParticipantEmoji}", ConfigParser.ParsedConfigData.Value.addParticipantEmoji)
                .Replace("{AddEscrowEmoji}", ConfigParser.ParsedConfigData.Value.addEscrowEmoji)
                .Replace("{LockEmoji}", ConfigParser.ParsedConfigData.Value.lockEmoji);

            return _Message;
        }

        private static string GetParticipantsListString(string _Prefix, string _Separator)
        {
            string participantsList = "";
            for (int i = 0; i < m_Participants.Count; i++)
            {
                KeyValuePair<IUser, UserData> participant = m_Participants.ElementAt(i);
                participantsList += _Prefix + "<@" + participant.Key.Id + ">";
                if (participant.Value.m_RoninAddress.Length > 0)
                {
                    participantsList += " (" + GetCensoredRoninAddress(participant.Value.m_RoninAddress) + ")";
                }

                if (i < m_Participants.Count - 1)
                {
                    participantsList += _Separator;
                }
            }

            return participantsList;
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


        public static IMessageChannel BuyPoolChannel
        {
            get => m_BuyPoolChannel;
            set
            {
                m_BuyPoolChannel = value;
            }
        }
        public static IUserMessage BuyPoolMessage
        {
            get => m_BuyPoolMessage;
            set
            {
                m_BuyPoolMessage = value;
            }
        }
        public static DiscordSocketClient Client { get => m_Client; }
        public static Dictionary<IUser, UserData> Participants { get => m_Participants; }
    }
}