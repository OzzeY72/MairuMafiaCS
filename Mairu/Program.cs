using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Channels;
using Mairu;
using System.ComponentModel.Design;

class Config
{
    public string token;
    public string prefix;
}
public class Program
{
    private DiscordSocketClient _client;
    public string prefix = "!";
    public Mafia game;
    public static Task Main(string[] args) => new Program().MainAsync();
    public async Task MainAsync()
    {
        var _config = new DiscordSocketConfig
        {
            MessageCacheSize = 100,
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
        };
        _client = new DiscordSocketClient(_config);
        var json = File.ReadAllText("config.json");
        Config config = JsonConvert.DeserializeObject<Config>(json);
        var token = config.token;

        prefix = config.prefix;

        await _client.LoginAsync(TokenType.Bot, token);

        await _client.StartAsync();
        //_client.SlashCommandExecuted += SlashCommandHandler;

        //_client.MessageReceived += HandlerMessage;

        _client.Ready += Client_Ready;
        _client.SlashCommandExecuted += SlashCommandHandler;
        _client.ButtonExecuted += MafiaButtonHandler;
        _client.Ready += () =>
        {
            Console.WriteLine("Bot is connected!");
            return Task.CompletedTask;
        };

        await Task.Delay(-1);
    }

    private async Task HandlerMessage(SocketMessage msg)
    {
        var message = msg as SocketUserMessage;
        Console.WriteLine(msg.Author.Username + "\t" + msg.Content);
        int argPos = 0;
        if (!(message.HasCharPrefix('!', ref argPos) ||
            message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
            message.Author.IsBot)
            return;
        string[] args = msg.Content.Split(' ');
        if (args[0] == prefix + "ping") await msg.Channel.SendMessageAsync("Pong! " + msg.Author.Mention);
        //else if (args[0] == prefix + "mplay") await HandleMafiaPlay(msg);
        //else if (args[0] == prefix + "mstart") await HandleMafiaStart(msg);
            //case "!action": await HandlePictureCommand(msg); break;
            //case "!suicide": await HandlePictureCommand(msg);break;
            //case "!help": await HandleHelpCommand(msg); break;
            //case "!mjoin": await HandleJoinCommand(msg); break;
            //case "!mplay": await HandlePlayCommand(msg); break;
    }
    private async Task MafiaButtonHandler(SocketMessageComponent component)
    {
        Console.WriteLine("Handle button");
        var bttnid = component.Data.CustomId.Split("$$");
        Console.WriteLine(bttnid[0] +"\t"+ bttnid[1]);
        if (bttnid[1] == "Mafia")
        {
            foreach (var player in game.players)
            {
                if (bttnid[0] == player.user.Id.ToString())
                {
                    Console.WriteLine("Пизда тебе - " + player.user.Username);
                    game.lastmsg = "Был убит - " + player.user.Mention;
                    player.isAlive = false;
                    game.alive_count--;
                    game.isSkip = true;
                }
            }
        }
        else
        {
            if (!game.getPlayerById(component.User.Id).voted)
            {
                game.getPlayerById(Convert.ToUInt64(bttnid[0])).votes++;
                game.getPlayerById(component.User.Id).voted = true;
                game.voted++;
            }
            if (game.voted == game.alive_count)
            {
                foreach (var player in game.players)
                {
                    if (player.votes >= game.voted/2)
                    {
                        Console.WriteLine("Посадили на бутылку - " + player.user.Username);
                        var msg = player.user.Mention;
                        player.isAlive = false;
                        game.alive_count--;
                        await Task.Delay(new TimeSpan(0, 0, 5)).ContinueWith(async o =>
                        {
                            await game.main_msg.ModifyAsync(x => x.Embed.Value.ToEmbedBuilder().AddField("Казнили - ", msg));
                        });
                        game.isSkip = true;
                    }
                    game.votationpassed = true;
                    foreach (var iplayer in game.players)
                    {
                        player.votes = 0;
                        player.voted = false;
                        game.voted = 0;
                    }
                }
            }
        }
    }
    private async Task HandleMafiaPlay(SocketSlashCommand msg)
    {
        Console.WriteLine(msg.User.Username);
        if (game != null && game.isRunning != GameStatus.Stopped)
        {
            foreach (var player in game.players)
            {
                if (msg.User.Id == player.user.Id)
                {
                    await msg.RespondAsync("Вы уже участвуете");
                    return;
                }
            }
        }
        if (game == null)
        {
            game = new Mafia();
            game.isRunning = GameStatus.Waiting;
            game.mafiacount = 1;
            game.players = new List<Player>();
        }
        game.players.Add(new Player(msg.User, true,msg));
        game.alive_count = game.players.Count;
        game.saveMsg(msg);
        await msg.DeferAsync(ephemeral: true);
        string struser = "";
        foreach(var player in game.players)
        {
            struser += player.user.Username + ", ";
        }
        var embed = new EmbedBuilder()
            .AddField("Готовые игроки",struser)
            .WithTitle("Вы принимаете участие " + game.players.Count + " игроков готово");
        foreach (var player in game.msgs){
            await player.ModifyOriginalResponseAsync(x => { x.Embed = embed.Build();});
        }
    }
    private async Task HandleMafiaStart(SocketSlashCommand msg)
    {
        Console.WriteLine("Handle start");
        game.channel = msg.Channel;
        if (game.alive_count >= 2)
        {
            game.channel = msg.Channel;
            game.main_msg = await game.channel.SendMessageAsync("До начала игры - 5");
            await game.startGame();
        }
        else 
        {   
            await game.channel.SendMessageAsync("Не хватает игроков");
        }
    }

    private async Task SlashCommandHandler(SocketSlashCommand command)
    {
        switch(command.Data.Name)
        {
            case "join": await HandleMafiaPlay(command); break;
            case "start": await HandleMafiaStart(command); break;
        }
    }
    private async Task Client_Ready()
    {
        Console.WriteLine("Client Ready");   
        ulong guildId = 1092152738311315587;
        var guild = _client.GetGuild(guildId);

        var guildCommand = new Discord.SlashCommandBuilder()
            .WithName("join")
            .WithDescription("Join game");
        var guildCommand2 = new Discord.SlashCommandBuilder()
            .WithName("start")
            .WithDescription("Start game");
        try
        {
            await guild.CreateApplicationCommandAsync(guildCommand.Build());
            await guild.CreateApplicationCommandAsync(guildCommand2.Build());
        }
        catch (HttpException exception)
        {
            var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
            Console.WriteLine(json);
        }
    }
    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }
}