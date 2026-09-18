using System;
using NUnit.Framework;
using Server.Security;

namespace Server.Test;

[TestFixture]
public class WebhookUrlBuilderTests {
    [Test]
    public void Build_TrailingSlashBase_ProducesExpectedUrl() {
        var url = WebhookUrlBuilder.Build(new Uri("https://host/"), "/telegram/", "chaldeabot");

        Assert.That(url.AbsoluteUri, Is.EqualTo("https://host/telegram/chaldeabot"));
    }

    [Test]
    public void Build_BaseWithoutTrailingSlash_StillProducesExpectedUrl() {
        var url = WebhookUrlBuilder.Build(new Uri("https://host"), "/telegram/", "chaldeabot");

        Assert.That(url.AbsoluteUri, Is.EqualTo("https://host/telegram/chaldeabot"));
    }

    [Test]
    public void Build_EndpointNeedingEscaping_IsEscaped() {
        var url = WebhookUrlBuilder.Build(new Uri("https://host/"), "/telegram/", "cha ldea bot");

        Assert.That(url.AbsoluteUri, Is.EqualTo("https://host/telegram/cha%20ldea%20bot"));
    }
}
