using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;

namespace RconPlugin
{
    [Category("rcon")]
    public class Commands : CommandModule
    {
        public RconPlugin Plugin => (RconPlugin)Context.Plugin;

        [Command("setpassword")]
        public void SetPassword(string password)
        {
            var hash = Md5Util.HashString(password);
            Plugin.Config.PassHash = Convert.ToBase64String(hash);
            Plugin.Config.Save();
            Context.Respond("Password set, reload the RCON server to apply changes.");
        }

        [Command("reload", "Restart the RCON server with updated configuration.")]
        public void Reload()
        {
            Plugin.Reload();
        }
    }
}
