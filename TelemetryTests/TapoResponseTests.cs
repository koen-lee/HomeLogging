using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using TelemetryToRaven.Tapo;
using Xunit;

namespace TelemetryTests
{
    public class TapoResponseTests
    {


        [Fact]
        public void ParseSample()
        {
            var undertest = File.ReadAllText("tapo.json");
            Assert.NotEmpty(undertest);
            var parsed = JsonSerializer.Deserialize<JsonObject>(undertest);
            var response = new TapoUtilResponse(parsed);
            Assert.NotNull(response.Ip);
            Assert.InRange(response.MonthEnergy, 0, 100);
        }
    }
}