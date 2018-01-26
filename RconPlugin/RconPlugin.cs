using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.Multiplayer;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Session;
using Torch.Commands;
using Torch.Server;

namespace RconPlugin
{
    public class RconPlugin : TorchPluginBase
    {
        //Use this to write messages to the Torch log.
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private RconServer _server;
        private new TorchServer Torch;
        public Config Config { get; private set; }

        /// <inheritdoc />
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Torch = (TorchServer)torch;
            //Your init code here, the game is not initialized at this point.

            Reload();
        }

        public void Reload()
        {
            _server?.Dispose();
            Config = Config.Load(Path.Combine(StoragePath, "Rcon.cfg"));

            if (string.IsNullOrEmpty(Config.PassHash))
            {
                Log.Warn("No password set! Until a password is set with `!rcon setpassword` all connections will be rejected.");
            }

            _server = new RconServer(new IPEndPoint(Config.IP, Config.Port))
            {
                CommandHandler = HandleCommand
            };
            _server.SetPassword(Convert.FromBase64String(Config.PassHash));
            _server.Start();
        }

        private string HandleCommand(string message)
        {
            switch (message)
            {
                case "start":
                    Torch.Start();
                    return "Server started.";
                case "stop":
                    Torch.Stop();
                    return "Server stopped.";
                case "status":
                    return $"Version: {Torch.TorchVersion}\nState: {Torch.State:G}\nSim: {Torch.SimulationRatio:0.00}";
                default:
                {
                    var prefix = "!";
                    if (Torch.State != ServerState.Running)
                        return "Error: Server is not running.";
                    if (message.StartsWith(prefix))
                    {
                        var commandManager = Torch.CurrentSession.Managers.GetManager<CommandManager>();
                        var command = commandManager.Commands.GetCommand(message.Substring(prefix.Length), out string argText);
                        if (command == null)
                            return "Command not found.";
                        var splitArgs = Regex.Matches(argText, "(\"[^\"]+\"|\\S+)").Cast<Match>().Select(x => x.ToString().Replace("\"", "")).ToList();
                        
                        var context = new RconCommandContext(Torch, command.Plugin, Sync.MyId, argText, splitArgs);
                        if (command.TryInvoke(context))
                            return context.Response.ToString();
                        else
                            return "Error executing command.";
                    }

                    return "Unknown command.";
                }
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            //Unload your plugin here.
            _server.Dispose();
        }
    }
}
