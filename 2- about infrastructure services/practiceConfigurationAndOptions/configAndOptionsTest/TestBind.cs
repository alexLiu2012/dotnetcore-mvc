using ConfigureationAndOptions;
using Microsoft.Extensions.Configuration;
using System.IO;
using Xunit;

namespace ConfigurationTest
{
    public class TestBind
    {
        public ConfigurationBuilder ConfigBuilder { get; }
        public IConfiguration Config { get; }

        public ConfigurationBuilder ConfigBuilderRoot { get; }
        public IConfiguration ConfigRoot { get; }


        public TestBind()
        {
            ConfigBuilder = new ConfigurationBuilder();
            ConfigBuilder.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "configObject.json"));
            Config = ConfigBuilder.Build();

            ConfigBuilderRoot = new ConfigurationBuilder();
            ConfigBuilderRoot.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "configObjectWithRoot.json"));
            ConfigRoot = ConfigBuilderRoot.Build();
        }


        // "bind()" method will bind property of object from configuration value,
        // But not available for type which is convertable (same as "get value" method did)

        // a non-null object as result needs to past as a parameter,        
        // it will NOT override if bind failure (no related configuration exist)


        // null result object will bypass the bind process!
        [Fact]
        public void TestBindNullResult()
        {
            // exist key, -> bypass the bind process, -> null
            SubConfigList list = null;
            Config.Bind("sublist", list);
            Assert.Null(list);

            // non exist key, -> bypass the bind process, -> null
            string key = null;
            Config.Bind("key", key);
            Assert.Null(key);            
        }

        // int is convertable, cannot bind
        [Fact]
        public void TestBindInt()
        {            
            int keyInt = 0;

            // exist key, -> not bind for int (convertable), -> object (int) not changed
            Config.Bind("keyInt", keyInt);
            Assert.NotEqual(23, keyInt);
            Assert.Equal(0, keyInt);
        }


        // string is convertable, cannot bind
        [Fact]
        public void TestBindString()
        {            
            string keya = string.Empty;

            // exist key, -> not bind for string convertable), -> object (string) not changed
            Config.Bind("keya", keya);
            Assert.NotEqual("valuea", keya);
            Assert.Equal("", keya);
        }


        // list is binded
        [Fact]
        public void TestBindList()
        {
            var list = new SubConfigList()
            {
                "haha",
                "hehe"
            };
            
            // exist key, -> bind to listBind (result)
            var listBind = new SubConfigList();
            Config.Bind("SubList", listBind);
            Assert.Equal(list, listBind);            
        }


        // dictionary is binded
        [Fact]
        public void TestBindDictionary()
        {
            var dictionary = new SubConfigDictionary()
            {
                {"SubKeya","SubValuea" },
                {"SubKeyb","SubValueb" },
                {"SubKeyc","SubValuec" }
            };

            // exist key, -> bind to dictionary
            var dictionaryBind = new SubConfigDictionary();
            Config.Bind("subDictionary", dictionaryBind);
            Assert.Equal(dictionary, dictionaryBind);
        }


        // complex object is binded
        [Fact]
        public void TestBindComplex()
        {
            var config = new Config()
            {
                Keya = "valuea",
                Keyb = "valueb",
                Keyc = "valuec",

                SubList = new SubConfigList()
                {
                    "haha",
                    "hehe"
                },

                SubDictionary = new SubConfigDictionary()
                {
                    {"SubKeya","SubValuea" },
                    {"SubKeyb","SubValueb" },
                    {"SubKeyc","SubValuec" }
                }
            };

            // exist key, -> bind to complex type
            var configBind = new Config();
            ConfigRoot.Bind("myConfig", configBind);
            Assert.Equal(config, configBind);
        }
    }
}
