using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Parameters;
using XsgTwitterBot.Configuration;
using XsgTwitterBot.Node.Impl;
using XsgTwitterBot.Services.Impl;
using Xunit;

namespace XsgTwitterBot.Tests
{
    public class MessageParserTests
    {
        [Fact]
        public async Task GetValidAddressAsync_Should_ReturnFirstValidAddress()
        {
            var text =
                @"I wouldlike to present you mmy 
                awesome coin s1dLfyVfgUo535Sv7GuTEkoztX3ux OR 
                s1dLfyVfgUo535S OR s3dLfyVfgUo535Sv7GuTEkoztX3uxJS9mJ1
                s1dLfyVfgUo535Sv7GuTEkoztX3uxJS9mJ1";

            var messageParser = new MessageParser(new NodeApi(new NodeOptions
            {
                AuthUserName = "demzet",
                AuthUserPassword = "pwd1",
                Url = "http://localhost:8232"
            }));

            var address = await messageParser.GetValidAddressAsync(text);

            Assert.Equal("s1dLfyVfgUo535Sv7GuTEkoztX3uxJS9mJ1", address);
        }

        AppSettings _appSettings = new AppSettings();
        
        [Fact]
        public async Task SearchTest()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("settings.json", true, false)
                .AddEnvironmentVariables()
                .Build();
            
            config.Bind(_appSettings);
            
            Auth.SetUserCredentials(
                _appSettings.TwitterSettings.ConsumerKey,
                _appSettings.TwitterSettings.ConsumerSecret,
                _appSettings.TwitterSettings.AccessToken,
                _appSettings.TwitterSettings.AccessTokenSecret);

           // var user = User.GetUserFromScreenName("@aph5nt");

           
            
            var response = Message.PublishMessage($"No problem, here ishkhkhj the reply!", 2858559033);
             
            
            
        }
    }
}

/*

Consumer API keys
M52reEhcoZLW42t0dCd0btWTJ (API key)NEW

SFp6OTBmEbEO7xrpyhDypx0xNxRkbNlX9svvYfQVGhyeKF0WME (API secret key)NEW

Access token & access token secret
1113344598863171584-AIEUe6ulwC0mGwNKbulwU9SiqmBBDI (Access token)

zQwkfdukZS2UxgHLcjz7s3TJckDTIrgeDr985JpWZfMiE (Access token secret)

16817054

*/