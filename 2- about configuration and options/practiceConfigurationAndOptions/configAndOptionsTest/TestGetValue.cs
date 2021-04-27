using ConfigureationAndOptions;
using Microsoft.Extensions.Configuration;
using System.IO;
using Xunit;

namespace ConfigurationTest
{
    public class TestGetValue
    {
        public ConfigurationBuilder ConfigBuilder { get; }
        public IConfiguration Config { get; }

        public ConfigurationBuilder ConfigBuilderRoot { get; }
        public IConfiguration ConfigRoot { get; }


        public TestGetValue()
        {
            ConfigBuilder = new ConfigurationBuilder();
            ConfigBuilder.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "configObject.json"));
            Config = ConfigBuilder.Build();

            ConfigBuilderRoot = new ConfigurationBuilder();
            ConfigBuilderRoot.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "configObjectWithRoot.json"));
            ConfigRoot = ConfigBuilderRoot.Build();
        }


        // "get value t" method will convert the configuration value (string) to t, where t must be convertable
        // -> return convert t,
        // -> return default input or default (default) if: 1- not exist; 2- cannot convert

       
                        
        // for value type (int)       
        [Fact]
        public void TestGetValueInt()
        {
            // exist key, (int) t
            var keyInt = Config.GetValue<int>("keyInt");           
            Assert.Equal(23, keyInt);

            // non exist key, -> default int
            var key1 = Config.GetValue<int>("Key");
            Assert.Equal(default, key1);

            // non exist key, -> default input
            var key2 = Config.GetValue<int>("key", -23);
            Assert.Equal(-23, key2);
        }


        // for string
        [Fact]
        public void TestGetValueString()
        {
            // exist key, -> string
            var keya = Config.GetValue<string>("Keya");
            Assert.Equal("valuea", keya);

            // non exist key, -> null
            var key1 = Config.GetValue<string>("Key");
            Assert.Null(key1);

            // not exist string, -> default input (string)
            var key2 = Config.GetValue<string>("key", "not exist");
            Assert.Equal("not exist", key2);
        }


        // list cannot be converted from string, -> null or default list input        
        [Fact]
        public void TestGetValueList()
        {
            var defaultList = new SubConfigList();

            // exist key, cannot convert string to list, -> null
            var list1 = Config.GetValue<SubConfigList>("SubList");
            Assert.Null(list1);

            // exist key, cannot convert string to list, -> default list input
            var list2 = Config.GetValue<SubConfigList>("sublist", defaultList);
            Assert.Equal(defaultList, list2);
            
            // non exist key, -> null
            var list3 = Config.GetValue<SubConfigList>("key");
            Assert.Null(list3);

            // non exist key, -> default list input
            var list4 = Config.GetValue<SubConfigList>("key", defaultList);
            Assert.Equal(defaultList, list4);
        }


        // dictionary cannot be converted from string, -> null or default dictionary input

        [Fact]
        public void TestGetValueDictionary()
        {
            var defaultDictionary = new SubConfigDictionary();

            // exist key, cannot convert string to dictionary -> null
            var dictionary1 = Config.GetValue<SubConfigDictionary>("SubDictionary");
            Assert.Null(dictionary1);

            // exist key, cannot convert string to dictionary -> default dictionary input
            var dictionary2 = Config.GetValue<SubConfigDictionary>("subDictionary", defaultDictionary);
            Assert.Equal(defaultDictionary, dictionary2);

            // non exist key, -> null
            var dictionary3 = Config.GetValue<SubConfigDictionary>("key");
            Assert.Null(dictionary3);

            // non exist key, -> default dictionary input
            var dictionary4 = Config.GetValue<SubConfigDictionary>("key", defaultDictionary);
            Assert.Equal(defaultDictionary, dictionary4);
        }


        // reference type cannot be converted from string (not implemente IConvertable),
        // -> null for exist key; null for non exist key or instnce input

        [Fact]
        public void TestGetValueComplex()
        {
            var defaultConfig = new Config();

            // exist key, cannot convert string to complex object, -> null
            var complex1 = ConfigRoot.GetValue<Config>("myConfig");
            Assert.Null(complex1);

            // exist key, cannot convert string to complex object, -> default instance input
            var complex2 = ConfigRoot.GetValue<Config>("myConfig", defaultConfig);
            Assert.Equal(defaultConfig, complex2);
           
            // non exist key, -> null
            var complex3 = Config.GetValue<Config>("key");
            Assert.Null(complex3);

            // non exist key, -> default object instance input
            var complex4 = Config.GetValue<Config>("key", defaultConfig);
            Assert.Equal(defaultConfig, complex4);
        }
    }
}
