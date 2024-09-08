using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Rayshift.Models;

namespace Rayshift.Test {
    public class RayshiftClientTests {
        private const Region Region = Models.Region.Na;

        private RayshiftClient _client;
        private string _apiKey, _friendCode;
        
        [SetUp]
        public void Setup() {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<RayshiftClientTests>()
                .Build();
            
            _apiKey = configuration["ApiKey"];
            _friendCode = configuration["FriendCode"];
            
            Assert.That(_apiKey, Is.Not.Null, "Missing API Key");
            Assert.That(_friendCode, Is.Not.Null, "Missing Friend Code");
            
            _client = new RayshiftClient(_apiKey);
        }

        [Test]
        public async Task TestDecks() {
            var result = await _client.GetSupportDeck(Region, _friendCode);
            
            Assert.That(result, Is.Not.Null);
            await CheckImages(result);
        }
        
        [Test]
        public async Task TestLookup() {
            var result = await _client.RequestSupportLookup(Region, _friendCode, async response => {
                Assert.That(response, Is.Not.Null);
                await CheckImages(response);
            });
            
            Assert.That(result, Is.True);
        }

        private async Task CheckImages(ApiResponse apiResponse) {
            Assert.That(apiResponse.Response, Is.Not.Null);
            using (var client = new HttpClient()) {
                await CheckImage(client, apiResponse.Response.SupportList(Region));
            }
        }

        private async Task CheckImage(HttpClient client, string url) {
            Console.WriteLine($"URL: {url}");
            var response = await client.GetAsync(url);
            Assert.That(response.IsSuccessStatusCode, Is.True);
            Assert.That(Regex.IsMatch(response.Content.Headers.ContentType.ToString(), "image/(png|jpeg)"), Is.True);
        }
    }
}