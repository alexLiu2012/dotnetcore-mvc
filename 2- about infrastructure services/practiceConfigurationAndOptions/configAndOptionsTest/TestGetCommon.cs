using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ConfigurationTest
{
    public class TestGetCommon
    {
        public ConfigurationBuilder ConfigBuilder { get; }

        public TestGetCommon()
        {
            ConfigBuilder = new ConfigurationBuilder();
        }


        [Fact]
        public void TestGetByIndexer()
        {
            ConfigBuilder.Sources.Clear();

            // register configuration source & build conifuration
            ConfigBuilder.AddInMemoryCollection(
                new Dictionary<string, string>()
                {
                    { "keya", "valuea" },
                    { "keyb", "valueb" },
                    { "keyc", "valuec" }
                });

            var config = ConfigBuilder.Build();

            // configuration props stored in an internal dictionary<string,string>,
            // indexer will actually call the "dictionary.tryget()" method, -> return null if no config found, no exception threw            

            // get exist value
            var keya = config["keya"];
            Assert.Equal("valuea", keya);

            // non exist value, -> reutrn null
            var key = config["key"];
            Assert.Null(key);
        }

        [Fact]
        public void TestGetSection()
        {
            ConfigBuilder.Sources.Clear();

            // register configuration info & build configuration
            ConfigBuilder.AddInMemoryCollection(
                new Dictionary<string, string>()
                {
                    { "keya", "valuea" },
                    { "keyb", "valueb" },
                    { "keyc", "valuec" }
                });

            var config = ConfigBuilder.Build();

            // "get section" method will directly create a new "configuration section" with specific key & value,
            // -> return empty instance, -> NOT null and no exception threw

            // exist section
            var keya = config.GetSection("keya");
            Assert.Equal("valuea", keya.Value);

            // non exist section, -> return configurationSection[key, null]
            var key = config.GetSection("key");
            Assert.NotNull(key);

            // section value is null
            Assert.Null(key.Value);

            // get required section (in .net 6), -> threw exception                 
        }


        [Fact]
        public void TestGetChildren()
        {
            ConfigBuilder.Sources.Clear();

            // register configuration info & build configuration
            ConfigBuilder.AddInMemoryCollection(
                new Dictionary<string, string>()
                {
                    { "keya", "valuea" },                    
                    { "keyb", "valueb" },
                    { "keyc", "valuec" }
                });

            var config = ConfigBuilder.Build();

            var children = config.GetChildren();
            Assert.Equal(3, children.Count());
        }


        [Fact]
        public void TestAsEnumerable()
        {
            ConfigBuilder.Sources.Clear();

            // register configuration info & build configuration
            ConfigBuilder.AddInMemoryCollection(
                new Dictionary<string, string>()
                {
                    { "keya", "valuea" },
                    { "keyb", "valueb" },
                    { "keyc", "valuec" }
                });

            var config = ConfigBuilder.Build();

            var children = config.AsEnumerable();
            Assert.Equal(3, children.Count());
        }


        [Fact]
        public void TestGetConnectionString()
        {
            ConfigBuilder.Sources.Clear();

            // register configuration info & build configuration
            // with connection strings
            ConfigBuilder.AddInMemoryCollection(
                new Dictionary<string, string>()
                {
                    { "connectionstrings:1st", "1st conn string" },
                    { "connectionstrings:2nd", "2nd conn string" },
                    { "keya", "valuea" },
                    { "keyb", "valueb" },
                    { "keyc", "valuec" }
                });

            var config1 = ConfigBuilder.Build();

            var connString1 = config1.GetConnectionString("1st");
            Assert.Equal("1st conn string", connString1);

            var connString2 = config1.GetConnectionString("2nd");
            Assert.Equal("2nd conn string", connString2);

            

            ConfigBuilder.Sources.Clear();

            // register configuration info & build configuration
            // without connetion strings
            ConfigBuilder.AddInMemoryCollection(
                new Dictionary<string, string>()
                {                    
                    { "keya", "valuea" },
                    { "keyb", "valueb" },
                    { "keyc", "valuec" }
                });

            var config2 = ConfigBuilder.Build();
            var connString = config2.GetConnectionString("key");
            Assert.Null(connString);
        }        
    }
}
