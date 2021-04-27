using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Xunit;

namespace ConfigurationTest
{
    public class TestLoadConfiguration
    {
        public ConfigurationBuilder ConfigBuilder { get; }       

        public TestLoadConfiguration()
        {
            ConfigBuilder = new ConfigurationBuilder();            
        }


        /* key name is case insensitive */

        
        [Fact]
        public void TestCreateConfiguration()
        {
            // source
            var source = new MemoryConfigurationSource()
            {
                InitialData = new Dictionary<string, string>()
                {
                    { "keya", "valuea" },
                    { "keyb", "valueb" },
                    { "keyc", "valuec" }
                }
            };
            
            // provider
            var provider = new MemoryConfigurationProvider(source);

            // configure
            var configure = new ConfigurationRoot(new List<IConfigurationProvider>() { provider });

            var keya = configure["keya"];
            Assert.Equal("valuea", keya);

            var keyb = configure.GetSection("keyb")?.Value;
            Assert.Equal("valueb", keyb);
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
        // "reload" method will load data from the providers again, and then trigger the (configuration root) change token
                                
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

            // register a reload handler to the change token of configuration
            object obj = null;            
            config.GetReloadToken().RegisterChangeCallback(c => obj = c, "changed");

            // reload to trigger the change token
            config.Reload();
            Assert.Equal("changed", (string)obj);

            ConfigBuilder.Sources.Clear();
        }


        // "on reload" method in configuration root will reload the provider info, 
        // but NOT trigger the provider change token!!!

        [Fact]
        public void TestReloadFromConfiguration()
        {
            /* register and build configuration root */

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


            /* register change token handler */

            // register change token handler for "configration provider"
            // (actually, nothing registered in configuration provider base)
            object objProvider = null;
            provider.GetReloadToken().RegisterChangeCallback(c => objProvider = c, "change from provider");

            // register change token handler for "configuration root"
            object objConfig = null;
            configuration.GetReloadToken().RegisterChangeCallback(c => objConfig = c, "change from config");


            // trigger configuration root change token
            configuration.Reload();

            // configuration provider handler not triggered!
            Assert.Null(objProvider);

            // configuration root handler triggered!
            Assert.Equal("change from config", objConfig);            
        }


        // but, configuration provider changes,
        // will trigger both itself change token handler and container configuration root change token handler

        [Fact]
        public void TestReloadFromProvider()
        {
            /* register and build configuration root */

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


            /* register change token handler */

            // register change token handler for "configration provider"
            // (actually, nothing registered in configuration provider base)
            object objProvider = null;
            provider.GetReloadToken().RegisterChangeCallback(c => objProvider = c, "change from provider");

            // register change token handler for "configuration root"
            object objConfig = null;
            configuration.GetReloadToken().RegisterChangeCallback(c => objConfig = c, "change from config");
            

            // trigger config provider "onReload" method
            // -by reflect as it is an internal method
            var reloadReflected = typeof(ConfigurationProvider).GetMethod("OnReload", BindingFlags.NonPublic | BindingFlags.Instance);
            reloadReflected.Invoke(provider, null);

            // configuration provider handler triggered!
            Assert.Equal("change from provider", objProvider);

            // configuration root handler triggered!
            Assert.Equal("change from config", objConfig);                        
        }


        // there is no change token mapping registered in "configuration provider base",
        // so in general configuration provider changing will NOT trigger the "configuration root" change token handler!
        // of cause derived configuration provider can change it!

        // in "file configuration provider base", a file watch token register with "load" file again action with a delay time (250ms default),
        // so the changing of file will be re-load after the delay, and trigger the "configuration root" change token handler.
        // register cusomized handler in "configuration root" to listener the changing!

        [Fact]
        public void TestReloadInFileConfiguration()
        {
            ConfigBuilder.Sources.Clear();

            // register configuration file and build configuration (root)
            var path = Path.Combine(Directory.GetCurrentDirectory(), "configReload.json");            
            ConfigBuilder.AddJsonFile(path, false, reloadOnChange: true);

            var config = ConfigBuilder.Build();


            // register configuration root change token handler
            object obj = null;
            config.GetReloadToken().RegisterChangeCallback(c => obj = c, "changed");
            
            // touch the file to trigger changing
            var content = File.ReadAllText(path);
            File.WriteAllText(path, content);
            
            // sleep thread, as provider token has a delay
            Thread.Sleep(500);


            Assert.Equal("changed", obj);            
        }
    }
}
