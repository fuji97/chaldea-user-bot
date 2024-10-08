﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Telegram.Bot.Advanced.DbContexts;

namespace Server.DbContext;

public class Master
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public long UserId { get; set; }
    public string Name { get; set; }
    public string FriendCode { get; set; }
    public string SupportList { get; set; }
    public string ServantList { get; set; }
    public MasterServer Server { get; set; }
    public MasterStatus Status { get; set; }
    public ICollection<RegisteredChat> RegisteredChats { get; set; }
    [ForeignKey("UserId")]
    public TelegramChat User { get; set; }
        
    public bool UseRayshift { get; set; }

    public Master() { }

    public Master(TelegramChat user, string name, string friendCode, MasterServer server, string support = null, string servant = null, bool useRayshift = false,
        MasterStatus status = MasterStatus.Enabled) {
        User = user;
        UserId = user.Id;
        Name = name;
        FriendCode = friendCode;
        Server = server;
        SupportList = support;
        ServantList = servant;
        Status = status;
        UseRayshift = useRayshift;
    }

    /*
    public void UpdateData(string key, string value)
    {
        var data = Data.FirstOrDefault(d => d.Key == key);
        if (data != null)
        {
            data.Value = value;
        }
        else
        {
            Data.Add(new Data(this, key, value));
        }
    }
    */
}

public enum MasterStatus {
    Enabled = 0,
    Disabled = 1
}

public enum MasterServer {
    Na = 0,
    Jp = 1
}