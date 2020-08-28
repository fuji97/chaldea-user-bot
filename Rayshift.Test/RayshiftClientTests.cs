using System.Linq;
using System.Net.Http;
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
            
            _client = new RayshiftClient(_apiKey);
        }

        [Test]
        public async Task TestDecks() {
            var result = await _client.GetSupportDeck(Region, _friendCode);
            
            Assert.IsNotNull(result);
            await CheckImages(result);
        }
        
        [Test]
        public async Task TestLookup() {
            var result = await _client.RequestSupportLookup(Region, _friendCode, async response => {
                Assert.IsNotNull(response);
                await CheckImages(response);
            });
            
            Assert.True(result);
        }

        private async Task CheckImages(ApiResponse apiResponse) {
            Assert.IsNotNull(apiResponse.Response);
            using (var client = new HttpClient()) {
                await CheckImage(client, apiResponse.Response.SupportList(SupportListType.Normal));
                await CheckImage(client, apiResponse.Response.SupportList(SupportListType.Event));
                await CheckImage(client, apiResponse.Response.SupportList(SupportListType.Both));
            }
        }

        private async Task CheckImage(HttpClient client, string url) {
            var response = await client.GetAsync(url);
            Assert.True(response.IsSuccessStatusCode);
            Assert.True(response.Content.Headers.ContentType.ToString() == "image/png");
        }
    }
}