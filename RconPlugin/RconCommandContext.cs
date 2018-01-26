using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.API;
using Torch.API.Plugins;
using Torch.Commands;

namespace RconPlugin
{
    public class RconCommandContext : CommandContext
    {
        /// <inheritdoc />
        public RconCommandContext(ITorchBase torch, ITorchPlugin plugin, ulong steamIdSender, string rawArgs = null, List<string> args = null) : base(torch, plugin, steamIdSender, rawArgs, args) { }

        public StringBuilder Response { get; } = new StringBuilder();

        /// <inheritdoc />
        public override void Respond(string message, string sender = "Server", string font = "Blue")
        {
            Response.AppendLine(message);
        }
    }
}
