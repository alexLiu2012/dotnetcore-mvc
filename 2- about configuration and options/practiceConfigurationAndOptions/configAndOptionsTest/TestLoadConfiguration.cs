using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
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


        // a change token defined in the base "configuration root",
        // "reload" method will load data from the providers again, and then trigger the root token.on change,
        // so registered customized handler into the root.token will be invoked.
        //
        // besides, change registrations had been registered with <provider.token, root token raise (re-create token)>,
        // so any provider.token invoked, the root.token will be invoked as well
        
        [Fact]
        public void TestReload()
        {
            ConfigBuilder.AddInMemoryCollection(
                new Dictionary<string, string>()
                {
                    { "keya", "valuea" },
                    { "keyb", "valueb" },
                    { "keyc", "valuec" }
                });

            var config = ConfigBuilder.Build();

            object obj = null;

            // get reload token will always return a new change token
            // register customized handler to the token, so that handler will be triggered WHEN "configurtion root.reload()" called
            config.GetReloadToken().RegisterChangeCallback(c => obj = c, "changed");
            config.Reload();
            Assert.Equal("changed", (string)obj);

            ConfigBuilder.Sources.Clear();
        }


        [Fact]
        public void TestReloadByProvider()
        {
            // configuration source
            var source = new MemoryConfigurationSource()
            {
                InitialData = new Dictionary<string, string>()
                {
                    { "keya", "valuea" },
                    { "keyb", "valueb" },
                    { "keyc", "valuec" }
                }
            };
            // configuration provider
            var provider = new MemoryConfigurationProvider(source);
            // configuration root
            var configuration = new ConfigurationRoot(new List<IConfigurationProvider>() { provider });


            object obj = null;

            // register configration provider change handler
            provider.GetReloadToken().RegisterChangeCallback(c => { }, "");
            // register configuration root chagne handler
            configuration.GetReloadToken().RegisterChangeCallback(c => obj = c, "changed");

            // get configuration token -reload method by reflet
            var reloadReflected = typeof(ConfigurationProvider).GetMethod("OnReload", BindingFlags.NonPublic | BindingFlags.Instance);

            reloadReflected.Invoke(provider, null);
            Assert.Equal("changed", obj);
        }

        
        // another change registerations defined in the base "file configuration provider", 
        // <file change token, load provider (again)> was regiestered.
        // in "Load()" method, base "file configuration provider" token will be triggered        

        [Fact]
        public void TestReloadInFileConfiguration()
        {            
            var path = Path.Combine(Directory.GetCurrentDirectory(), "configReload.json");            

            ConfigBuilder.AddJsonFile(path, false, reloadOnChange: true);

            var config = ConfigBuilder.Build();

            object obj = null;
            config.GetReloadToken().RegisterChangeCallback(c => obj = c, "changed");
            
            var content = File.ReadAllText(path);
            File.WriteAllText(path, content);
            
            // sleep thread, as provider token has a delay
            Thread.Sleep(5000);
            Assert.Equal("changed", obj);            
        }
    }
}
