using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace AXSGroupBuy
{
    public struct ConfigData
    {
        public string botToken;
        public string poolChannelId;
        public List<string> poolAdminRoleIds;
        public string botStatus;
        public string addParticipantEmoji;
        public string addEscrowEmoji;
        public string lockEmoji;
        public string closeEmoji;
        public string poolMessage;
        public string poolLockedPrivateMessage;
        public string poolSuccessMessage;
    }

    public class ConfigParser
    {
        private static ConfigData? m_ParsedConfigData = null;


        private static ConfigData? ParseConfigFile()
        {
            ConfigData result = new ConfigData();

            char platformSeparator = Path.DirectorySeparatorChar;
            result.botToken = File.ReadAllText("config" + platformSeparator + "botToken.cfg", Encoding.UTF8);
            result.poolChannelId = File.ReadAllText("config" + platformSeparator + "poolChannelId.cfg", Encoding.UTF8);
            result.poolAdminRoleIds = File.ReadAllLines("config" + platformSeparator + "poolAdminRoleIds.cfg", Encoding.UTF8).ToList();
            result.botStatus = File.ReadAllText("config" + platformSeparator + "botStatus.cfg", Encoding.UTF8);
            result.addParticipantEmoji = File.ReadAllText("config" + platformSeparator + "addParticipantEmoji.cfg", Encoding.UTF8);
            result.addEscrowEmoji = File.ReadAllText("config" + platformSeparator + "addEscrowEmoji.cfg", Encoding.UTF8);
            result.lockEmoji = File.ReadAllText("config" + platformSeparator + "lockEmoji.cfg", Encoding.UTF8);
            result.closeEmoji = File.ReadAllText("config" + platformSeparator + "closeEmoji.cfg", Encoding.UTF8);
            result.poolMessage = File.ReadAllText("config" + platformSeparator + "poolMessage.cfg", Encoding.UTF8);
            result.poolLockedPrivateMessage = File.ReadAllText("config" + platformSeparator + "poolLockedPrivateMessage.cfg", Encoding.UTF8);
            result.poolSuccessMessage = File.ReadAllText("config" + platformSeparator + "poolSuccessMessage.cfg", Encoding.UTF8);

            return result;
        }

        public static ConfigData? ParsedConfigData
        {
            get
            {
                if (m_ParsedConfigData == null)
                {
                    m_ParsedConfigData = ParseConfigFile();
                }

                return m_ParsedConfigData;
            }
        }
    }
}