using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using XsgTwitterBot.Configuration;
using XsgTwitterBot.Node;
using XsgTwitterBot.Node.Impl;
using Xunit;

namespace XsgTwitterBot.Tests
{
    public class NodeApiTests
    {
        private readonly INodeApi _nodeApi;

        public NodeApiTests()
        {
            _nodeApi = new NodeApi(new NodeOptions
            {
                AuthUserName = "demzet",
                AuthUserPassword = "pwd1",
                Url = "http://localhost:8232",
            });
        }

        [Fact]
        public async Task GetInfo_Should_ReturnData()
        {
            var result = await _nodeApi.GetInfoAsync();

            Assert.NotNull(result);
        }
 
        [Fact]
        public async Task SendToAddress_Should_Success()
        {
            for (int i = 0; i < 10; i++)
            {
                var txid = await _nodeApi.SendToAddressAsync("s1hheqmvMe29QQrVid8wZaKys5DpW13wTG7", 1m);
                Console.WriteLine(txid);
            }
            
        }

        [Theory]
        [InlineData("s1dLfyVfgUo535Sv7GuTEkoztX3uxJS9mJ1", true)]
        [InlineData("s1dLfyVfgUo535Sv7GuTEkoztX3ux", false)]
        public async Task ValidateAddress_Should_ReturnResult(string address, bool isValid)
        {
            var result = await _nodeApi.ValidateAddressAsync(address);

            Assert.True(result.Result.IsValid == isValid);
        }


        [Fact]
        public async Task Validate_Address()
        {
            var text = @"New twitter promotion campaign from #xsg #snowgem (s1e3V62JUJiXy7mqBgMeX22NXk2V4kGu555) project.
                You can earn 25 XSG per day by posting a single tweet 
                @krafman_inc
                 .";
            
            var output = Regex.Replace(text, @"\r\n?|\n|\(|\)", " ");
        }
    }


}
