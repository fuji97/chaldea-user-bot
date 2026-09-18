using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataScraper.Models;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;

namespace DataScraper;

public class Scraper {
    private const string BASE_URL = "http://fate-go.cirnopedia.org/";
    private const string ALL_SERVANTS_PATH = "servant_all.php";
        
    private HtmlDocument Document { get; set; }
        
    public Scraper() {
    }

    public async Task<List<ServantEntry>> GetAllServants() {
        var web = new HtmlWeb();

        Document = await web.LoadFromWebAsync(BASE_URL + ALL_SERVANTS_PATH);
        var rootNode = Document.QuerySelector("table#rounded-corner");
        var nodes = rootNode.SelectSingleNode("//tbody").GetChildElements();
        var servants = new List<ServantEntry>();

        foreach (var node in nodes) {
            servants.Add(CreateServantEntry(node));
        }

        return servants;
    }

    private ServantEntry CreateServantEntry(HtmlNode node) {
        var fields = node.GetChildElements().ToList();
        var servant = new ServantEntry(BASE_URL);

        servant.Id = node.Attributes["id"].Value;
        servant.Stars = GetServantRarity(fields[1].InnerText);
        servant.Name = GetServantName(fields[3].FirstChild);
        servant.Class = GetServantClass(fields[4].InnerText);
        servant.Cost = int.Parse(fields[5].InnerText);
        servant.BaseHp = int.Parse(fields[6].InnerText.Replace(",", ""));
        servant.BaseAttack = int.Parse(fields[7].InnerText.Replace(",", ""));
        servant.MaxHp = int.Parse(fields[8].FirstChild
            .InnerText.Replace(",", ""));
        servant.MaxAttack = int.Parse(fields[9].FirstChild
            .InnerText.Replace(",", ""));
        var cards = GetCards(fields[10].Attributes["sorttable_customkey"].Value);
        servant.Cards[AttackType.Quick] = cards.Item1;
        servant.Cards[AttackType.Arts] = cards.Item2;
        servant.Cards[AttackType.Buster] = cards.Item3;
        servant.NpType = GetNP(fields[11].Attributes["sorttable_customkey"].Value);
        servant.Skills = GetSkills(fields[12]);
        servant.Comments = fields[13].InnerText;
        servant.ServerLocation = node.Attributes["class"].Value == "US" ?
            ServerLocation.US : ServerLocation.JP;
            
        return servant;
    }

    private List<SkillEntry> GetSkills(HtmlNode htmlNode) {
        var nodes = htmlNode.GetChildElements().Where(x => x.Name == "a");
        var skills = new List<SkillEntry>();

        foreach (var node in nodes) {
            var skill = new SkillEntry();
            skill.Id = node.Attributes["href"].Value.Substring(10);
            var imgTag = node.ChildNodes.First(x => x.Name == "img");
            skill.ImageUrl = BASE_URL + imgTag.Attributes["src"].Value;
            skill.Name = imgTag.NextSibling.InnerText;
            skills.Add(skill);
        }
            
        return skills;
    }

    private ServantClass GetServantClass(string innerText) {
        switch (innerText.TrimEnd('\n')) {
            case "Saber":
                return ServantClass.Saber;
            case "Archer":
                return ServantClass.Archer;
            case "Lancer":
                return ServantClass.Lancer;
            case "Rider":
                return ServantClass.Rider;
            case "Assassin":
                return ServantClass.Assassin;
            case "Caster":
                return ServantClass.Caster;
            case "Berserker":
                return ServantClass.Berserker;
            case "Ruler":
                return ServantClass.Ruler;
            case "Shielder":
                return ServantClass.Shielder;
            case "Avenger":
                return ServantClass.Avenger;
            case "Moon Cancer":
                return ServantClass.MoonCancer;
            case "Alterego":
                return ServantClass.AlterEgo;
            case "Foreigner":
                return ServantClass.Foreigner;
            case "Beast I":
                return ServantClass.BeastI;
            case "Beast II":
                return ServantClass.BeastII;
            case "Beast III":
                return ServantClass.BeastIII;
            case "Grand Caster":
                return ServantClass.GrandCaster;
            default:
                return ServantClass.Unknown;
        }
    }

    private string GetServantName(HtmlNode textField) {
        return textField.ChildNodes.First(x => x.Name == "br").NextSibling.InnerText;
    }

    private Stars GetServantRarity(string innerText) {
        switch (innerText[0]) {
            case '0':
                return Stars.Zero;
            case '1':
                return Stars.One;
            case '2':
                return Stars.Two;
            case '3':
                return Stars.Three;
            case '4':
                return Stars.Four;
            case '5':
                return Stars.Five;
            default:
                throw new FormatException("Rarità non riconosciuta");
        }
    }

    private (int, int, int) GetCards(string code) {
        var quicks = 0;
        var arts = 0;
        var busters = 0;

        for (int i = 0; i < 5; i++) {
            switch (code[i*2+1]) {
                case '1':
                    quicks++;
                    break;
                case '2':
                    arts++;
                    break;
                case '3':
                    busters++;
                    break;
            }
        }
            
        return (quicks, arts, busters);
    }

    private AttackType GetNP(string code) {
        switch (code[1]) {
            case '1':
                return AttackType.Quick;
            case '2':
                return AttackType.Arts;
            case '3':
                return AttackType.Buster;
        }

        throw new FormatException("NP non valido");
    }
}