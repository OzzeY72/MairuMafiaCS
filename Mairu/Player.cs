using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mairu
{
    public class Player
    {
        public Discord.IUser user;
        public Roles role;
        public bool isAlive;
        public int voteforkill;
        public int voteforkick;
        public SocketSlashCommand msg;
        public int votes;
        public bool voted;
        public Player(IUser user,bool isAlive,SocketSlashCommand msg)
        {
            this.user = user;
            this.isAlive = isAlive;
            this.msg = msg;
            voteforkick = 0;
            voteforkill = 0;
        }
    }
}
