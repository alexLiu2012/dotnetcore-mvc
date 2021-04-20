using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace configAndOptionsTest
{
    public class TestLoadConfiguration
    {
        public ConfigurationBuilder ConfigBuilder { get; }       

        public TestLoadConfiguration()
        {
            ConfigBuilder = new ConfigurationBuilder();            
        }


        /* key name is case insensitive */

        // in configuration, data stored a dictionary,
        // get value by indexer will actually call the "dictionary.tryget()" method, 
        // so no exeption will be thrown even no related value found, -> return null!
        [Fact]
        public void TestGetByIndexer()
        {
            ConfigBuilder.AddInMemoryCollection(
                new Dictionary<string, string>()
                {
                    { "keya", "valuea" },
                    { "keyb", "valueb" },
                    { "keyc", "valuec" }
                });

            var config = ConfigBuilder.Build();

            // get an exist value
            var keya = config["keya"];
            Assert.Equal("valuea", keya);

            // get a non exist value, -> reutrn null
            var key = config["key"];
            Assert.Null(key);
        }


        // "get section" method will first create a new "configuration section", then bind value to it; 
        // even if there is no such section in the configuration, there will be always "configuration section" returned!
        [Fact]
        public void TestGetSection()
        {
            ConfigBuilder.AddInMemoryCollection(
                new Dictionary<string, string>()
                {
                    { "keya", "valuea" },
                    { "keyb", "valueb" },
                    { "keyc", "valuec" }
                });

            var config = ConfigBuilder.Build();

            // get an exist section
            var keya = config.GetSection("keya");
            Assert.Equal("valuea", keya.Value);

            // get a non exist section, -> return configurationSection[key, null]
            var key = config.GetSection("key");
            Assert.NotNull(key);
        }


        [Fact]
        public void TestInMemoryCollection()
        {            
            ConfigBuilder.AddInMemoryCollection(
                new Dictionary<string, string>()
                {
                    { "keya", "valuea" },
                    { "keyb", "valueb" },
                    { "keyc", "valuec" }
                });

            var config = ConfigBuilder.Build();           
            Assert.Equal("valuea", config["keya"]);

            ConfigBuilder.Sources.Clear();
        }

        [Fact]
        public void TestCommandLineArgs()
        {
            ConfigBuilder.AddCommandLine(
                new string[]
                {
                    "--keya=valuea",
                    "--keyb=valueb",
                    "--keyc=valuec"
                });

            var config = ConfigBuilder.Build();
            Assert.Equal("valuea", config["keya"]);

            ConfigBuilder.Sources.Clear();
        }

        [Fact]
        public void TestIniFile()
        {
            ConfigBuilder.AddIniFile(Path.Combine(Directory.GetCurrentDirectory(), "configs.ini"));

            var config = ConfigBuilder.Build();
            Assert.Equal("valuea", config["keya_ini"]);

            ConfigBuilder.Sources.Clear();
        }

        [Fact]
        public void TestJsonFile()
        {
            ConfigBuilder.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "configs.json"));

            var config = ConfigBuilder.Build();
            Assert.Equal("valuea", config["keya_json"]);

            ConfigBuilder.Sources.Clear();
        }

        [Fact]
        public void TestXmlFile()
        {
            ConfigBuilder.AddXmlFile(Path.Combine(Directory.GetCurrentDirectory(), "configs.xml"));

            var config = ConfigBuilder.Build();
            Assert.Equal("valuea", config["keya_xml"]);

            ConfigBuilder.Sources.Clear();
        }                
    }
}
