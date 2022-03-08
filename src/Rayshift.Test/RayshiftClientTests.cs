using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Rayshift.Models;

namespace Rayshift.Test {
    public class RayshiftClientTests {
        private const Region Region = Models.Region.Jp;

        private RayshiftClient _client = null!;
        private string _apiKey = null!, _friendCode = null!;
        
        [SetUp]
        public void Setup() {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<RayshiftClientTests>()
                .Build();
            
            var apiKey = configuration["ApiKey"];
            var friendCode = configuration["FriendCode"];

            Assert.IsNotNull(apiKey, "Missing API Key");
            Assert.IsNotNull(friendCode, "Missing Friend Code");
            
            _apiKey = apiKey!;
            _friendCode = friendCode!;
            
            _client = new RayshiftClient(_apiKey);
        }

        [Test]
        public async Task TestDecks() {
            var result = await _client.GetSupportDeck(Region, _friendCode);
            
            Assert.IsNotNull(result);
            await CheckImages(result!);
        }
        
        [Test]
        public async Task TestLookup() {
            var result = await _client.RequestSupportLookup(Region, _friendCode, async response => {
                Assert.IsNotNull(response);
                await CheckImages(response!);
            });
            
            Assert.True(result);
        }

        private async Task CheckImages(ApiResponse apiResponse) {
            Assert.IsNotNull(apiResponse.Response);
            using (var client = new HttpClient()) {
                await CheckImage(client, apiResponse.Response!.SupportList(Region));
            }
        }

        private async Task CheckImage(HttpClient client, string url) {
            var response = await client.GetAsync(url);
            Assert.True(response.IsSuccessStatusCode);
            Assert.True(Regex.IsMatch(response.Content.Headers.ContentType!.ToString(), "image/(png|jpeg)"));
        }
    }
}