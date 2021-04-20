using ConfigureationAndOptions;
using Microsoft.Extensions.Configuration;
using System.IO;
using Xunit;

namespace configAndOptionsTest
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


        /* "bind()" method will bind property of object from configuration value,
           But not available for type which is convertable (same as "get value" method did) */

        // bind(object) needs to pass a non-null object,
        // the object instance will NOT override if bind failure (no related configuration exist)

        [Fact]
        public void TestBindInt()
        {
            // cannot bind to int (int is convertable)
            int keyInt = 0;
            Config.Bind("keyInt", keyInt);

            Assert.NotEqual(23, keyInt);
        }


        [Fact]
        public void TestBindString()
        {
            // cannot bind to string (string is convertable)
            string keya = string.Empty;
            Config.Bind("keya", keya);

            Assert.NotEqual("valuea", keya);
        }


        [Fact]
        public void TestBindList()
        {
            var list = new SubConfigList()
            {
                "haha",
                "hehe"
            };

            // bind to list
            var listBind = new SubConfigList();
            Config.Bind("SubList", listBind);

            Assert.Equal(list, listBind);
        }


        [Fact]
        public void TestBindDictionary()
        {
            var dictionary = new SubConfigDictionary()
            {
                {"SubKeya","SubValuea" },
                {"SubKeyb","SubValueb" },
                {"SubKeyc","SubValuec" }
            };

            // bind to dictionary
            var dictionaryBind = new SubConfigDictionary();
            Config.Bind("subDictionary", dictionaryBind);

            Assert.Equal(dictionary, dictionaryBind);
        }


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

            // bind to complex type
            var configBind = new Config();
            ConfigRoot.Bind("myConfig", configBind);

            Assert.Equal(config, configBind);
        }
    }
}
