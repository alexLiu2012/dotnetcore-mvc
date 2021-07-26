using ConfigureationAndOptions;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ConfigurationTest
{
    public class TestGet
    {
        public ConfigurationBuilder ConfigBuilder { get; }
        public IConfiguration Config { get; }

        public ConfigurationBuilder ConfigBuilderRoot { get; }
        public IConfiguration ConfigRoot { get; }

        public ConfigurationBuilder ConfigBuilderSimple { get; }
        public IConfiguration ConfigSimple { get; }


        public TestGet()
        {
            ConfigBuilder = new ConfigurationBuilder();
            ConfigBuilder.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "configObject.json"));
            Config = ConfigBuilder.Build();

            ConfigBuilderRoot = new ConfigurationBuilder();
            ConfigBuilderRoot.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "configObjectWithRoot.json"));
            ConfigRoot = ConfigBuilderRoot.Build();
        }


        // get<T> method will either convert the value (convertabel) or bind the value
        //
        //  - exist value (not null), convertable, -> convert value
        // 
        //  - exist value (not null), not convertable, -> with children configuration -> bind (collection) 
        //  - exist value (not null), not convertable, -> no children configuration -> return instance(null) (bypass bind process), then default t from "get t" method
        //     
        //  - non exist value (null), -> with children configuration -> bind (collection)
        //  - non exist value (null), -> no children configuration -> return instance(null) (bypass bind process), then default t from "get t" method
        //
        //  - null configuration, -> return instance (null) (bypass bind process), then default t from "get t" method

        
        
        // for int, use convert internal
        [Fact]
        public void TestGetInt()
        {
            // exisit value (whatever key for), -> t (int)
            var keyInt = Config.GetSection("keyInt").Get<int>();
            Assert.Equal(23, keyInt);

            //?

            // null exist value, with children configuration, -> bind config failure (cannot bind to scalar) -> null inside, default int by "get t" method
            var config1 = Config.GetSection("sublist");
            Assert.True(config1.Value is null && config1.GetChildren().Any());

            var key1 = config1.Get<int>();
            Assert.Equal(0, key1);


            // null exist value, no children configuration, -> null inside (bypass bind process), default int by "get t" method           
            var config2 = Config.GetSection("key");
            Assert.True(config2.Value is null && !config2.GetChildren().Any());

            var key2 = Config.GetSection("key").Get<int>();
            Assert.Equal(0, key2);            
        }


        // for string, use convert inside
        [Fact]
        public void TestGetString()
        {                      
            // exist value, -> t (string)            
            var keya = Config.GetSection("Keya").Get<string>();
            Assert.Equal("valuea", keya);

            //?

            // null exist value, with children configuration, -> bind config failure (cannot bind to scalar) -> create string instance => throw exception
            var config1 = Config.GetSection("sublist");
            Assert.True(config1.Value is null && config1.GetChildren().Any());

            Action action = () => config1.Get<string>();
            Assert.ThrowsAny<Exception>(action);


            // null exist value, no children configuration, -> null inside (bypass bind process), default string (null) by "get t" method                           
            var config2 = Config.GetSection("key");
            Assert.True(config2.Value is null && !config2.GetChildren().Any());

            var key2 = config2.Get<string>();
            Assert.Null(key2);            
        }



        // for list
        [Fact]
        public void TestGetList()
        {
            // non exist value, with children configuration, -> bind to t (list)
            var subConfiglist = Config.GetSection("SubList").Get<SubConfigList>();
            Assert.Equal(
                new SubConfigList()
                {
                    "haha",
                    "hehe"
                },
                subConfiglist);

            // non exist value, with children configuration, -> bind config failure (cannot bind dict to list) -> create empty dict
            var config1 = Config.GetSection("subdictionary");
            Assert.True(config1.Value is null && config1.GetChildren().Any());

            var list1 = config1.Get<SubConfigList>();
            Assert.NotNull(list1);


            // non exist value, no children configuration, -> null inside (bypass bind process), default list (null) by "get t" method
            var config2 = Config.GetSection("key");
            Assert.True(config2.Value is null && !config2.GetChildren().Any());            

            var list2 = config2.Get<SubConfigList>();           
            Assert.Null(list2);            
        }


        // for dictionary
        [Fact]
        public void TestGetDictionary()
        {
            // non exist value, with children configuration, -> bind to t (dictionary)
            var subConfigDictionary = Config.GetSection("SubDictionary").Get<SubConfigDictionary>();
            Assert.Equal(
                new SubConfigDictionary()
                {
                    {"SubKeya","SubValuea" },
                    {"SubKeyb","SubValueb" },
                    {"SubKeyc","SubValuec" }
                },
                subConfigDictionary);


            // non exist value, with children configuration, -> bind config failure (cannot bind list to dict) -> create empty dict
            var config1 = Config.GetSection("sublist");
            Assert.True(config1.Value is null && config1.GetChildren().Any());

            var key1 = config1.Get<SubConfigList>();
            Assert.NotNull(key1);


            // non exist value, no children configuration, -> null inside (bypass bind process), default dictionary (null) by "get t" method
            var config2 = Config.GetSection("key");
            Assert.True(config2.Value is null && !config2.GetChildren().Any());

            var dict2 = config2.Get<SubConfigList>();
            Assert.Null(dict2);                        
        }


        // for complex type
        [Fact]
        public void TestGetComplex()
        {            
            var defaultObj = new Config()
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

            // non exist value, with children configuration, -> bind to t (complex type)
            var obj1 = ConfigRoot.GetSection("myConfig").Get<Config>();                        
            Assert.Equal(defaultObj, obj1);

            // non exist value, with child configuration, -> bind configuration failure, -> create empty complex type (=> throw exception if no 'public ctor')
            var obj2 = Config.GetSection("sublist").Get<Config>();
            Assert.NotNull(obj2);

            // non exist value, no children configuration, -> null inside (bypass bind process), -> default t (null) by "get t" method
            var obj3 = Config.GetSection("key").Get<Config>();
            Assert.Null(obj3);            
        }
                
    }
}
