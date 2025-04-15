using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;
using System.Text;

namespace WindowsGSM.Plugins
{
    public class Citadel : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.Citadel", // WindowsGSM.XXXX
            author = "raziel7893",
            description = "WindowsGSM plugin for supporting Citadel Dedicated Server",
            version = "1.0.0",
            url = "https://github.com/Raziel7893/WindowsGSM.Citadel", // Github repository link (Best practice) TODO
            color = "#34FFeb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "489650"; // Game server appId Steam

        // - Standard Constructor and properties
        public Citadel(ServerConfig serverData) : base(serverData) => base.serverData = serverData;

        // - Game server Fixed variables
        //public override string StartPath => "CitadelServer.exe"; // Game server start path
        public override string StartPath => "Citadel\\Binaries\\Win64\\CitadelServer-Win64-Shipping.exe";
        public string FullName = "Citadel Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation

        // - Game server default values
        public string Port = "7777"; // Default port

        public string Additional = "-nosteamclient -server -log"; // Additional server start parameter

        // TODO: Following options are not supported yet, as ther is no documentation of available options
        public string Maxplayers = "40"; // Default maxplayers        
        public string QueryPort = "27015"; // Default query port. This is the port specified in the Server Manager in the client UI to establish a server connection.
        // TODO: Unsupported option
        public string Defaultmap = "default"; // Default map name
        // TODO: Undisclosed method
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            string configDir = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, "Citadel\\Saved\\Config\\WindowsServer");
            string engineIni = Path.Combine(configDir, "engine.ini");
            string gameIni = Path.Combine(configDir, "game.ini");

            Directory.CreateDirectory(configDir);

            File.WriteAllText(engineIni, $"[url]\r\nPort={serverData.ServerPort}");

            File.WriteAllText(gameIni, $"[UWorks]\r\nSteamPort=8766\r\nConnectionPort={serverData.ServerPort}\r\nQueryPort={serverData.ServerQueryPort}\r\n \r\n[/script/citadel.remoteconsole]\r\nWebServerPort=8889\r\nWebServerUsername=admin\r\nWebServerPass=password\r\n \r\n[/script/citadel.socialmanager]\r\nPassword=citadel123\r\nWhiteList=OPTIONALSteamIdToWhitelist1\r\nWhiteList=OPTIONALSteamIdToWhitelist2\r\nAdminSteamIDs=OPTIONALSteamIdToPermaAdmin1\r\nAdminSteamIDs=OPTIONALSteamIdToPermaAdmin2\r\n \r\n[/script/citadel.citadelgameinstance]\r\nAutoLevelNewPlayers=-1\r\nRaidWeekDayStartTime=18\r\nRaidWeekDayEndTime=23\r\nRaidWeekendStartTime=11\r\nRaidWeekendEndTime=23\r\nbAllowWipes=false\r\nWorldCreationSettings=(ServerName=\"PrivateServer\",ServerWipeIntervalDays=0,ServerType=PVP,PlayerLimit=40,bPrivate=True,Password=\"\",UniqueOfficialServerKey=\"\",MaxStructuresPerRegion=10000,MaxStructuresPerServer=120000,ExperienceMultiplier=1.000000,KnowledgePointEarnedMultiplier=1.000000,CharacterPointEarnedMultiplier=1.000000,bUnlimitedResources=False,PlayerDamageMultiplier=1.000000,ArmorMultipler=1.000000,BaseManaRegen=1.000000,InventoryCapacityMultipler=1.000000,bInventoryWeightRestrictions=True,MagicFindMultiplier=1.000000,FlyingCostMultiplier=1.000000,FlyingSpeedMultiplier=1.000000,bFlyingIsFree=False,StructureDecayMultiplier=1.000000,bThronesDecay=True,ResourceCollectionMultiplier=1.000000,StructureDamageMultiplier=1.000000,bRespectNoBuildZones=True,MagicStructureManaRegenerationMultiplier=1.000000,MagicStructureManaConsumptionMultiplier=1.000000,TimeOfDayLock=Auto,NPCPopulationMultiplier=1.000000)\r\n \r\n[/script/citadel.globalnpcspawner]\r\nNPCSpawnCap=1000");
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath})";
                return null;
            }

            //Try gather a password from the gui
            var sb = new StringBuilder();
            sb.Append($"{serverData.ServerParam}");

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = false,
                    WorkingDirectory = ServerPath.GetServersServerFiles(serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = sb.ToString(),
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (serverData.EmbedConsole)
            {
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.CreateNoWindow = true;
                var serverConsole = new ServerConsole(serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
            }

            // Start Process
            try
            {
                p.Start();
                if (serverData.EmbedConsole)
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }
                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }
        }

        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                Functions.ServerConsole.SendWaitToMainWindow("^c");
                p.WaitForExit(2000);
                if (!p.HasExited)
                    p.Kill();
            });
        }
    }
}
