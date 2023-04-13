using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using System.Numerics;

namespace Mairu
{
    public enum Roles { 
        Mafia,
        Inhabitant,
        Sherif,
        Doctor
    }
    public enum GameStatus
    { 
        Stopped,
        Waiting,
        Playing
    }
    public class Mafia
    {
        public int timeforday = 30;
        public int timefornight = 30;

        public GameStatus isRunning = GameStatus.Stopped;
        public List<Player> players;
        public int alive_count;
        public List<SocketSlashCommand> msgs = new List<SocketSlashCommand>();
        public Discord.Rest.RestUserMessage main_msg;
        public int mafiacount;
        public int day_count = 0;
        public ISocketMessageChannel channel;
        public string lastmsg;
        public bool isSkip;
        public int voted = 0;
        public bool votationpassed = false;
        public void saveMsg(SocketSlashCommand msg)
        {
            msgs.Add(msg);
        }
        public void setRoles()
        {
            foreach (Player player in players)
            {
                player.role = Roles.Inhabitant;
            }
            List<int> last_index = new List<int>();
            for (int i = 0; i < mafiacount; i++)
            {
                int index = 0;
                Random rnd = new Random();
                index = rnd.Next(players.Count);
                while (last_index.Contains(index))
                {
                    rnd = new Random();
                    index = rnd.Next(players.Count);
                }
                last_index.Add(index);
                players[index].role = Roles.Mafia;
            }
        }
        public Player getPlayerById(ulong id)
        {
            foreach (Player player in players) {
                if (player.user.Id == id)
                    return player;
            }
            return null;
        }
        public async Task startGame()
        {
            if (isRunning == GameStatus.Stopped)
                return;
            setRoles();
            isRunning = GameStatus.Playing;
            foreach (SocketSlashCommand msg in msgs) {
                var str = "";
                var url = "";
                //Console.WriteLine(msg.User.Username);
                switch (getPlayerById(msg.User.Id).role)
                {
                    case Roles.Mafia: url = "https://cdn.smartytoys.ru/images/store/656_2.jpg";str = "Мафия";break;
                    case Roles.Inhabitant: url = "https://cdn.smartytoys.ru/images/store/656_3.jpg"; str = "Мирный житель";break;
                    default: break;
                }
                Console.WriteLine(str);
                var embed = new EmbedBuilder()
                    .WithImageUrl(url)
                    .WithTitle("Ваша роль - " + str);
                await msg.ModifyOriginalResponseAsync(x => { x.Embed = embed.Build();});
            }
            int seccount = 0;
            while (seccount != 5)
            {
                await Task.Delay(new TimeSpan(0, 0, 1)).ContinueWith(async o =>
                {
                    seccount++;
                    if (seccount == 5)
                        await startNewNight();
                    else
                        await main_msg.ModifyAsync(x => x.Content = "До начала игры - " + (5 - seccount));
                });
            }
            //await startNewNight();
            /*
            var builder = new ComponentBuilder();
            foreach (Player player in players)
            {
                if (player.role != Roles.Mafia)
                    builder.WithButton(player.user.Username,player.user.Id.ToString(),ButtonStyle.Danger);
            }
            await main_msg.ModifyOriginalResponseAsync(x => x.Components = builder.Build());*/
        }
        public async Task endGame(bool mafiawin)
        {
            isRunning = GameStatus.Stopped;
            string str = "";
            if (mafiawin)
            {
                str = "Мафия победила - ";
                foreach (var player in players)
                {
                    if(player.role == Roles.Mafia)
                        str += player.user.Username + " ";
                }
            }
            else 
            {
                str = "Мирные победили - ";
                foreach (var player in players)
                {
                    if (player.role == Roles.Inhabitant)
                        str += player.user.Username + " ";
                }
            }
            var embed = new EmbedBuilder()
                .AddField("Результат игры - ", str);
            await main_msg.DeleteAsync();
            main_msg = await channel.SendMessageAsync(embed: embed.Build());
        }
        public async Task startNewDay()
        {
            if (mafiacount >= alive_count - mafiacount)
            {
                await endGame(true);
            }
            else if (mafiacount == 0)
            {
                await endGame(false);
            }
            if (isRunning == GameStatus.Playing)
            {
                var embed = new EmbedBuilder()
                    .WithImageUrl("https://ubanks.com.ua/img/city/zaporizhzhya.jpg")
                    .AddField("День - " + day_count.ToString(), "Новости прошедшей ночи: " + lastmsg);
                await main_msg.DeleteAsync();
                main_msg = await channel.SendMessageAsync(embed: embed.Build());
                int seccount = 0;
                bool flag = false;

                var votembed = new EmbedBuilder()
                    .WithTitle("Выберете игрока, которого хотите выгнать");

                foreach (var iplayer in players)
                {
                    var builder = new ComponentBuilder();
                    foreach (var player in players)
                    {
                        if (player.isAlive && player != iplayer)
                        {
                            if (iplayer.role == Roles.Mafia && player.role != Roles.Mafia)
                                builder.WithButton(player.user.Username, player.user.Id.ToString() + "$$Vote", ButtonStyle.Danger);
                            else
                                builder.WithButton(player.user.Username, player.user.Id.ToString() + "$$Vote", ButtonStyle.Primary);
                        }
                    }
                    await iplayer.msg.ModifyOriginalResponseAsync(x => { x.Embed = votembed.Build(); x.Components = builder.Build(); }) ;
                }

                while (seccount != timeforday)
                {
                    await Task.Delay(new TimeSpan(0, 0, 1)).ContinueWith(async o =>
                    {
                        seccount++;
                        embed.WithTitle("Осталось до конца дня " + (timeforday - seccount) + " секунд");
                        var str = "";
                        foreach (var player in players)
                            if (player.isAlive)
                                str += player.user.Mention + " ";
                        if (!flag) embed.AddField("Остались в живых: ", str + alive_count); flag = true;
                        if (seccount == timeforday)
                            await startNewNight();
                        else
                            await main_msg.ModifyAsync(x => x.Embed = embed.Build());
                    });
                }
                if (!votationpassed)
                {
                    foreach (var player in players)
                    {
                        if (player.votes >= voted / 2)
                        {
                            Console.WriteLine("Посадили на бутылку - " + player.user.Username);
                            var msg = player.user.Mention;
                            player.isAlive = false;
                            alive_count--;
                            await Task.Delay(new TimeSpan(0, 0, 5)).ContinueWith(async o =>
                            {
                                await main_msg.ModifyAsync(x => x.Embed.Value.ToEmbedBuilder().AddField("Казнили - ", msg));
                            });
                        }
                        foreach (var iplayer in players)
                        {
                            player.votes = 0;
                            player.voted = false;
                            voted = 0;
                        }
                    }
                }
                foreach (var player in players)
                {
                    if (!player.isAlive)
                    {
                        var embed2 = new EmbedBuilder()
                            .WithTitle("Вас убили");
                        await player.msg.ModifyOriginalResponseAsync(x => x.Embed = embed2.Build());
                        //players.Remove(player);
                    }
                }
            }
        }
        public async Task startNewNight()
        { 
            day_count++;
            var embed = new EmbedBuilder()
                .WithImageUrl("https://gamerwall.pro/uploads/posts/2022-05/1652388782_3-gamerwall-pro-p-nochnoi-gorod-minimalizm-oboi-krasivo-4.jpg")
                .AddField("Ночь - " + day_count.ToString(), "Мафия делает выбор ...");
            await main_msg.DeleteAsync();
            main_msg = await channel.SendMessageAsync(embed:embed.Build());
            //main_msg.ModifyOriginalResponseAsync(x => x.Embed = embed.Build());
            foreach (SocketSlashCommand msg in msgs)
            {
                var embed2 = new EmbedBuilder();
                switch (getPlayerById(msg.User.Id).role)
                {
                    case Roles.Mafia: 
                        embed2.WithTitle("Вы Мафия, выберете жертву ");
                        var builder = new ComponentBuilder();
                        foreach (Player player in players)
                        {
                            if (player.role != Roles.Mafia && player.isAlive == true)
                                builder.WithButton(player.user.Username, player.user.Id.ToString()+"$$Mafia", ButtonStyle.Danger);
                        }
                        await msg.ModifyOriginalResponseAsync(x => { x.Embed = embed2.Build(); x.Components = builder.Build(); });
                        break;
                    case Roles.Inhabitant: embed2.WithTitle("Вы Мирный житель, спите "); await msg.ModifyOriginalResponseAsync(x => { x.Embed = embed2.Build(); }); break;
                    default: break;
                }
            }
            int seccount = 0;
            while (seccount != timefornight)
            {
                await Task.Delay(new TimeSpan(0, 0, 1)).ContinueWith(async o =>
                {
                    seccount++;
                    embed.WithTitle("Осталось до конца ночи " + (timefornight - seccount) + " секунд");
                    if(isSkip)
                    {
                        seccount = timeforday;
                        isSkip = false;
                    }
                    if (seccount == timefornight)
                        await startNewDay();
                    else
                        await main_msg.ModifyAsync(x => x.Embed = embed.Build());
                });
            }
        }
    }
}
