using System.Threading.Tasks;
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
    }
}