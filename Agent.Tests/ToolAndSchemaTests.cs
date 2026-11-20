using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agent.Llm;
using Agent.Tools;
using Xunit;

namespace Agent.Tests
{
    /// <summary>Tests for typed tool arguments, schemas and the tool registry's safety nets.</summary>
    public class ToolAndSchemaTests
    {
        private static JsonElement Args(string json)
        {
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                return document.RootElement.Clone();
            }
        }

        [Fact]
        public void GetInt_AcceptsNumbersAndText()
        {
            Assert.Equal(5, Args("{\"n\":5}").GetInt("n"));
            Assert.Equal(5, Args("{\"n\":\"5\"}").GetInt("n"));
            Assert.Equal(5, Args("{\"n\":5.0}").GetInt("n"));
            Assert.Null(Args("{\"n\":5.5}").GetInt("n"));
            Assert.Null(Args("{\"n\":\"five\"}").GetInt("n"));
            Assert.Null(Args("{}").GetInt("n"));
            Assert.Null(Args("{\"n\":null}").GetInt("n"));
        }

        [Fact]
        public void GetNumber_AcceptsNumbersAndText()
        {
            Assert.Equal(12.5, Args("{\"p\":12.5}").GetNumber("p"));
            Assert.Equal(12.5, Args("{\"p\":\"12.5\"}").GetNumber("p"));
            Assert.Null(Args("{\"p\":\"cheap\"}").GetNumber("p"));
        }

        [Fact]
        public void GetBool_AcceptsTheUsualSpellings()
        {
            Assert.True(Args("{\"b\":true}").GetBool("b"));
            Assert.True(Args("{\"b\":\"yes\"}").GetBool("b"));
            Assert.False(Args("{\"b\":\"false\"}").GetBool("b"));
            Assert.False(Args("{\"b\":0}").GetBool("b"));
            Assert.Null(Args("{\"b\":\"maybe\"}").GetBool("b"));
        }

        [Fact]
        public void GetString_ReadsTextAndNumbers()
        {
            Assert.Equal("Hats", Args("{\"t\":\"Hats\"}").GetString("t"));
            Assert.Equal("42", Args("{\"t\":42}").GetString("t"));
            Assert.Null(Args("[]").GetString("t"));
        }

        [Fact]
        public void Schema_WritesTypesEnumsAndRequired()
        {
            JsonObject schema = Schema.Object(
                new ToolParameter("type", "Product type", false, ToolParameterTypes.String, new List<string> { "Boards", "Hats" }),
                new ToolParameter("maxPrice", "Highest price", false, ToolParameterTypes.Number),
                new ToolParameter("productId", "Product id", true, ToolParameterTypes.Integer));

            Assert.Equal("number", (string)schema["properties"]["maxPrice"]["type"]);
            Assert.Equal("integer", (string)schema["properties"]["productId"]["type"]);
            Assert.Equal("Hats", (string)schema["properties"]["type"]["enum"][1]);
            Assert.Equal("productId", (string)schema["required"][0]);
            Assert.Single(schema["required"].AsArray());
        }

        [Fact]
        public async Task Registry_TimesOutASlowTool()
        {
            ToolRegistry registry = new ToolRegistry(new List<ITool> { new SlowTool() }, TimeSpan.FromMilliseconds(100));

            ToolExecutionResult result = await registry.ExecuteAsync(new ToolCall("1", "slow", "{}"), new AgentRequestContext(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("timed out", result.Output);
        }

        [Fact]
        public async Task Registry_TurnsExceptionsAndBadJsonIntoErrorText()
        {
            ToolRegistry registry = new ToolRegistry(new List<ITool> { new BrokenTool(), new EchoTool() });

            ToolExecutionResult broken = await registry.ExecuteAsync(new ToolCall("1", "broken", "{}"), new AgentRequestContext(), CancellationToken.None);
            ToolExecutionResult badJson = await registry.ExecuteAsync(new ToolCall("2", "echo", "{not json"), new AgentRequestContext(), CancellationToken.None);

            Assert.Equal("Error: the tool failed (InvalidOperationException).", broken.Output);
            Assert.Equal("Error: the tool arguments were not valid JSON.", badJson.Output);
        }

        [Fact]
        public void Registry_RejectsTwoToolsWithTheSameName()
        {
            Assert.Throws<InvalidOperationException>(() => new ToolRegistry(new List<ITool> { new EchoTool(), new EchoTool() }));
        }
    }
}
