using System.Collections.Generic;
using System.Text;

namespace DataScraper.Models;

public class ServantEntry {
    public string Name { get; set; }
    public string Id { get; set; }
    public Stars Stars { get; set; }

    public string IconUrl {
        get {
            var str = Id.Substring(0,3) + "1";
            return baseUrl + $"icons/servant/servant_{str}.png";
        }
    }

    public string ImageUrl {
        get {
            var str = Id.Substring(0,3);
            return baseUrl + $"icons/servant_card/{str}1.jpg";
        }
    }

    public ServantClass Class { get; set; }
    public int Cost { get; set; }
    public int BaseHp { get; set; }
    public int MaxHp { get; set; }
    public int BaseAttack { get; set; }
    public int MaxAttack { get; set; }
    public Dictionary<AttackType, int> Cards { get; set; }
    public AttackType NpType { get; set; }
    public List<SkillEntry> Skills { get; set; }
    public string Comments { get; set; }
    public ServerLocation ServerLocation { get; set; }

    public string ServantUrl => baseUrl + "servant_profile.php?servant=" + Id;

    private string baseUrl;

    public ServantEntry(string baseUrl) : this() {
        this.baseUrl = baseUrl;
    }

    private ServantEntry() {
        Cards = new Dictionary<AttackType, int>();
        Cards[AttackType.Arts] = 0;
        Cards[AttackType.Quick] = 0;
        Cards[AttackType.Buster] = 0;
        Skills = new List<SkillEntry>();
    }

    public string GetDeckString() {
        StringBuilder deck = new StringBuilder();
        for (int i = 0; i < Cards[AttackType.Quick]; i++) {
            deck.Append('Q');
        }
        for (int i = 0; i < Cards[AttackType.Arts]; i++) {
            deck.Append('A');
        }
        for (int i = 0; i < Cards[AttackType.Buster]; i++) {
            deck.Append('B');
        }

        return deck.ToString();
    }
}