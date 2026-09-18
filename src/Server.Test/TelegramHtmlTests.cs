using NUnit.Framework;
using Server.Services;

namespace Server.Test;

[TestFixture]
public class TelegramHtmlTests {
    [TestCase("&", "&amp;")]
    [TestCase("<", "&lt;")]
    [TestCase(">", "&gt;")]
    [TestCase("<b>x</b> & y", "&lt;b&gt;x&lt;/b&gt; &amp; y")]
    [TestCase(null, "")]
    [TestCase("", "")]
    [TestCase("plain text", "plain text")]
    public void Escape_MapsReservedCharactersExactlyOnce(string? input, string expected) {
        Assert.That(TelegramHtml.Escape(input!), Is.EqualTo(expected));
    }
}
